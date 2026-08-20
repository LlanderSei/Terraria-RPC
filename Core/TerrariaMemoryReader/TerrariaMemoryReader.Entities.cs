using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaRPC.Core
{
  public partial class TerrariaMemoryReader
  {
    private void ScanBossesAndEvents(ClrRuntime runtime, ClrAppDomain appDomain, ClrType mainType)
    {
      // Reset active entity states before scanning
      CurrentState.ActiveBossName = "";
      CurrentState.ActiveBossHp = 0;
      CurrentState.ActiveBossMaxHp = 0;

      CurrentState.ActiveEventName = "";
      CurrentState.ActiveEventProgress = -1;
      CurrentState.ActiveEventWaveNum = -1;

      CurrentState.ActiveNonProgressiveEventName = "";
      CurrentState.ActivePeacefulEventName = "";
      CurrentState.ActiveWeatherName = "";

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
              string bestBossName = "";
              int bestBossHp = 0;
              int bestBossMaxHp = 0;
              int highestMaxHp = 0;

              // The Twins tracking: accumulate both eyes' HP for combined display
              int twinsLife = 0, twinsLifeMax = 0, twinsCount = 0;
              // Brain of Cthulhu tracking: combine the Brain and Creepers into one boss total
              int bocLife = 0, bocLifeMax = 0, bocCount = 0;
              // Eater of Worlds tracking: combine all active worm segments into one boss total
              int eowLife = 0, eowLifeMax = 0, eowCount = 0;
              // Skeletron Prime tracking: combine the head and all limbs.
              int primeLife = 0, primeLifeMax = 0, primeCount = 0;
              // Golem tracking: combine the body, head, and fists.
              int golemLife = 0, golemLifeMax = 0, golemCount = 0;
              // Moon Lord tracking: combine the core, hands, and head.
              int moonLordLife = 0, moonLordLifeMax = 0, moonLordCount = 0;

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

                  // Skip dead/inactive NPC slots (life<=0 means the slot is empty or dead)
                  if (life <= 0) continue;

                  string typeName = GetNpcTypeName(runtime, appDomain, npcObj, type);
                  if (string.IsNullOrEmpty(typeName)) continue;

                  // Debug: log every NPC that passes the boss check so we can verify type IDs
                  Logger.Debug($"[BossFound] type={type} boss={isBoss} isPillar={isPillar} life={life}/{lifeMax} name={typeName}");

                  // The Twins: accumulate both Retinazer (125) and Spazmatism (126)
                  if (type == 125 || type == 126)
                  {
                    twinsLife += life;
                    twinsLifeMax += lifeMax;
                    twinsCount++;
                    continue; // handled after loop
                  }

                  // Brain of Cthulhu: aggregate the brain and all creepers.
                  if (type == 266 || type == 267)
                  {
                    bocLife += life;
                    bocLifeMax += lifeMax;
                    bocCount++;
                    continue;
                  }

                  // Eater of Worlds: each segment is a separate NPC, so sum the whole worm.
                  if (type == 13 || type == 14 || type == 15)
                  {
                    eowLife += life;
                    eowLifeMax += lifeMax;
                    eowCount++;
                    continue;
                  }

                  // Skeletron Prime: aggregate the head plus all attached limbs.
                  if (type == 127 || type == 128 || type == 129 || type == 130 || type == 131)
                  {
                    primeLife += life;
                    primeLifeMax += lifeMax;
                    primeCount++;
                    continue;
                  }

                  // Golem: aggregate the body, head, and both fists.
                  if (type == 245 || type == 246 || type == 247 || type == 248)
                  {
                    golemLife += life;
                    golemLifeMax += lifeMax;
                    golemCount++;
                    continue;
                  }

                  // Moon Lord: aggregate the core, head, and both hands.
                  if (type == 396 || type == 397 || type == 398)
                  {
                    moonLordLife += life;
                    moonLordLifeMax += lifeMax;
                    moonLordCount++;
                    continue;
                  }

                  if (isPillar)
                  {
                    int shield = GetPillarShield(mainType, appDomain, type);
                    int maxShield = GetPillarMaxShield(mainType, appDomain);

                    if (shield > 0)
                    {
                      bestBossName = typeName;
                      bestBossHp = shield;
                      bestBossMaxHp = maxShield > 0 ? maxShield : shield;
                      break;
                    }
                    else
                    {
                      bestBossName = typeName;
                      bestBossHp = life;
                      bestBossMaxHp = lifeMax;
                      break;
                    }
                  }

                  if (lifeMax > highestMaxHp)
                  {
                    highestMaxHp = lifeMax;
                    bestBossName = typeName;
                    bestBossHp = life;
                    bestBossMaxHp = lifeMax;
                  }
                }
              }

              // Resolve The Twins post-loop.
              if (twinsCount > 0)
              {
                _lastTwinsMaxHp = Math.Max(_lastTwinsMaxHp, twinsLifeMax);
                if (_lastTwinsMaxHp >= highestMaxHp)
                {
                  bestBossName = "The Twins";
                  bestBossHp = twinsLife;
                  bestBossMaxHp = _lastTwinsMaxHp;
                  highestMaxHp = _lastTwinsMaxHp;
                }
              }
              else
              {
                _lastTwinsMaxHp = 0;
              }

              // Resolve Brain of Cthulhu post-loop.
              if (bocCount > 0)
              {
                _lastBocMaxHp = Math.Max(_lastBocMaxHp, bocLifeMax);
                if (_lastBocMaxHp >= highestMaxHp)
                {
                  bestBossName = "Brain of Cthulhu";
                  bestBossHp = bocLife;
                  bestBossMaxHp = _lastBocMaxHp > 0 ? _lastBocMaxHp : bocLifeMax;
                  highestMaxHp = _lastBocMaxHp;
                }
              }
              else
              {
                _lastBocMaxHp = 0;
              }

              // Resolve Eater of Worlds post-loop.
              if (eowCount > 0)
              {
                _lastEowMaxHp = Math.Max(_lastEowMaxHp, eowLifeMax);
                if (_lastEowMaxHp >= highestMaxHp)
                {
                  bestBossName = "Eater of Worlds";
                  bestBossHp = eowLife;
                  bestBossMaxHp = _lastEowMaxHp > 0 ? _lastEowMaxHp : eowLifeMax;
                  highestMaxHp = _lastEowMaxHp;
                }
              }
              else
              {
                _lastEowMaxHp = 0;
              }

              // Resolve Skeletron Prime post-loop.
              if (primeCount > 0)
              {
                _lastPrimeMaxHp = Math.Max(_lastPrimeMaxHp, primeLifeMax);
                if (_lastPrimeMaxHp >= highestMaxHp)
                {
                  bestBossName = "Skeletron Prime";
                  bestBossHp = primeLife;
                  bestBossMaxHp = _lastPrimeMaxHp;
                  highestMaxHp = _lastPrimeMaxHp;
                }
              }
              else
              {
                _lastPrimeMaxHp = 0;
              }

              // Resolve Golem post-loop.
              if (golemCount > 0)
              {
                _lastGolemMaxHp = Math.Max(_lastGolemMaxHp, golemLifeMax);
                if (_lastGolemMaxHp >= highestMaxHp)
                {
                  bestBossName = "Golem";
                  bestBossHp = golemLife;
                  bestBossMaxHp = _lastGolemMaxHp;
                  highestMaxHp = _lastGolemMaxHp;
                }
              }
              else
              {
                _lastGolemMaxHp = 0;
              }

              // Resolve Moon Lord post-loop.
              if (moonLordCount > 0)
              {
                _lastMoonLordMaxHp = Math.Max(_lastMoonLordMaxHp, moonLordLifeMax);
                if (_lastMoonLordMaxHp >= highestMaxHp)
                {
                  bestBossName = "Moon Lord";
                  bestBossHp = moonLordLife;
                  bestBossMaxHp = _lastMoonLordMaxHp;
                  highestMaxHp = _lastMoonLordMaxHp;
                }
              }
              else
              {
                _lastMoonLordMaxHp = 0;
              }

              if (!string.IsNullOrEmpty(bestBossName))
              {
                CurrentState.ActiveBossName = bestBossName;
                CurrentState.ActiveBossHp = bestBossHp;
                CurrentState.ActiveBossMaxHp = bestBossMaxHp;
              }
            }
          }
        }

        // 2. Progressive Events (Invasion, Slime Rain, Pumpkin/Frost Moon, Old One's Army)
        int invasionType = mainType.StaticFields.FirstOrDefault(f => f.Name == "invasionType")?.Read<int>(appDomain) ?? 0;
        int invasionProgress = mainType.StaticFields.FirstOrDefault(f => f.Name == "invasionProgress")?.Read<int>(appDomain) ?? 0;
        int invasionProgressMax = mainType.StaticFields.FirstOrDefault(f => f.Name == "invasionProgressMax")?.Read<int>(appDomain) ?? 0;
        // invasionWave is used by OOA and moon events for the current wave number
        int invasionWave = mainType.StaticFields.FirstOrDefault(f => f.Name == "invasionWave")?.Read<int>(appDomain) ?? 0;

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
            int dd2Wave = invasionProgressMax switch
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
            if (dd2Wave > 0)
            {
              _lastKnownOoaWave = dd2Wave;
            }
            else if (_lastKnownOoaWave > 0)
            {
              dd2Wave = _lastKnownOoaWave;
            }

            int pct = (intermissionTime > 0 || invasionProgressMax <= 1) ? 100 : (invasionProgressMax > 0 ? (int)(invasionProgress * 100.0 / invasionProgressMax) : -1);
            CurrentState.ActiveEventName = "Old One's Army";
            CurrentState.ActiveEventProgress = pct >= 0 ? Math.Min(100, pct) : -1;
            CurrentState.ActiveEventWaveNum = dd2Wave > 0 ? dd2Wave : 1;
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
            CurrentState.ActiveEventProgress = Math.Min(100, Math.Max(0, pct));
          }
          else if (mainType.StaticFields.FirstOrDefault(f => f.Name == "slimeRain")?.Read<bool>(appDomain) ?? false)
          {
            CurrentState.ActiveEventName = "Slime Rain";
            CurrentState.ActiveEventProgress = -1;
          }
          else if (mainType.StaticFields.FirstOrDefault(f => f.Name == "pumpkinMoon")?.Read<bool>(appDomain) ?? false)
          {
            CurrentState.ActiveEventName = "Pumpkin Moon";
            CurrentState.ActiveEventProgress = -1;
            CurrentState.ActiveEventWaveNum = invasionWave > 0 ? invasionWave : -1;
          }
          else if (mainType.StaticFields.FirstOrDefault(f => f.Name == "snowMoon")?.Read<bool>(appDomain) ?? false)
          {
            CurrentState.ActiveEventName = "Frost Moon";
            CurrentState.ActiveEventProgress = -1;
            CurrentState.ActiveEventWaveNum = invasionWave > 0 ? invasionWave : -1;
          }
        }

        // 3. Non-Progressive Events (Blood Moon, Solar Eclipse)
        if (mainType.StaticFields.FirstOrDefault(f => f.Name == "bloodMoon")?.Read<bool>(appDomain) ?? false)
        {
          CurrentState.ActiveNonProgressiveEventName = "Blood Moon";
        }
        else if (mainType.StaticFields.FirstOrDefault(f => f.Name == "eclipse")?.Read<bool>(appDomain) ?? false)
        {
          CurrentState.ActiveNonProgressiveEventName = "Solar Eclipse";
        }

        // 4. Peaceful Events (Party, Lantern Night)
        var partyType = TryGetCachedType(runtime, ref _birthdayPartyTypeMT, "Terraria.GameContent.Events.BirthdayParty");
        if (partyType != null)
        {
          bool manualParty = partyType.StaticFields.FirstOrDefault(f => f.Name == "ManualParty")?.Read<bool>(appDomain) ?? false;
          bool genuineParty = partyType.StaticFields.FirstOrDefault(f => f.Name == "GenuineParty")?.Read<bool>(appDomain) ?? false;
          if (manualParty || genuineParty) CurrentState.ActivePeacefulEventName = "Party is occurring.";
        }
        if (string.IsNullOrEmpty(CurrentState.ActivePeacefulEventName))
        {
          var lanternType = TryGetCachedType(runtime, ref _lanternNightTypeMT, "Terraria.GameContent.Events.LanternNight");
          if (lanternType != null)
          {
            bool manualLanterns = lanternType.StaticFields.FirstOrDefault(f => f.Name == "ManualLanterns")?.Read<bool>(appDomain) ?? false;
            bool genuineLanterns = lanternType.StaticFields.FirstOrDefault(f => f.Name == "GenuineLanterns")?.Read<bool>(appDomain) ?? false;
            if (manualLanterns || genuineLanterns) CurrentState.ActivePeacefulEventName = "Lantern Night is occurring";
          }
        }

        // 5. Weather Events (Rain, Thunderstorm, Sandstorm, Windy Day)
        var sandstormType = TryGetCachedType(runtime, ref _sandstormTypeMT, "Terraria.GameContent.Events.Sandstorm");
        bool isSandstorm = false;
        if (sandstormType != null)
        {
          isSandstorm = sandstormType.StaticFields.FirstOrDefault(f => f.Name == "Happening")?.Read<bool>(appDomain) ?? false;
        }

        if (isSandstorm)
        {
          CurrentState.ActiveWeatherName = "Sandstorm";
        }
        else
        {
          bool isRaining = mainType.StaticFields.FirstOrDefault(f => f.Name == "raining")?.Read<bool>(appDomain) ?? false;
          float maxRaining = mainType.StaticFields.FirstOrDefault(f => f.Name == "maxRaining")?.Read<float>(appDomain) ?? 0f;
          float windSpeed = mainType.StaticFields.FirstOrDefault(f => f.Name == "windSpeedCurrent")?.Read<float>(appDomain) ?? 0f;

          if (isRaining)
          {
            if (maxRaining > 0.6f && Math.Abs(windSpeed) > 0.4f)
              CurrentState.ActiveWeatherName = "Thunderstorm";
            else
              CurrentState.ActiveWeatherName = "Rain";
          }
          else if (Math.Abs(windSpeed) >= 0.4f)
          {
            CurrentState.ActiveWeatherName = "Windy Day";
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
          4   => "Eye of Cthulhu",
          50  => "King Slime",
          13  => "Eater of Worlds",
          14  => "Eater of Worlds",
          15  => "Eater of Worlds",
          266 => "Brain of Cthulhu",
          267 => "Creeper",
          222 => "Queen Bee",
          35  => "Skeletron",
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
        _   => $"Boss ({type})"
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
        _   => ""
      };

      if (string.IsNullOrEmpty(fieldName)) return 0;
      var field = mainType.StaticFields.FirstOrDefault(f => f.Name == fieldName);
      return field?.Read<int>(appDomain) ?? 0;
    }

    private int GetPillarMaxShield(ClrType mainType, ClrAppDomain appDomain)
    {
      var field = mainType.StaticFields.FirstOrDefault(f => f.Name == "ShieldStrengthTowerMax");
      int val = field?.Read<int>(appDomain) ?? 0;
      return val > 0 ? val : 100;
    }
  }
}
