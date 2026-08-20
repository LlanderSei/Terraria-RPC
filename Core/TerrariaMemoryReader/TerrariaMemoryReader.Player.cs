using System;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaRPC.Core
{
  public partial class TerrariaMemoryReader
  {
    private void ReadPlayerState(ClrRuntime runtime, ClrAppDomain appDomain, ClrType mainType, ClrType? playerType)
    {
        // Extract Held Item + Biome from Main.player[Main.myPlayer]
        var myPlayer = mainType.StaticFields.FirstOrDefault(f => f.Name == "myPlayer")?.Read<int>(appDomain) ?? -1;
        ulong playersAddr = mainType.StaticFields.FirstOrDefault(f => f.Name == "player")?.Read<ulong>(appDomain) ?? 0;
        ulong playerAddr = 0; // hoisted so biome block can use it
        if (myPlayer >= 0 && playersAddr != 0)
        {
          var playerArrayObj = runtime.Heap.GetObject(playersAddr);
          if (playerArrayObj.IsValid && playerArrayObj.IsArray)
          {
            playerAddr = playerArrayObj.AsArray().GetObjectValue(myPlayer).Address;
            if (playerAddr != 0)
            {
              var playerObj = runtime.Heap.GetObject(playerAddr);
              if (playerObj.IsValid)
              {
                CurrentState.PlayerHp = playerObj.ReadField<int>("statLife");
                CurrentState.PlayerMaxHp = playerObj.ReadField<int>("statLifeMax2");
                CurrentState.PlayerMp = playerObj.ReadField<int>("statMana");
                CurrentState.PlayerMaxMp = playerObj.ReadField<int>("statManaMax2");
                CurrentState.PlayerDef = playerObj.ReadField<int>("statDefense");

                var itemObj = playerObj.ReadObjectField("lastVisualizedSelectedItem");
                if (itemObj.IsValid)
                {
                  int itemType = itemObj.ReadField<int>("type");
                  if (itemType > 0)
                  {
                    byte itemPrefix = itemObj.ReadField<byte>("prefix");
                    string itemSignature = $"{itemType}:{itemPrefix}";

                    if (itemSignature != _lastAtkItemSignature)
                    {
                      _lastAtkItemSignature = itemSignature;
                    }

                    var langType = TryGetCachedType(runtime, ref _langTypeMT, "Terraria.Lang");

                    if (langType != null)
                    {
                      ulong cacheAddr = langType.StaticFields.FirstOrDefault(f => f.Name == "_itemNameCache")?.Read<ulong>(appDomain) ?? 0;
                      if (cacheAddr != 0)
                      {
                        var cacheArrayObj = runtime.Heap.GetObject(cacheAddr);
                        if (cacheArrayObj.IsValid && cacheArrayObj.IsArray)
                        {
                          var locTextObj = cacheArrayObj.AsArray().GetObjectValue(itemType);
                          if (locTextObj.IsValid)
                          {
                            CurrentState.PlayerItemHeld = locTextObj.ReadStringField("<EnglishValue>k__BackingField") ?? "";
                          }
                        }
                      }

                      if (itemPrefix > 0)
                      {
                        ulong prefixCacheAddr = langType.StaticFields.FirstOrDefault(f => f.Name == "prefix")?.Read<ulong>(appDomain) ?? 0;
                        if (prefixCacheAddr != 0)
                        {
                          var prefixArrayObj = runtime.Heap.GetObject(prefixCacheAddr);
                          if (prefixArrayObj.IsValid && prefixArrayObj.IsArray)
                          {
                            var locTextObj = prefixArrayObj.AsArray().GetObjectValue(itemPrefix);
                            if (locTextObj.IsValid)
                            {
                              CurrentState.PlayerItemPrefix = locTextObj.ReadStringField("<EnglishValue>k__BackingField") ?? "";
                            }
                          }
                        }
                      }
                      else
                      {
                        CurrentState.PlayerItemPrefix = "";
                      }
                    }
                  }
                  else
                  {
                    _lastAtkItemSignature = "";
                    CurrentState.PlayerItemHeld = "";
                    CurrentState.PlayerItemPrefix = "";
                  }

                  // -- Weapon Damage & DPS ------------------------------------------------
                  int weaponDamage = 0;
                  if (itemObj.IsValid)
                  {
                    int baseDamage = itemObj.ReadField<int>("damage");
                    float multiplier = 1f;

                    if (itemObj.ReadField<bool>("melee"))
                      multiplier = playerObj.ReadField<float>("meleeDamage");
                    else if (itemObj.ReadField<bool>("magic"))
                      multiplier = playerObj.ReadField<float>("magicDamage");
                    else if (itemObj.ReadField<bool>("ranged"))
                      multiplier = playerObj.ReadField<float>("rangedDamage");
                    else if (itemObj.ReadField<bool>("summon"))
                      multiplier = playerObj.ReadField<float>("minionDamage");

                    weaponDamage = (int)(baseDamage * multiplier);
                  }
                  
                  int currentDps = 0;
                  int dpsDamage = playerObj.ReadField<int>("dpsDamage");
                  if (dpsDamage > 0)
                  {
                    var startObj = playerObj.ReadValueTypeField("dpsStart");
                    var endObj = playerObj.ReadValueTypeField("dpsEnd");
                    var lastHitObj = playerObj.ReadValueTypeField("dpsLastHit");
                    if (startObj.IsValid && endObj.IsValid && lastHitObj.IsValid)
                    {
                      try
                      {
                        long lastHitTicks = (long)lastHitObj.ReadField<ulong>("dateData");
                        long startTicks = (long)startObj.ReadField<ulong>("dateData");
                        long endTicks = (long)endObj.ReadField<ulong>("dateData");

                        DateTime lastHitTime = DateTime.FromBinary(lastHitTicks);
                        DateTime startTime = DateTime.FromBinary(startTicks);
                        DateTime endTime = DateTime.FromBinary(endTicks);

                        // Ignore impossible timer windows so a garbage read cannot poison the session max.
                        if (startTime <= endTime && lastHitTime <= DateTime.Now.AddSeconds(5) && (DateTime.Now - lastHitTime).TotalSeconds <= 2.0)
                        {
                          double seconds = (endTime - startTime).TotalSeconds;
                          if (seconds > 0.0)
                            currentDps = (int)(dpsDamage / Math.Max(1.0, seconds));
                        }
                      }
                      catch { }
                    }
                  }

                  int currentMaxAtk = Math.Max(weaponDamage, currentDps);
                  if (currentMaxAtk > CurrentState.HighestRecordedAtk)
                  {
                    CurrentState.HighestRecordedAtk = currentMaxAtk;
                  }

                  if (CurrentState.HighestRecordedAtk > 0)
                  {
                    CurrentState.PlayerAtk = CurrentState.HighestRecordedAtk.ToString();
                  }

                  if (currentMaxAtk > 100000)
                  {
                    int loggedItemType = itemObj.ReadField<int>("type");
                    int loggedBaseDamage = itemObj.ReadField<int>("damage");
                    Logger.Warn($"Suspicious attack sample: item={_lastAtkItemSignature} itemType={loggedItemType} base={loggedBaseDamage} weapon={weaponDamage} dpsDamage={dpsDamage} currentDps={currentDps} max={currentMaxAtk}");
                  }
                }
              }
            }
          }
        }

        // -- Biome / Depth Detection ----------------------------------------------
        // Depth is computed from player tile-Y vs. world boundaries (reliable).
        // Horizontal biome type is read from zone1/zone3 BitsByte flags.
        byte ReadZoneByte(string fieldName)
        {
          var zf = playerType?.Fields.FirstOrDefault(x => x.Name == fieldName);
          if (zf == null) return 0;
          var vf = zf.Type?.Fields.FirstOrDefault(x => x.Name == "value");
          if (vf == null) return 0;
          ulong sa = (ulong)((long)playerAddr + zf.Offset + IntPtr.Size);
          return vf.Read<byte>(sa, interior: true);
        }

        if (playerType != null && playerAddr != 0)
        {
          // -- Horizontal biome type from zone bits (zone1 - zone5) --------------
          byte z1 = ReadZoneByte("zone1");
          byte z2 = ReadZoneByte("zone2");
          byte z3 = ReadZoneByte("zone3");
          byte z4 = ReadZoneByte("zone4");
          byte z5 = ReadZoneByte("zone5");

          // zone3 Depth and Ocean
          bool posIsSpace      = (z3 & 0b00000001) != 0; // SkyHeight
          // OverworldHeight is bit 1
          bool posIsUnderground= (z3 & 0b00000100) != 0; // DirtLayerHeight
          bool posIsCavern     = (z3 & 0b00001000) != 0; // RockLayerHeight
          bool posIsUnderworld = (z3 & 0b00010000) != 0; // UnderworldHeight
          bool posIsOcean      = (z3 & 0b00100000) != 0; // Beach

          // Biome types
          bool inDungeon    = (z1 & 0b00000001) != 0;
          bool inCorruption = (z1 & 0b00000010) != 0;
          bool inHallow     = (z1 & 0b00000100) != 0;
          bool inMeteor     = (z1 & 0b00001000) != 0;
          bool inJungle     = (z1 & 0b00010000) != 0;
          bool inSnow       = (z1 & 0b00100000) != 0;
          bool inCrimson    = (z1 & 0b01000000) != 0;
          
          bool inDesert     = (z2 & 0b00100000) != 0;
          bool inGlowshroom = (z2 & 0b01000000) != 0;
          bool inUnderDesert= (z2 & 0b10000000) != 0;
          
          bool inGraveyard  = (z4 & 0b01000000) != 0;
          
          bool inShimmer    = (z5 & 0b00000001) != 0;

          // -- Compose final biome string -----------------------------------------
          if (posIsUnderworld)
          {
            // Underworld always wins regardless of nearby biomes
            CurrentState.Biome = "Underworld";
          }
          else if (posIsSpace)
          {
            CurrentState.Biome = "Space";
          }
          else if (posIsOcean)
          {
            // Ocean: evil variants take priority, otherwise plain Ocean
            if      (inCorruption) CurrentState.Biome = "Corrupt Ocean";
            else if (inCrimson)    CurrentState.Biome = "Crimson Ocean";
            else if (inHallow)     CurrentState.Biome = "Hallowed Ocean";
            else                   CurrentState.Biome = "Ocean";
          }
          else
          {
            // Depth prefix for underground/cavern zones
            string depthPrefix = posIsCavern      ? "Cavern"
                               : posIsUnderground ? "Underground"
                               : "";  // Surface/Sky ? no prefix

            string biomeName = "Forest";
            if      (inDungeon)     biomeName = "Dungeon";
            else if (inShimmer)     biomeName = "Aether";
            else if (inMeteor)      biomeName = "Meteorite";
            else if (inGlowshroom)  biomeName = "Glowing Mushroom";
            else if (inGraveyard)   biomeName = "Graveyard";
            else if (inJungle)      biomeName = "Jungle";
            else if (inCorruption)  biomeName = "Corruption";
            else if (inCrimson)     biomeName = "Crimson";
            else if (inHallow)      biomeName = "Hallow";
            else if (inSnow)        biomeName = "Snow";
            else if (inUnderDesert) biomeName = "Desert";
            else if (inDesert)      biomeName = "Desert";
            else                    biomeName = "Forest";
            if (biomeName == "Forest" && !string.IsNullOrEmpty(depthPrefix))
            {
              CurrentState.Biome = depthPrefix; // Just "Underground" or "Cavern"
            }
            else
            {
              CurrentState.Biome = string.IsNullOrEmpty(depthPrefix)
                ? biomeName
                : $"{depthPrefix} {biomeName}";
            }
          }
        }

    }
  }
}
