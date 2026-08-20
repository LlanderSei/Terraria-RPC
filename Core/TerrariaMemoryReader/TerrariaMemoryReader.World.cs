using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaRPC.Core
{
  public partial class TerrariaMemoryReader
  {
    private void ReadWorldState(ClrRuntime runtime, ClrAppDomain appDomain, ClrType mainType, ClrType? worldGenType)
    {
        // Read worldName
        var worldNameField = mainType.StaticFields.FirstOrDefault(f => f.Name == "worldName");
        if (worldNameField != null)
        {
          ulong addr = worldNameField.Read<ulong>(appDomain);
          if (addr != 0)
          {
            ClrObject worldNameObj = runtime.Heap.GetObject(addr);
            if (worldNameObj.IsValid)
              CurrentState.WorldName = worldNameObj.AsString() ?? "";
          }
        }

        // Read World Size
        var maxTilesXField = mainType.StaticFields.FirstOrDefault(f => f.Name == "maxTilesX");
        if (maxTilesXField != null)
        {
          int maxTiles = maxTilesXField.Read<int>(appDomain);
          if (maxTiles <= 4200) CurrentState.WorldSize = "Small";
          else if (maxTiles <= 6400) CurrentState.WorldSize = "Medium";
          else CurrentState.WorldSize = "Large";
        }

        // Read Hardmode
        var hardModeField = mainType.StaticFields.FirstOrDefault(f => f.Name == "hardMode");
        if (hardModeField != null)
          CurrentState.WorldIsHardmode = hardModeField.Read<bool>(appDomain);

        // Read Evil Type (WorldGen.crimson)
        if (worldGenType != null)
        {
          var crimsonField = worldGenType.StaticFields.FirstOrDefault(f => f.Name == "crimson");
          if (crimsonField != null)
          {
            bool isCrimson = crimsonField.Read<bool>(appDomain);
            CurrentState.WorldEvil = isCrimson ? "Crimson" : "Corruption";
          }
        }

        // Read Special Seeds from Main (so it works in Multiplayer too!)
        var specialSeeds = new System.Collections.Generic.List<string>();
        string NormalizeSecretSeedName(string seedName)
        {
          string normalized = seedName.Trim();
          return normalized.ToLowerInvariant() switch
          {
            "calm before the storm" => "Calm before the storm",
            "electric boogaloo" => "Electric boogaloo",
            _ => normalized
          };
        }

        void CheckSpecialSeed(string fieldName, string seedName)
        {
          var field = mainType.StaticFields.FirstOrDefault(f => f.Name == fieldName);
          if (field != null && field.Read<bool>(appDomain))
            specialSeeds.Add(seedName);
        }

        CheckSpecialSeed("drunkWorld", "Drunk");
        CheckSpecialSeed("notTheBeesWorld", "Not The Bees");
        CheckSpecialSeed("getGoodWorld", "For The Worthy");
        CheckSpecialSeed("tenthAnniversaryWorld", "Celebrationmk10");
        CheckSpecialSeed("dontStarveWorld", "The Constant");
        CheckSpecialSeed("remixWorld", "Remix");
        CheckSpecialSeed("noTrapsWorld", "No Traps");
        CheckSpecialSeed("zenithWorld", "Zenith");
        CheckSpecialSeed("skyblockWorld", "Skyblock");
        CurrentState.WorldSpecialSeeds = specialSeeds.ToArray();

        // Read ActiveWorldFileData for Seed, Difficulty, Secret Seeds, and Skyblock
        var activeWorldField = mainType.StaticFields.FirstOrDefault(f => f.Name == "ActiveWorldFileData");
        if (activeWorldField != null)
        {
          ulong activeWorldAddr = activeWorldField.Read<ulong>(appDomain);
          if (activeWorldAddr != 0)
          {
            ClrObject activeWorldObj = runtime.Heap.GetObject(activeWorldAddr);
            if (activeWorldObj.IsValid)
            {
              // Difficulty — read always (valid in SP and MP)
              var gameModeField = activeWorldObj.Type.Fields.FirstOrDefault(f => f.Name == "GameMode");
              if (gameModeField != null)
              {
                int gameMode = gameModeField.Read<int>(activeWorldAddr, false);
                CurrentState.WorldDifficulty = gameMode switch
                {
                  0 => "Classic",
                  1 => "Expert",
                  2 => "Master",
                  3 => "Journey",
                  _ => "Unknown"
                };
              }

              // Seed & Secret Seeds — only valid in SP (stale in MP client)
              if (CurrentState.NetMode != 1)
              {
                var seedTextField = activeWorldObj.Type.Fields.FirstOrDefault(f => f.Name == "_seedText");
                if (seedTextField != null)
                {
                  ulong seedTextAddr = seedTextField.Read<ulong>(activeWorldAddr, false);
                  if (seedTextAddr != 0)
                  {
                    ClrObject seedTextObj = runtime.Heap.GetObject(seedTextAddr);
                    string rawSeed = seedTextObj.AsString() ?? "";

                    if (rawSeed.Contains('|'))
                    {
                      var parts = rawSeed.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(NormalizeSecretSeedName)
                        .ToArray();
                      if (parts.Length > 0)
                      {
                        CurrentState.WorldSeed = parts.Last();
                        CurrentState.WorldSecretSeeds = parts.Take(parts.Length - 1).ToArray();
                      }
                      else
                      {
                        CurrentState.WorldSeed = "";
                        CurrentState.WorldSecretSeeds = Array.Empty<string>();
                      }
                    }
                    else
                    {
                      CurrentState.WorldSeed = NormalizeSecretSeedName(rawSeed);
                      CurrentState.WorldSecretSeeds = Array.Empty<string>();
                    }
                  }
                }
              }
              else
              {
                // Multiplayer: clear stale SP seed data
                CurrentState.WorldSeed = "";
                CurrentState.WorldSecretSeeds = Array.Empty<string>();
              }
            }
          }
        }

        // Adjust displayed special seeds: if Zenith is active, it forces all other seeds.
        // Collapse the list to just "Zenith" plus any extras that aren't implied (e.g. Skyblock).
        if (CurrentState.WorldSpecialSeeds.Contains("Zenith"))
        {
          var zenithImplied = new HashSet<string> { "Drunk", "Not The Bees", "For The Worthy", "Celebrationmk10", "The Constant", "Remix", "No Traps" };
          CurrentState.WorldSpecialSeeds = CurrentState.WorldSpecialSeeds
            .Where(s => !zenithImplied.Contains(s))
            .ToArray();
        }

        // Save raw difficulty before any FTW escalation.
        CurrentState.WorldRawDifficulty = CurrentState.WorldDifficulty;

        // The game always stores the raw base GameMode in memory (both SP and MP).
        // Apply FTW / Zenith escalation manually.
        bool hasFtw = CurrentState.WorldSpecialSeeds.Contains("For The Worthy")
          || CurrentState.WorldSpecialSeeds.Contains("Zenith");

        if (hasFtw)
        {
          CurrentState.WorldDifficulty = CurrentState.WorldDifficulty switch
          {
            "Journey"  => "Classic",
            "Classic"  => "Expert",
            "Expert"   => "Master",
            "Master"   => "Legendary",
            _          => CurrentState.WorldDifficulty
          };
        }
    }
  }
}
