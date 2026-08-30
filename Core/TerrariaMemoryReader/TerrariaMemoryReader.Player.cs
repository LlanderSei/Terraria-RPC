using System;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaRPC.Core
{
  public partial class TerrariaMemoryReader
  {
    private void ReadPlayerState(ClrRuntime runtime, ClrAppDomain appDomain, ClrType mainType, ClrType? playerType)
    {
      CurrentState.TorchGodActive = false;
      CurrentState.PlayerHasPosition = false;
      CurrentState.PlayerIsSpectating = false;
      CurrentState.SpectatedName = "";
      CurrentState.RespawnTimer = 0;

      var myPlayer = mainType.StaticFields.FirstOrDefault(f => f.Name == "myPlayer")?.Read<int>(appDomain) ?? -1;
      CurrentState.PlayerIndex = myPlayer;
      ulong playersAddr = mainType.StaticFields.FirstOrDefault(f => f.Name == "player")?.Read<ulong>(appDomain) ?? 0;
      ulong playerAddr = 0;

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
              CurrentState.TorchGodActive = playerObj.ReadField<bool>("happyFunTorchTime");
              int spectatingIndex = playerObj.ReadField<int>("spectating");
              bool isDead = playerObj.ReadField<bool>("dead");
              CurrentState.RespawnTimer = playerObj.ReadField<int>("respawnTimer");
              CurrentState.PlayerIsSpectating = spectatingIndex >= 0 || isDead || CurrentState.RespawnTimer > 0;

              var positionField = playerObj.Type?.Fields.FirstOrDefault(f => f.Name == "position");
              var positionType = positionField?.Type;
              var positionXField = positionType?.Fields.FirstOrDefault(f => f.Name == "X");
              var positionYField = positionType?.Fields.FirstOrDefault(f => f.Name == "Y");
              if (positionField != null && positionXField != null && positionYField != null)
              {
                ulong positionAddr = (ulong)((long)playerAddr + positionField.Offset + IntPtr.Size);
                float posX = positionXField.Read<float>(positionAddr, interior: true);
                float posY = positionYField.Read<float>(positionAddr, interior: true);
                int width = playerObj.ReadField<int>("width");
                int height = playerObj.ReadField<int>("height");
                CurrentState.PlayerCenterX = posX + width * 0.5f;
                CurrentState.PlayerCenterY = posY + height * 0.5f;
                CurrentState.PlayerHasPosition = true;
              }

              if (spectatingIndex >= 0 && playersAddr != 0)
              {
                var spectatedName = "";
                try
                {
                  var spectatedArrayObj = runtime.Heap.GetObject(playersAddr);
                  if (spectatedArrayObj.IsValid && spectatedArrayObj.IsArray)
                  {
                    int playerCount = spectatedArrayObj.AsArray().Length;
                    if (spectatingIndex < playerCount)
                    {
                      var spectatedObj = spectatedArrayObj.AsArray().GetObjectValue(spectatingIndex);
                      if (spectatedObj.IsValid)
                      {
                        spectatedName = spectatedObj.ReadStringField("name") ?? "";
                      }
                    }
                  }
                }
                catch
                {
                  spectatedName = "";
                }

                CurrentState.SpectatedName = spectatedName;
              }

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

                int currentWeaponDamage = 0;
                bool hasDamageableWeapon = false;
                int baseDamage = itemObj.ReadField<int>("damage");
                hasDamageableWeapon = baseDamage > 0;

                if (hasDamageableWeapon)
                {
                  float multiplier = 1f;
                  if (itemObj.ReadField<bool>("melee"))
                    multiplier = playerObj.ReadField<float>("meleeDamage");
                  else if (itemObj.ReadField<bool>("magic"))
                    multiplier = playerObj.ReadField<float>("magicDamage");
                  else if (itemObj.ReadField<bool>("ranged"))
                    multiplier = playerObj.ReadField<float>("rangedDamage");
                  else if (itemObj.ReadField<bool>("summon"))
                    multiplier = playerObj.ReadField<float>("minionDamage");

                  currentWeaponDamage = (int)(baseDamage * multiplier);
                }

                if (hasDamageableWeapon && currentWeaponDamage > 0)
                {
                  CurrentState.PlayerDynamicWeaponDmg = currentWeaponDamage;
                  if (currentWeaponDamage > CurrentState.PlayerHighestWeaponDmg)
                    CurrentState.PlayerHighestWeaponDmg = currentWeaponDamage;
                }

                int currentDps = 0;
                int dpsDamage = playerObj.ReadField<int>("dpsDamage");
                if (dpsDamage > 0)
                {
                  try
                  {
                    DateTime startTime = playerObj.ReadField<DateTime>("dpsStart");
                    DateTime endTime = playerObj.ReadField<DateTime>("dpsEnd");
                    DateTime lastHitTime = playerObj.ReadField<DateTime>("dpsLastHit");

                    if (startTime <= endTime && lastHitTime <= DateTime.Now.AddSeconds(5) && (DateTime.Now - lastHitTime).TotalSeconds <= 2.0)
                    {
                      double seconds = (endTime - startTime).TotalSeconds;
                      if (seconds >= 0.25)
                        currentDps = (int)(dpsDamage / seconds);
                    }
                  }
                  catch { }
                }

                CurrentState.PlayerDynamicDps = currentDps;
                if (currentDps > CurrentState.PlayerHighestDps)
                  CurrentState.PlayerHighestDps = currentDps;

                CurrentState.HighestRecordedAtk = CurrentState.PlayerHighestWeaponDmg;
                CurrentState.PlayerAtk = CurrentState.PlayerHighestWeaponDmg > 0
                  ? CurrentState.PlayerHighestWeaponDmg.ToString()
                  : "N/A";

                if (currentWeaponDamage > 100000 || currentDps > 100000)
                {
                  int loggedItemType = itemObj.ReadField<int>("type");
                  int loggedBaseDamage = itemObj.ReadField<int>("damage");
                  Logger.Warn($"Suspicious attack sample: item={_lastAtkItemSignature} itemType={loggedItemType} base={loggedBaseDamage} weapon={currentWeaponDamage} dpsDamage={dpsDamage} currentDps={currentDps} highestWeapon={CurrentState.PlayerHighestWeaponDmg} highestDps={CurrentState.PlayerHighestDps}");
                }
              }
            }
          }
        }

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
          byte z1 = ReadZoneByte("zone1");
          byte z2 = ReadZoneByte("zone2");
          byte z3 = ReadZoneByte("zone3");
          byte z4 = ReadZoneByte("zone4");
          byte z5 = ReadZoneByte("zone5");

          bool posIsSpace = (z3 & 0b00000001) != 0;
          bool posIsUnderground = (z3 & 0b00000100) != 0;
          bool posIsCavern = (z3 & 0b00001000) != 0;
          bool posIsUnderworld = (z3 & 0b00010000) != 0;
          bool posIsOcean = (z3 & 0b00100000) != 0;

          bool inDungeon = (z1 & 0b00000001) != 0;
          bool inCorruption = (z1 & 0b00000010) != 0;
          bool inHallow = (z1 & 0b00000100) != 0;
          bool inMeteor = (z1 & 0b00001000) != 0;
          bool inJungle = (z1 & 0b00010000) != 0;
          bool inSnow = (z1 & 0b00100000) != 0;
          bool inCrimson = (z1 & 0b01000000) != 0;

          bool inDesert = (z2 & 0b00100000) != 0;
          bool inGlowshroom = (z2 & 0b01000000) != 0;
          bool inUnderDesert = (z2 & 0b10000000) != 0;

          bool inGraveyard = (z4 & 0b01000000) != 0;
          bool inShimmer = (z5 & 0b00000001) != 0;

          if (posIsUnderworld)
          {
            CurrentState.Biome = "Underworld";
          }
          else if (posIsSpace)
          {
            CurrentState.Biome = "Space";
          }
          else if (posIsOcean)
          {
            if (inCorruption) CurrentState.Biome = "Corrupt Ocean";
            else if (inCrimson) CurrentState.Biome = "Crimson Ocean";
            else if (inHallow) CurrentState.Biome = "Hallowed Ocean";
            else CurrentState.Biome = "Ocean";
          }
          else
          {
            string depthPrefix = posIsCavern ? "Cavern" : posIsUnderground ? "Underground" : "";

            string biomeName = "Forest";
            if (inDungeon) biomeName = "Dungeon";
            else if (inShimmer) biomeName = "Aether";
            else if (inMeteor) biomeName = "Meteorite";
            else if (inGlowshroom) biomeName = "Glowing Mushroom";
            else if (inGraveyard) biomeName = "Graveyard";
            else if (inJungle) biomeName = "Jungle";
            else if (inCorruption) biomeName = "Corruption";
            else if (inCrimson) biomeName = "Crimson";
            else if (inHallow) biomeName = "Hallow";
            else if (inSnow) biomeName = "Snow";
            else if (inUnderDesert) biomeName = "Desert";
            else if (inDesert) biomeName = "Desert";

            if (biomeName == "Forest" && !string.IsNullOrEmpty(depthPrefix))
            {
              CurrentState.Biome = depthPrefix;
            }
            else
            {
              CurrentState.Biome = string.IsNullOrEmpty(depthPrefix) ? biomeName : $"{depthPrefix} {biomeName}";
            }
          }
        }
      }
    }
  }
}
