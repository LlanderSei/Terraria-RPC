using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaRPC.Core
{
  public partial class TerrariaMemoryReader
  {
    private sealed class BossCandidate
    {
      public string Name { get; set; } = "";
      public int Hp { get; set; }
      public int MaxHp { get; set; }
      public bool HasShield { get; set; }
      public int Shield { get; set; }
      public int MaxShield { get; set; }
      public bool HitByPlayer { get; set; }
      public bool RecentlyHitByPlayer { get; set; }
      public long HitByPlayerSinceTicks { get; set; }
      public float DistanceSq { get; set; } = float.MaxValue;
      public bool IsPillar { get; set; }
    }

    private readonly Dictionary<string, long> _bossHitSeenTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _bossLastObservedHp = new(StringComparer.OrdinalIgnoreCase);

    private static bool IsNpcHitByPlayer(ClrObject npcObj, int myPlayer)
    {
      if (myPlayer < 0)
      {
        return false;
      }

      try
      {
        var interactionArrayObj = npcObj.ReadObjectField("playerInteraction");
        if (interactionArrayObj.IsValid && interactionArrayObj.IsArray)
        {
          var interactionType = interactionArrayObj.Type;
          if (interactionType != null)
          {
            return interactionType.ReadArrayElements<bool>(interactionArrayObj.Address, myPlayer, 1).FirstOrDefault();
          }
        }

        return npcObj.ReadField<int>("lastInteraction") == myPlayer;
      }
      catch
      {
        return false;
      }
    }

    private long GetOrUpdateBossHitTicks(string bossName, int currentHp, bool hitByPlayer, long nowTicks)
    {
      if (_bossLastObservedHp.TryGetValue(bossName, out int previousHp) && currentHp < previousHp)
      {
        _bossHitSeenTicks[bossName] = nowTicks;
      }

      _bossLastObservedHp[bossName] = currentHp;

      if (hitByPlayer && !_bossHitSeenTicks.ContainsKey(bossName))
      {
        _bossHitSeenTicks[bossName] = nowTicks;
      }

      if (!hitByPlayer && !_bossHitSeenTicks.ContainsKey(bossName))
      {
        return 0;
      }

      return _bossHitSeenTicks.TryGetValue(bossName, out long seenTicks) ? seenTicks : 0;
    }

    private void ScanBossesAndEvents(ClrRuntime runtime, ClrAppDomain appDomain, ClrType mainType, RpcConfig config)
    {
      static bool TryReadVector2Center(ClrObject obj, string fieldName, out float centerX, out float centerY)
      {
        centerX = 0f;
        centerY = 0f;

        var valueField = obj.Type?.Fields.FirstOrDefault(f => f.Name == fieldName);
        var valueType = valueField?.Type;
        if (valueField == null || valueType == null) return false;

        var xField = valueType.Fields.FirstOrDefault(f => f.Name == "X");
        var yField = valueType.Fields.FirstOrDefault(f => f.Name == "Y");
        if (xField == null || yField == null) return false;

        ulong address = (ulong)((long)obj.Address + valueField.Offset + IntPtr.Size);
        float x = xField.Read<float>(address, interior: true);
        float y = yField.Read<float>(address, interior: true);
        int width = obj.ReadField<int>("width");
        int height = obj.ReadField<int>("height");

        centerX = x + width * 0.5f;
        centerY = y + height * 0.5f;
        return true;
      }

      // Reset active entity states before scanning
      CurrentState.ActiveBossName = "";
      CurrentState.ActiveBossHp = 0;
      CurrentState.ActiveBossMaxHp = 0;
      CurrentState.ActiveBossHasShield = false;
      CurrentState.ActiveBossSp = 0;
      CurrentState.ActiveBossMaxSp = 0;

      CurrentState.ActiveEventName = "";
      CurrentState.ActiveEventProgress = -1;
      CurrentState.ActiveEventWaveNum = -1;
      CurrentState.ActiveEventHasProgress = false;
      CurrentState.ActiveEventIsAtMaxWave = false;
      CurrentState.ActiveEventIsAtMaxProgression = false;
      CurrentState.ActiveEventProgression = -1;
      CurrentState.ActiveEventPoints = 0;

      CurrentState.ActiveNonProgressiveEventName = "";
      CurrentState.ActiveNonProgressiveEventValue = "";
      CurrentState.ActivePeacefulEventName = "";
      CurrentState.ActivePeacefulEventValue = "";
      CurrentState.ActiveWeatherName = "";
      bool lunarEventActive = false;
      long nowTicks = DateTime.UtcNow.Ticks;

      if (CurrentState.GameMenu || (CurrentState.Screen != GameScreen.InGameSinglePlayer && CurrentState.Screen != GameScreen.InGameMultiplayer)) return;

      try
      {
        // 1. Scan Active Bosses from Main.npc
        var npcField = mainType.StaticFields.FirstOrDefault(f => f.Name == "npc");
        if (npcField != null)
        {
          ulong npcArrayAddr = npcField.Read<ulong>(appDomain);
          if (npcArrayAddr != 0)
          {
            var npcArrayObj = runtime.Heap.GetObject(npcArrayAddr);
            if (npcArrayObj.IsValid && npcArrayObj.IsArray)
            {
              int len = npcArrayObj.AsArray().Length;
              int myPlayer = CurrentState.PlayerIndex;

              // Brain of Cthulhu tracking: combine the Brain and Creepers into one boss total
              int bocLife = 0, bocLifeMax = 0, bocCount = 0;
              bool bocHitByPlayer = false;
              float bocDistanceSq = float.MaxValue;
              // Eater of Worlds tracking: combine all active worm segments into one boss total
              int eowLife = 0, eowLifeMax = 0, eowCount = 0;
              bool eowHitByPlayer = false;
              float eowDistanceSq = float.MaxValue;
              // Golem tracking: combine the body, head, and fists.
              int golemLife = 0, golemLifeMax = 0, golemCount = 0;
              bool golemHitByPlayer = false;
              float golemDistanceSq = float.MaxValue;
              // Moon Lord tracking: combine the core, hands, and head.
              int moonLordLife = 0, moonLordLifeMax = 0, moonLordCount = 0;
              bool moonLordHitByPlayer = false;
              float moonLordDistanceSq = float.MaxValue;
              // Martian Saucer: combine its core, cannon, and turret parts.
              int martianSaucerLife = 0, martianSaucerLifeMax = 0, martianSaucerCount = 0;
              bool martianSaucerHitByPlayer = false;
              float martianSaucerDistanceSq = float.MaxValue;
              float playerCenterX = CurrentState.PlayerHasPosition ? CurrentState.PlayerCenterX : 0f;
              float playerCenterY = CurrentState.PlayerHasPosition ? CurrentState.PlayerCenterY : 0f;
              bool hasPlayerCenter = CurrentState.PlayerHasPosition;
              const float pillarPriorityRangePx = 1600f;
              float pillarPriorityRangeSq = pillarPriorityRangePx * pillarPriorityRangePx;
              string nearestPillarName = "";
              int nearestPillarHp = 0;
              int nearestPillarMaxHp = 0;
              bool nearestPillarHasShield = false;
              int nearestPillarShield = 0;
              int nearestPillarMaxShield = 0;
              float nearestPillarDistanceSq = float.MaxValue;

              bool UseHitPriority()
              {
                var bossPriority = config.InGame.BossAndEventPriority.BossPriority;
                return bossPriority.PrioritizeTargetHitBoss || (!bossPriority.PrioritizeNearestBoss && !bossPriority.PrioritizeHighestHealthBoss);
              }

              bool IsBetterBossCandidate(BossCandidate candidate, BossCandidate? current)
              {
                if (current == null || string.IsNullOrEmpty(current.Name))
                {
                  return true;
                }

                if (UseHitPriority() && candidate.RecentlyHitByPlayer != current.RecentlyHitByPlayer)
                {
                  return candidate.RecentlyHitByPlayer;
                }

                if (UseHitPriority() && candidate.RecentlyHitByPlayer && current.RecentlyHitByPlayer && candidate.HitByPlayerSinceTicks != current.HitByPlayerSinceTicks)
                {
                  return candidate.HitByPlayerSinceTicks > current.HitByPlayerSinceTicks;
                }

                var bossPriority = config.InGame.BossAndEventPriority.BossPriority;

                if (bossPriority.PrioritizeNearestBoss && candidate.DistanceSq != current.DistanceSq)
                {
                  return candidate.DistanceSq < current.DistanceSq;
                }

                if (bossPriority.PrioritizeHighestHealthBoss && candidate.Hp != current.Hp)
                {
                  return candidate.Hp > current.Hp;
                }

                if (candidate.Hp != current.Hp)
                {
                  return candidate.Hp > current.Hp;
                }

                if (candidate.MaxHp != current.MaxHp)
                {
                  return candidate.MaxHp > current.MaxHp;
                }

                if (candidate.HasShield != current.HasShield)
                {
                  return candidate.HasShield;
                }

                return candidate.DistanceSq < current.DistanceSq;
              }

              BossCandidate? bestBoss = null;

              void ConsiderBoss(BossCandidate candidate)
              {
                if (string.IsNullOrWhiteSpace(candidate.Name) || candidate.MaxHp <= 0)
                {
                  return;
                }

                if (IsBetterBossCandidate(candidate, bestBoss))
                {
                  bestBoss = candidate;
                }
              }

              for (int i = 0; i < len; i++)
              {
                var npcObj = npcArrayObj.AsArray().GetObjectValue(i);
                if (!npcObj.IsValid) continue;

                bool active = npcObj.ReadField<bool>("active");
                if (!active) continue;

                bool isBoss = npcObj.ReadField<bool>("boss");
                int type = npcObj.ReadField<int>("type");

                // Corrected pillar IDs (confirmed by user from Terraria wiki):
                // 493=LunarTowerStardust, 517=LunarTowerSolar, 507=LunarTowerNebula, 422=LunarTowerVortex
                bool isPillar = type == 493 || type == 517 || type == 507 || type == 422;

                if (isBoss || isPillar || IsKnownBossType(type))
                {
                  int life = npcObj.ReadField<int>("life");
                  int lifeMax = npcObj.ReadField<int>("lifeMax");
                  bool moonLordPart = type == 396 || type == 397 || type == 398;
                  if (moonLordPart && _moonLordPreviousPartHp.TryGetValue(i, out int previousPartHp) &&
                      previousPartHp > 0 && lifeMax > 0 && previousPartHp <= lifeMax / 10 && life >= lifeMax * 3 / 4)
                  {
                    _moonLordDefeatedSlots.Add(i);
                    Logger.DebugThrottled(
                      $"moon-lord-part-reset:{i}",
                      $"[Moon Lord] part slot={i} type={type} reset from {previousPartHp}/{lifeMax} to {life}/{lifeMax}; treating part as defeated");
                  }
                  if (moonLordPart && life > 0)
                    _moonLordPreviousPartHp[i] = life;
                  if (moonLordPart && life <= 0)
                  {
                    _moonLordDefeatedSlots.Add(i);
                    continue;
                  }

                  // Terraria can recreate a defeated eye in the same NPC slot
                  // during a phase transition. Keep that slot excluded for the
                  // remainder of the current Moon Lord encounter.
                  if (moonLordPart && _moonLordDefeatedSlots.Contains(i))
                    continue;

                  // Skip dead/inactive NPC slots (life<=0 means the slot is empty or dead)
                  if (life <= 0) continue;

                  string typeName = GetNpcTypeName(runtime, appDomain, npcObj, type);
                  if (string.IsNullOrEmpty(typeName)) continue;

                  bool hitByPlayer = IsNpcHitByPlayer(npcObj, myPlayer);
                  long hitByPlayerSinceTicks = GetOrUpdateBossHitTicks(typeName, life, hitByPlayer, nowTicks);
                  bool recentlyHitByPlayer = hitByPlayerSinceTicks > 0 && (nowTicks - hitByPlayerSinceTicks) <= TimeSpan.FromSeconds(3).Ticks;
                  float distanceSq = float.MaxValue;
                  float npcCenterX = 0f;
                  float npcCenterY = 0f;
                  if (hasPlayerCenter && TryReadVector2Center(npcObj, "position", out npcCenterX, out npcCenterY))
                  {
                    float dx = npcCenterX - playerCenterX;
                    float dy = npcCenterY - playerCenterY;
                    distanceSq = dx * dx + dy * dy;
                    if (isPillar)
                    {
                      Logger.DebugThrottled(
                        $"pillar-position:{type}",
                        $"[Pillar Position] {typeName} type={type} player=({playerCenterX:0.0},{playerCenterY:0.0}) pillar=({npcCenterX:0.0},{npcCenterY:0.0}) distanceSq={distanceSq:0.0}");
                    }
                  }

                  // Debug: log every NPC that passes the boss check so we can verify type IDs
                  Logger.Debug($"[BossFound] type={type} boss={isBoss} isPillar={isPillar} life={life}/{lifeMax} name={typeName}");
                  if (moonLordPart)
                  {
                    Logger.DebugThrottled(
                      $"moon-lord-part:{i}",
                      $"[Moon Lord Part] slot={i} type={type} life={life}/{lifeMax} defeated={_moonLordDefeatedSlots.Contains(i)}");
                  }

                  if (type == 125 || type == 126)
                  {
                    ConsiderBoss(new BossCandidate
                    {
                      Name = typeName,
                      Hp = life,
                      MaxHp = lifeMax,
                      HasShield = false,
                      Shield = 0,
                      MaxShield = 0,
                      HitByPlayer = hitByPlayer,
                      RecentlyHitByPlayer = recentlyHitByPlayer,
                      HitByPlayerSinceTicks = hitByPlayerSinceTicks,
                      DistanceSq = distanceSq,
                      IsPillar = false
                    });
                    continue;
                  }

                  // Brain of Cthulhu: aggregate the brain and all creepers.
                  if (type == 266 || type == 267)
                  {
                    bocLife += life;
                    bocLifeMax += lifeMax;
                    bocCount++;
                    bocHitByPlayer |= hitByPlayer;
                    bocDistanceSq = Math.Min(bocDistanceSq, distanceSq);
                    continue;
                  }

                  // Eater of Worlds: each segment is a separate NPC, so sum the whole worm.
                  if (type == 13 || type == 14 || type == 15)
                  {
                    eowLife += life;
                    eowLifeMax += lifeMax;
                    eowCount++;
                    eowHitByPlayer |= hitByPlayer;
                    eowDistanceSq = Math.Min(eowDistanceSq, distanceSq);
                    continue;
                  }

                  // Skeletron Prime: only the head counts for HP/status.
                  if (type == 128 || type == 129 || type == 130 || type == 131)
                  {
                    continue;
                  }

                  if (type == 127)
                  {
                    ConsiderBoss(new BossCandidate
                    {
                      Name = typeName,
                      Hp = life,
                      MaxHp = lifeMax,
                      HasShield = false,
                      Shield = 0,
                      MaxShield = 0,
                      HitByPlayer = hitByPlayer,
                      RecentlyHitByPlayer = recentlyHitByPlayer,
                      HitByPlayerSinceTicks = hitByPlayerSinceTicks,
                      DistanceSq = distanceSq,
                      IsPillar = false
                    });
                    continue;
                  }

                  // Golem: aggregate the body, head, and both fists.
                  if (type == 245 || type == 246 || type == 247 || type == 248)
                  {
                    golemLife += life;
                    golemLifeMax += lifeMax;
                    golemCount++;
                    golemHitByPlayer |= hitByPlayer;
                    golemDistanceSq = Math.Min(golemDistanceSq, distanceSq);
                    continue;
                  }

                  // Moon Lord: aggregate the core, head, and both hands.
                  if (moonLordPart)
                  {
                    moonLordLife += life;
                    moonLordLifeMax += lifeMax;
                    moonLordCount++;
                    moonLordHitByPlayer |= hitByPlayer;
                    moonLordDistanceSq = Math.Min(moonLordDistanceSq, distanceSq);
                    continue;
                  }

                  // Martian Saucer: each part is a separate NPC slot.
                  if (type == 392 || type == 393 || type == 394 || type == 395)
                  {
                    martianSaucerLife += life;
                    martianSaucerLifeMax += lifeMax;
                    martianSaucerCount++;
                    martianSaucerHitByPlayer |= hitByPlayer;
                    martianSaucerDistanceSq = Math.Min(martianSaucerDistanceSq, distanceSq);
                    continue;
                  }

                  if (isPillar)
                  {
                    lunarEventActive = true;
                    ClrType shieldType = npcObj.Type ?? mainType;
                    int shield = GetPillarShield(shieldType, appDomain, type);
                    int maxShield = GetPillarMaxShield(shieldType, appDomain);
                    bool pillarHasShield = shield > 0;
                    Logger.DebugThrottled(
                      $"pillar-shield:{type}",
                      $"[Pillar Shield] {typeName} type={type} shield={shield} max={maxShield} life={life}/{lifeMax} distanceSq={distanceSq:0}");

                    if (distanceSq < nearestPillarDistanceSq)
                    {
                      nearestPillarDistanceSq = distanceSq;
                      nearestPillarName = typeName;
                      nearestPillarHasShield = pillarHasShield;
                      nearestPillarShield = shield;
                      nearestPillarMaxShield = maxShield > 0 ? maxShield : shield;
                      nearestPillarHp = life;
                      nearestPillarMaxHp = lifeMax;
                    }
                    continue;
                  }

                  ConsiderBoss(new BossCandidate
                  {
                    Name = typeName,
                    Hp = life,
                    MaxHp = lifeMax,
                    HasShield = false,
                    Shield = 0,
                    MaxShield = 0,
                    HitByPlayer = hitByPlayer,
                    RecentlyHitByPlayer = recentlyHitByPlayer,
                    HitByPlayerSinceTicks = hitByPlayerSinceTicks,
                    DistanceSq = distanceSq,
                    IsPillar = false
                  });
                }
              }

              // Resolve Brain of Cthulhu post-loop.
              if (bocCount > 0)
              {
                _lastBocMaxHp = Math.Max(_lastBocMaxHp, bocLifeMax);
                ConsiderBoss(new BossCandidate
                {
                  Name = "Brain of Cthulhu",
                  Hp = bocLife,
                  MaxHp = _lastBocMaxHp > 0 ? _lastBocMaxHp : bocLifeMax,
                  HasShield = false,
                  Shield = 0,
                  MaxShield = 0,
                  HitByPlayer = bocHitByPlayer,
                  RecentlyHitByPlayer = GetOrUpdateBossHitTicks("Brain of Cthulhu", bocLife, bocHitByPlayer, nowTicks) > 0,
                  HitByPlayerSinceTicks = GetOrUpdateBossHitTicks("Brain of Cthulhu", bocLife, bocHitByPlayer, nowTicks),
                  DistanceSq = bocDistanceSq,
                  IsPillar = false
                });
              }
              else
              {
                _lastBocMaxHp = 0;
              }

              // Resolve Eater of Worlds post-loop.
              if (eowCount > 0)
              {
                _lastEowMaxHp = Math.Max(_lastEowMaxHp, eowLifeMax);
                ConsiderBoss(new BossCandidate
                {
                  Name = "Eater of Worlds",
                  Hp = eowLife,
                  MaxHp = _lastEowMaxHp > 0 ? _lastEowMaxHp : eowLifeMax,
                  HasShield = false,
                  Shield = 0,
                  MaxShield = 0,
                  HitByPlayer = eowHitByPlayer,
                  RecentlyHitByPlayer = GetOrUpdateBossHitTicks("Eater of Worlds", eowLife, eowHitByPlayer, nowTicks) > 0,
                  HitByPlayerSinceTicks = GetOrUpdateBossHitTicks("Eater of Worlds", eowLife, eowHitByPlayer, nowTicks),
                  DistanceSq = eowDistanceSq,
                  IsPillar = false
                });
              }
              else
              {
                _lastEowMaxHp = 0;
              }

              // Resolve Golem post-loop.
              if (golemCount > 0)
              {
                _lastGolemMaxHp = Math.Max(_lastGolemMaxHp, golemLifeMax);
                ConsiderBoss(new BossCandidate
                {
                  Name = "Golem",
                  Hp = golemLife,
                  MaxHp = _lastGolemMaxHp,
                  HasShield = false,
                  Shield = 0,
                  MaxShield = 0,
                  HitByPlayer = golemHitByPlayer,
                  RecentlyHitByPlayer = GetOrUpdateBossHitTicks("Golem", golemLife, golemHitByPlayer, nowTicks) > 0,
                  HitByPlayerSinceTicks = GetOrUpdateBossHitTicks("Golem", golemLife, golemHitByPlayer, nowTicks),
                  DistanceSq = golemDistanceSq,
                  IsPillar = false
                });
              }
              else
              {
                _lastGolemMaxHp = 0;
              }

              // Resolve Moon Lord post-loop.
              if (moonLordCount > 0)
              {
                _moonLordMissingScans = 0;
                _lastMoonLordMaxHp = Math.Max(_lastMoonLordMaxHp, moonLordLifeMax);
                ConsiderBoss(new BossCandidate
                {
                  Name = "Moon Lord",
                  Hp = moonLordLife,
                  MaxHp = _lastMoonLordMaxHp,
                  HasShield = false,
                  Shield = 0,
                  MaxShield = 0,
                  HitByPlayer = moonLordHitByPlayer,
                  RecentlyHitByPlayer = GetOrUpdateBossHitTicks("Moon Lord", moonLordLife, moonLordHitByPlayer, nowTicks) > 0,
                  HitByPlayerSinceTicks = GetOrUpdateBossHitTicks("Moon Lord", moonLordLife, moonLordHitByPlayer, nowTicks),
                  DistanceSq = moonLordDistanceSq,
                  IsPillar = false
                });
              }
              else
              {
                _moonLordMissingScans++;
                // Moon Lord can temporarily remove every part during a phase
                // transition. Keep the encounter state across that gap and
                // reset only after a longer absence.
                if (_moonLordMissingScans >= 15)
                {
                  _lastMoonLordMaxHp = 0;
                  _moonLordDefeatedSlots.Clear();
                  _moonLordPreviousPartHp.Clear();
                  _moonLordMissingScans = 0;
                }
              }

              if (martianSaucerCount > 0)
              {
                ConsiderBoss(new BossCandidate
                {
                  Name = "Martian Saucer",
                  Hp = martianSaucerLife,
                  MaxHp = martianSaucerLifeMax,
                  HasShield = false,
                  Shield = 0,
                  MaxShield = 0,
                  HitByPlayer = martianSaucerHitByPlayer,
                  RecentlyHitByPlayer = GetOrUpdateBossHitTicks("Martian Saucer", martianSaucerLife, martianSaucerHitByPlayer, nowTicks) > 0,
                  HitByPlayerSinceTicks = GetOrUpdateBossHitTicks("Martian Saucer", martianSaucerLife, martianSaucerHitByPlayer, nowTicks),
                  DistanceSq = martianSaucerDistanceSq,
                  IsPillar = false
                });
              }

              // Pillars compete as one group. Their internal selection is
              // always nearest-first; the resulting pillar then competes with
              // other bosses using the configured priority rules.
              if (!string.IsNullOrEmpty(nearestPillarName))
              {
                ConsiderBoss(new BossCandidate
                {
                  Name = nearestPillarName,
                  Hp = nearestPillarHasShield ? nearestPillarShield : nearestPillarHp,
                  MaxHp = nearestPillarHasShield ? nearestPillarMaxShield : nearestPillarMaxHp,
                  HasShield = nearestPillarHasShield,
                  Shield = nearestPillarShield,
                  MaxShield = nearestPillarMaxShield,
                  DistanceSq = nearestPillarDistanceSq,
                  IsPillar = true
                });
              }

              var priority = config.InGame.BossAndEventPriority.BossPriority;
              Logger.DebugThrottled(
                "pillar-selection-state",
                $"[Pillar Selection] nearest={nearestPillarName} distanceSq={nearestPillarDistanceSq:0.0} shield={nearestPillarShield}/{nearestPillarMaxShield} lunarNearby={priority.PrioritizeLunarPillarsNearby} targetHit={priority.PrioritizeTargetHitBoss} nearestBoss={priority.PrioritizeNearestBoss} highestHealth={priority.PrioritizeHighestHealthBoss}");

              if (priority.PrioritizeLunarPillarsNearby && !string.IsNullOrEmpty(nearestPillarName) && nearestPillarDistanceSq <= pillarPriorityRangeSq)
              {
                Logger.DebugThrottled(
                  "pillar-selection",
                  $"[Pillar Selection] selected={nearestPillarName} distanceSq={nearestPillarDistanceSq:0.0} shield={nearestPillarShield}/{nearestPillarMaxShield}");
                bestBoss = new BossCandidate
                {
                  Name = nearestPillarName,
                  Hp = nearestPillarHasShield ? nearestPillarShield : nearestPillarHp,
                  MaxHp = nearestPillarHasShield ? nearestPillarMaxShield : nearestPillarMaxHp,
                  HasShield = nearestPillarHasShield,
                  Shield = nearestPillarShield,
                  MaxShield = nearestPillarMaxShield,
                  HitByPlayer = true,
                  DistanceSq = nearestPillarDistanceSq,
                  IsPillar = true
                };
              }

              if (bestBoss != null)
              {
                CurrentState.ActiveBossName = bestBoss.Name;
                CurrentState.ActiveBossHp = bestBoss.Hp;
                CurrentState.ActiveBossMaxHp = bestBoss.MaxHp;
                CurrentState.ActiveBossHasShield = bestBoss.HasShield;
                CurrentState.ActiveBossSp = bestBoss.HasShield ? bestBoss.Shield : 0;
                CurrentState.ActiveBossMaxSp = bestBoss.HasShield ? bestBoss.MaxShield : 0;
              }
            }
          }
        }

        // 2. Progressive Events (Invasion, Pumpkin/Frost Moon, Old One's Army)
        int ReadMainInt(params string[] names)
        {
          foreach (string name in names)
          {
            var field = mainType.StaticFields.FirstOrDefault(f => f.Name == name);
            if (field == null) continue;

            try { return field.Read<int>(appDomain); } catch { }
            try { return field.Read<short>(appDomain); } catch { }
            try { return field.Read<byte>(appDomain); } catch { }
          }

          return 0;
        }

        int invasionType = ReadMainInt("invasionType");
        int invasionProgress = ReadMainInt("invasionProgress");
        int invasionProgressMax = ReadMainInt("invasionProgressMax");
        // invasionWave is used by OOA and moon events for the current wave number
        int invasionWave = ReadMainInt("invasionWave", "pumpkinMoonWave", "snowMoonWave");
        bool pumpkinMoonActive = mainType.StaticFields.FirstOrDefault(f => f.Name == "pumpkinMoon")?.Read<bool>(appDomain) ?? false;
        bool frostMoonActive = mainType.StaticFields.FirstOrDefault(f => f.Name == "snowMoon")?.Read<bool>(appDomain) ?? false;
        string moonEvent = pumpkinMoonActive ? "Pumpkin Moon" : frostMoonActive ? "Frost Moon" : "";
        if (moonEvent.Length == 0)
        {
          _moonWaveEvent = "";
          _moonWave = -1;
          _moonLastProgress = -1;
          _moonLastProgressPercent = -1;
        }
        else if (invasionWave <= 0)
        {
          if (!string.Equals(_moonWaveEvent, moonEvent, StringComparison.OrdinalIgnoreCase))
          {
            _moonWaveEvent = moonEvent;
            _moonWave = 1;
            _moonLastProgress = -1;
          }

          // Moon progress resets when the next wave begins. Ignore the initial
          // 0/1 setup value so it cannot create a false extra wave.
          if (invasionProgressMax > 1 && invasionProgress >= 0)
          {
            if (_moonLastProgress >= 0 && invasionProgress < _moonLastProgress)
            {
              _moonWave++;
            }

            _moonLastProgress = invasionProgress;
          }

          invasionWave = _moonWave;
        }
        if (pumpkinMoonActive || frostMoonActive)
        {
          Logger.DebugThrottled(
            "moon-event-fields",
            $"[Moon Event] Pumpkin={pumpkinMoonActive} Frost={frostMoonActive} " +
            $"invasionWave={invasionWave} invasionProgress={invasionProgress}/{invasionProgressMax}"
          );
        }

        // Old One's Army: uses a dedicated DD2Event static class
        var dd2Type = TryGetCachedType(runtime, ref _dd2EventTypeMT, "Terraria.GameContent.Events.DD2Event");
        bool dd2Active = false;
        if (dd2Type != null)
        {
          dd2Active = dd2Type.StaticFields.FirstOrDefault(f => f.Name == "Ongoing")?.Read<bool>(appDomain) ?? false;
          if (dd2Active)
          {
            // Diagnostics: dump all static fields of DD2Event and Main invasion fields
            var fieldValues = new List<string>();
            foreach (var f in dd2Type.StaticFields)
            {
              try
              {
                int valInt = f.Read<int>(appDomain);
                fieldValues.Add($"{f.Name}={valInt}");
              }
              catch
              {
                try
                {
                  bool valBool = f.Read<bool>(appDomain);
                  fieldValues.Add($"{f.Name}={valBool}");
                }
                catch { }
              }
            }
            Logger.Debug($"[OOA Diagnostics] Main.invasionWave={invasionWave}, invasionType={invasionType}, invasionProgress={invasionProgress}/{invasionProgressMax} | DD2Fields: {string.Join(", ", fieldValues)}");

            // Old One's Army wave mapping based on invasionProgressMax values:
            // Tier 1 (5 waves): W1=60, W2=80, W3=100, W4=120, W5=140
            // Tier 2/3 (7 waves): W1=60, W2=80, W3=100, W4=120, W5=140, W6=180, W7=220
            int mappedDd2Wave = invasionProgressMax switch
            {
              60 => 1,
              80 => 2,
              100 => 3,
              120 => 4,
              140 => 5,
              180 => 6,
              220 => 7,
              _ => -1
            };

            // If we are currently in intermission (_timeLeftUntilSpawningBegins > 0 or invasionProgressMax == 1), preserve the current wave number
            int intermissionTime = dd2Type.StaticFields.FirstOrDefault(f => f.Name == "_timeLeftUntilSpawningBegins")?.Read<int>(appDomain) ?? 0;
            int dd2Wave;
            if (intermissionTime > 0 || invasionProgressMax <= 1)
            {
              dd2Wave = _lastKnownOoaWave > 0 ? _lastKnownOoaWave : mappedDd2Wave;
            }
            else if (mappedDd2Wave > 0 && (_lastKnownOoaWave <= 0 || mappedDd2Wave >= _lastKnownOoaWave))
            {
              _lastKnownOoaWave = mappedDd2Wave;
              dd2Wave = mappedDd2Wave;
            }
            else if (_lastKnownOoaWave > 0)
            {
              dd2Wave = _lastKnownOoaWave;
            }
            else
            {
              dd2Wave = -1;
            }

            int pct = (intermissionTime > 0 || invasionProgressMax <= 1) ? 100 : (invasionProgressMax > 0 ? (int)(invasionProgress * 100.0 / invasionProgressMax) : -1);
            CurrentState.ActiveEventName = "Old One's Army";
            CurrentState.ActiveEventHasProgress = true;
            CurrentState.ActiveEventProgress = pct >= 0 ? Math.Min(100, pct) : -1;
            CurrentState.ActiveEventProgression = CurrentState.ActiveEventProgress;
            CurrentState.ActiveEventPoints = invasionProgress;
            CurrentState.ActiveEventWaveNum = dd2Wave > 0 ? dd2Wave : 1;
            CurrentState.ActiveEventIsAtMaxWave = false;
            CurrentState.ActiveEventIsAtMaxProgression = CurrentState.ActiveEventProgress >= 100;
          }
          else
          {
            _lastKnownOoaWave = -1;
          }
        }
        else
        {
          _lastKnownOoaWave = -1;
        }

        if (!dd2Active)
        {
          if (invasionType > 0)
          {
            string invName = invasionType switch
            {
              1 => "Goblin Invasion",
              2 => "Frost Legion",
              3 => "Pirate Invasion",
              4 => "Martian Madness",
              _ => "Invasion"
            };
            int pct = invasionProgressMax > 0 ? (int)(invasionProgress * 100.0 / invasionProgressMax) : 0;
            CurrentState.ActiveEventName = invName;
            CurrentState.ActiveEventHasProgress = true;
            CurrentState.ActiveEventProgress = Math.Min(100, Math.Max(0, pct));
            CurrentState.ActiveEventProgression = CurrentState.ActiveEventProgress;
            CurrentState.ActiveEventPoints = invasionProgress;
            CurrentState.ActiveEventIsAtMaxWave = false;
            CurrentState.ActiveEventIsAtMaxProgression = CurrentState.ActiveEventProgress >= 100;
          }
          else if (mainType.StaticFields.FirstOrDefault(f => f.Name == "slimeRain")?.Read<bool>(appDomain) ?? false)
          {
            CurrentState.ActiveEventName = "Slime Rain";
            CurrentState.ActiveEventHasProgress = false;
            CurrentState.ActiveEventProgress = -1;
            CurrentState.ActiveEventProgression = -1;
            CurrentState.ActiveEventPoints = 0;
            CurrentState.ActiveEventIsAtMaxWave = false;
            CurrentState.ActiveEventIsAtMaxProgression = false;
          }
          else if (pumpkinMoonActive)
          {
            CurrentState.ActiveEventName = "Pumpkin Moon";
            CurrentState.ActiveEventHasProgress = true;
            int moonProgress = invasionProgressMax <= 1
              ? (_moonLastProgressPercent >= 95 && invasionWave >= 19 ? 100 : _moonLastProgressPercent)
              : invasionProgressMax > 0
              ? Math.Min(100, Math.Max(0, (int)(invasionProgress * 100.0 / invasionProgressMax)))
              : _moonLastProgressPercent;
            if (moonProgress >= 0)
              _moonLastProgressPercent = moonProgress;
            CurrentState.ActiveEventProgress = moonProgress;
            CurrentState.ActiveEventWaveNum = moonProgress >= 100 && invasionWave >= 19
              ? 20
              : invasionWave > 0 ? invasionWave : -1;
            CurrentState.ActiveEventProgression = moonProgress;
            CurrentState.ActiveEventPoints = invasionProgress;
            CurrentState.ActiveEventIsAtMaxWave = CurrentState.ActiveEventWaveNum >= 20;
            CurrentState.ActiveEventIsAtMaxProgression = CurrentState.ActiveEventIsAtMaxWave;
          }
          else if (frostMoonActive)
          {
            CurrentState.ActiveEventName = "Frost Moon";
            CurrentState.ActiveEventHasProgress = true;
            int moonProgress = invasionProgressMax <= 1
              ? (_moonLastProgressPercent >= 95 && invasionWave >= 19 ? 100 : _moonLastProgressPercent)
              : invasionProgressMax > 0
              ? Math.Min(100, Math.Max(0, (int)(invasionProgress * 100.0 / invasionProgressMax)))
              : _moonLastProgressPercent;
            if (moonProgress >= 0)
              _moonLastProgressPercent = moonProgress;
            CurrentState.ActiveEventProgress = moonProgress;
            CurrentState.ActiveEventWaveNum = moonProgress >= 100 && invasionWave >= 19
              ? 20
              : invasionWave > 0 ? invasionWave : -1;
            CurrentState.ActiveEventProgression = moonProgress;
            CurrentState.ActiveEventPoints = invasionProgress;
            CurrentState.ActiveEventIsAtMaxWave = CurrentState.ActiveEventWaveNum >= 20;
            CurrentState.ActiveEventIsAtMaxProgression = CurrentState.ActiveEventIsAtMaxWave;
          }
        }

        // 3. Non-Progressive Events (Lunar Event, Blood Moon, Solar Eclipse, Torch God, Slime Rain)
        bool bloodMoonActive = mainType.StaticFields.FirstOrDefault(f => f.Name == "bloodMoon")?.Read<bool>(appDomain) ?? false;
        bool eclipseActive = mainType.StaticFields.FirstOrDefault(f => f.Name == "eclipse")?.Read<bool>(appDomain) ?? false;
        bool slimeRainActive = mainType.StaticFields.FirstOrDefault(f => f.Name == "slimeRain")?.Read<bool>(appDomain) ?? false;
        bool torchGodActive = CurrentState.TorchGodActive;

        if (lunarEventActive)
        {
          CurrentState.ActiveNonProgressiveEventName = "Lunar Event";
          CurrentState.ActiveNonProgressiveEventValue = "LunarEvent";
        }
        else if (bloodMoonActive)
        {
          CurrentState.ActiveNonProgressiveEventName = "Blood Moon";
          CurrentState.ActiveNonProgressiveEventValue = "BloodMoon";
        }
        else if (eclipseActive)
        {
          CurrentState.ActiveNonProgressiveEventName = "Solar Eclipse";
          CurrentState.ActiveNonProgressiveEventValue = "SolarEclipse";
        }
        else if (torchGodActive)
        {
          CurrentState.ActiveNonProgressiveEventName = "The Torch God";
          CurrentState.ActiveNonProgressiveEventValue = "TorchGod";
        }
        else if (slimeRainActive)
        {
          CurrentState.ActiveNonProgressiveEventName = "Slime Rain";
          CurrentState.ActiveNonProgressiveEventValue = "SlimeRain";
        }

        // 4. Peaceful Events (Party, Lantern Night)
        var partyType = TryGetCachedType(runtime, ref _birthdayPartyTypeMT, "Terraria.GameContent.Events.BirthdayParty");
        float starfallBoost = mainType.StaticFields.FirstOrDefault(f => f.Name == "starfallBoost")?.Read<float>(appDomain) ?? 1f;
        bool starfall = !CurrentState.GameMenu && starfallBoost > 1.01f;
        if (starfall)
        {
          CurrentState.ActivePeacefulEventName = "Starfall";
          CurrentState.ActivePeacefulEventValue = "Starfall";
        }

        if (partyType != null)
        {
          bool manualParty = partyType.StaticFields.FirstOrDefault(f => f.Name == "ManualParty")?.Read<bool>(appDomain) ?? false;
          bool genuineParty = partyType.StaticFields.FirstOrDefault(f => f.Name == "GenuineParty")?.Read<bool>(appDomain) ?? false;
          if (string.IsNullOrEmpty(CurrentState.ActivePeacefulEventName) && (manualParty || genuineParty))
          {
            CurrentState.ActivePeacefulEventName = "Party";
            CurrentState.ActivePeacefulEventValue = "Party";
          }
        }
        if (string.IsNullOrEmpty(CurrentState.ActivePeacefulEventName))
        {
          var lanternType = TryGetCachedType(runtime, ref _lanternNightTypeMT, "Terraria.GameContent.Events.LanternNight");
          if (lanternType != null)
          {
            bool manualLanterns = lanternType.StaticFields.FirstOrDefault(f => f.Name == "ManualLanterns")?.Read<bool>(appDomain) ?? false;
            bool genuineLanterns = lanternType.StaticFields.FirstOrDefault(f => f.Name == "GenuineLanterns")?.Read<bool>(appDomain) ?? false;
            if (manualLanterns || genuineLanterns)
            {
              CurrentState.ActivePeacefulEventName = "Lantern Night";
              CurrentState.ActivePeacefulEventValue = "LanternNight";
            }
          }
        }

        // 5. Weather Events (Rain, Thunderstorm, Boulder Rain, Sandstorm, Windy Day)
        bool meteorShower = mainType.StaticFields.FirstOrDefault(f => f.Name == "_canShowMeteorFall")?.Read<bool>(appDomain) ?? false;
        var sandstormType = TryGetCachedType(runtime, ref _sandstormTypeMT, "Terraria.GameContent.Events.Sandstorm");
        bool isSandstorm = false;
        if (sandstormType != null && !meteorShower)
        {
          isSandstorm = sandstormType.StaticFields.FirstOrDefault(f => f.Name == "Happening")?.Read<bool>(appDomain) ?? false;
        }

        bool hasSpecialSeed(string seedName)
        {
          return CurrentState.WorldSpecialSeeds.Any(seed => string.Equals(seed, seedName, StringComparison.OrdinalIgnoreCase));
        }

        if (meteorShower)
        {
          CurrentState.ActiveWeatherName = "Meteor Shower";
        }
        else
        {
          if (isSandstorm)
          {
            CurrentState.ActiveWeatherName = "Sandstorm";
          }
          else
          {
            bool isRaining = mainType.StaticFields.FirstOrDefault(f => f.Name == "raining")?.Read<bool>(appDomain) ?? false;
            float maxRaining = mainType.StaticFields.FirstOrDefault(f => f.Name == "maxRaining")?.Read<float>(appDomain) ?? 0f;
            float windSpeed = mainType.StaticFields.FirstOrDefault(f => f.Name == "windSpeedCurrent")?.Read<float>(appDomain) ?? 0f;
            bool isThunderstorm = isRaining && maxRaining > 0.6f && Math.Abs(windSpeed) > 0.4f;
            bool isBoulderRain = isThunderstorm &&
              hasSpecialSeed("Drunk") &&
              hasSpecialSeed("For The Worthy") &&
              !hasSpecialSeed("Remix");

            if (isBoulderRain)
            {
              CurrentState.ActiveWeatherName = "Boulder Rain";
            }
            else if (isThunderstorm)
            {
              CurrentState.ActiveWeatherName = "Thunderstorm";
            }
            else if (isRaining)
            {
              CurrentState.ActiveWeatherName = "Rain";
            }
            else if (Math.Abs(windSpeed) >= 0.4f)
            {
              CurrentState.ActiveWeatherName = "Windy Day";
            }
          }
        }

      }
      catch (Exception ex)
      {
        Logger.Warn($"Failed to scan bosses and events: {ex.Message}");
      }
    }

    private static bool IsKnownBossType(int type)
    {
      return type == 4 || type == 13 || type == 14 || type == 15 || type == 35 ||
              type == 50 || type == 113 || type == 125 || type == 126 || type == 127 ||
              type == 128 || type == 129 || type == 130 || type == 131 ||
              type == 134 || type == 222 || type == 245 || type == 246 || type == 247 || type == 248 ||
              type == 262 || type == 266 || type == 267 ||
              type == 325 || type == 327 || type == 344 || type == 345 || type == 346 ||
              type == 392 || type == 393 || type == 394 || type == 395 ||
              type == 370 || type == 396 || type == 397 || type == 398 || type == 439 ||
             type == 491 ||  // Flying Dutchman
             type == 551 || type == 657 || type == 668 || type == 636 ||
             // Old One's Army bosses — IDs verified from Terraria wiki
             type == 564 || type == 565 ||  // Dark Mage Tier 1 & Tier 3
             type == 576;                   // Ogre
    }

    private string GetNpcTypeName(ClrRuntime runtime, ClrAppDomain appDomain, ClrObject npcObj, int type)
    {
      // Only try GivenOrTypeName for actual custom/renamed NPCs — for known types,
      // use the switch directly so we never get empty strings from unset fields.
      if (!IsKnownBossType(type))
      {
        try
        {
          var field = npcObj.Type?.Fields.FirstOrDefault(f => f.Name == "GivenOrTypeName");
          if (field != null)
          {
            ulong addr = field.Read<ulong>(npcObj.Address, false);
            if (addr != 0)
            {
              var strObj = runtime.Heap.GetObject(addr);
              string? s = strObj.IsValid ? strObj.AsString() : null;
              if (!string.IsNullOrWhiteSpace(s)) return s;
            }
          }
        }
        catch { }
      }

      return type switch
      {
        4 => "Eye of Cthulhu",
        50 => "King Slime",
        13 => "Eater of Worlds",
        14 => "Eater of Worlds",
        15 => "Eater of Worlds",
        266 => "Brain of Cthulhu",
        267 => "Creeper",
        222 => "Queen Bee",
        35 => "Skeletron",
        113 => "Wall of Flesh",
        657 => "Queen Slime",
        125 => "Retinazer",
        126 => "Spazmatism",
        134 => "The Destroyer",
        127 => "Skeletron Prime",
        128 => "Prime Cannon",
        129 => "Prime Saw",
        130 => "Prime Vice",
        131 => "Prime Laser",
        262 => "Plantera",
        325 => "Mourning Wood",
        327 => "Pumpking",
        344 => "Everscream",
        345 => "Ice Queen",
        346 => "Santa-NK1",
        392 => "Martian Saucer",
        393 => "Martian Saucer",
        394 => "Martian Saucer",
        395 => "Martian Saucer",
        245 => "Golem",
        246 => "Golem",
        247 => "Golem",
        248 => "Golem",
        370 => "Duke Fishron",
        439 => "Lunatic Cultist",
        396 => "Moon Lord",
        397 => "Moon Lord",
        398 => "Moon Lord",
        // Pillars — corrected IDs per Terraria wiki:
        // 493=LunarTowerStardust, 517=LunarTowerSolar, 507=LunarTowerNebula, 422=LunarTowerVortex
        493 => "Stardust Pillar",
        517 => "Solar Pillar",
        507 => "Nebula Pillar",
        422 => "Vortex Pillar",
        668 => "Deerclops",
        551 => "Betsy",
        636 => "Empress of Light",
        // Old One's Army bosses — IDs verified from Terraria wiki
        564 => "Dark Mage",
        565 => "Dark Mage",
        576 => "Ogre",
        491 => "Flying Dutchman",
        _ => $"Boss ({type})"
      };
    }

    private int GetPillarShield(ClrType mainType, ClrAppDomain appDomain, int pillarType)
    {
      // Pillar NPC IDs (confirmed from Terraria wiki):
      // 493=LunarTowerStardust, 517=LunarTowerSolar, 507=LunarTowerNebula, 422=LunarTowerVortex
      string fieldName = pillarType switch
      {
        493 => "ShieldStrengthTowerStardust",
        517 => "ShieldStrengthTowerSolar",
        507 => "ShieldStrengthTowerNebula",
        422 => "ShieldStrengthTowerVortex",
        _ => ""
      };

      if (string.IsNullOrEmpty(fieldName)) return 0;
      var field = mainType.StaticFields.FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
      if (field == null)
      {
        string available = string.Join(", ", mainType.StaticFields
          .Where(f => f.Name.Contains("shield", StringComparison.OrdinalIgnoreCase))
          .Select(f => f.Name));
        Logger.DebugThrottled(
          $"pillar-shield-field-miss:{pillarType}",
          $"[Pillar Shield] missing field '{fieldName}' for type={pillarType}; available shield fields=[{available}]");
        return 0;
      }

      try { return field.Read<int>(appDomain); } catch { }
      try { return field.Read<short>(appDomain); } catch { }
      try { return field.Read<byte>(appDomain); } catch { }
      return 0;
    }

    private int GetPillarMaxShield(ClrType mainType, ClrAppDomain appDomain)
    {
      var field = mainType.StaticFields.FirstOrDefault(f => string.Equals(f.Name, "ShieldStrengthTowerMax", StringComparison.OrdinalIgnoreCase))
        ?? mainType.StaticFields.FirstOrDefault(f => string.Equals(f.Name, "LunarShieldPowerMax", StringComparison.OrdinalIgnoreCase));
      int val = 0;
      if (field != null)
      {
        try { val = field.Read<int>(appDomain); } catch { }
        if (val == 0) try { val = field.Read<short>(appDomain); } catch { }
        if (val == 0) try { val = field.Read<byte>(appDomain); } catch { }
      }
      else
      {
        string available = string.Join(", ", mainType.StaticFields
          .Where(f => f.Name.Contains("shield", StringComparison.OrdinalIgnoreCase))
          .Select(f => f.Name));
        Logger.DebugThrottled(
          "pillar-shield-max-field-miss",
          $"[Pillar Shield] missing max field 'ShieldStrengthTowerMax'; available shield fields=[{available}]");
      }
      return val > 0 ? val : 100;
    }
  }
}
