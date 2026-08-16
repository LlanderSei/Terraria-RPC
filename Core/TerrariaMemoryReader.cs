using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaRPC.Core
{
  /// <summary>
  /// Observed Terraria menuMode values from live testing.
  /// </summary>
  public static class MenuModes
  {
    public const int MainMenuOrInGame = 0;  // 0 = Main Menu OR SP In-Game (disambiguate via gameMenu flag)
    public const int GenericMenu = 888;     // Player Selection, World Selection, Achievements, Workshop, Keybinding...
    public const int Entering = 10;         // Entering / loading a SP world
    public const int MultiplayerBrowser = 12; // MP server browser / IP entry screen
    public const int MultiplayerEnterIp = 13; // Entering IP to join
    public const int MultiplayerJoining = 31; // Actively connecting to a MP server
    public const int MultiplayerInGame = 14;  // Inside a MP world
    public const int Settings = 11;
    public const int Credits = 3000;
  }

  public enum GameScreen
  {
    Unknown,
    MainMenu,
    PlayerSelection,
    WorldSelection,
    EnteringWorld,
    InGameSinglePlayer,
    MultiplayerBrowser,
    MultiplayerPlayerSelection,
    MultiplayerIpSelection,
    MultiplayerJoining,
    InGameMultiplayer,
  }

  public class TerrariaGameState
  {
    public bool IsAttached { get; set; } = false;
    public int RawMenuMode { get; set; } = MenuModes.MainMenuOrInGame;
    public int NetMode { get; set; } = 0; // 0 = SP, 1 = MP Client, 2 = MP Server
    public bool GameMenu { get; set; } = true; // true = in a menu, false = in-game
    public string WorldName { get; set; } = "";

    // Resolved high-level screen — computed by TerrariaMemoryReader
    public GameScreen Screen { get; set; } = GameScreen.MainMenu;

    // In-game stats — placeholders until full memory reading is added
    public string Biome { get; set; } = "Forest";
    public int PlayerHp { get; set; } = 100;
    public int PlayerMaxHp { get; set; } = 100;
    public int PlayerMp { get; set; } = 20;
    public int PlayerMaxMp { get; set; } = 20;
    public string PlayerAtk { get; set; } = "N/A";
    public int HighestRecordedAtk { get; set; } = 0;
    public int PlayerDef { get; set; } = 0;
    public string PlayerItemHeld { get; set; } = "";
    public string PlayerItemPrefix { get; set; } = "";

    // World Stats
    public string WorldSeed { get; set; } = "";
    public string WorldSize { get; set; } = "";
    public string WorldEvil { get; set; } = "";
    /// <summary>Raw difficulty as stored in memory (Classic/Expert/Master/Journey), before FTW escalation.</summary>
    public string WorldRawDifficulty { get; set; } = "";
    /// <summary>Difficulty after FTW/Zenith escalation (e.g. Classic→Expert with FTW active).</summary>
    public string WorldDifficulty { get; set; } = "";
    public bool WorldIsHardmode { get; set; } = false;

    // Special Seeds (Drunk, Not The Bees, etc.)
    public string[] WorldSpecialSeeds { get; set; } = Array.Empty<string>();

    // Secret Seeds (abandoned manors, etc. from seed string)
    public string[] WorldSecretSeeds { get; set; } = Array.Empty<string>();
    public int WorldSecretSeedsAsNum => WorldSecretSeeds.Length;

    // Added: UI State Name for disambiguating menuMode 888
    public string UIStateName { get; set; } = "";
  }

  public class TerrariaMemoryReader
  {
    private Process? terrariaProcess;
    public TerrariaGameState CurrentState { get; private set; } = new TerrariaGameState();

    /// <summary>True if Terraria was successfully attached during the last Update() call.</summary>
    public bool IsConnected { get; private set; } = false;

    // State machine: track the previous screen to disambiguate 888 (player vs world selection)
    private GameScreen _previousScreen = GameScreen.MainMenu;
    private bool _isMultiplayerFlow = false;

    // Cached MethodTable addresses for fast type lookup — populated on first successful scan.
    private ulong _mainTypeMT    = 0;
    private ulong _worldGenTypeMT = 0;
    private ulong _playerTypeMT  = 0;
    private ulong _langTypeMT    = 0;

    public bool Attach()
    {
      if (terrariaProcess == null || terrariaProcess.HasExited)
      {
        var processes = Process.GetProcessesByName("Terraria");
        if (processes.Length > 0)
        {
          terrariaProcess = processes[0];
          CurrentState.IsAttached = true;
          IsConnected = true;
          Logger.Info($"Attached to Terraria (PID: {terrariaProcess.Id})");
          return true;
        }
        if (CurrentState.IsAttached)
          Logger.Info("Terraria process lost — waiting...");
        CurrentState.IsAttached = false;
        IsConnected = false;
        terrariaProcess = null;
        return false;
      }
      CurrentState.IsAttached = true;
      IsConnected = true;
      return true;
    }

    /// <summary>
    /// Looks up a ClrType by name, using a cached MethodTable address for O(1) lookup on
    /// subsequent calls. Falls back to a full module scan only when the cache misses.
    /// Also resets the other MT caches when a fresh Terraria process is attached
    /// (the old MTs won't be valid in a new runtime snapshot).
    /// </summary>
    private ClrType? TryGetCachedType(ClrRuntime runtime, ref ulong cachedMT, string typeName)
    {
      if (cachedMT != 0)
      {
        var cached = runtime.GetTypeByMethodTable(cachedMT);
        if (cached != null) return cached;
        // Cache miss (new process) — clear all MT caches and re-scan
        _mainTypeMT = _worldGenTypeMT = _playerTypeMT = _langTypeMT = 0;
        cachedMT = 0;
      }

      // Full scan fallback
      foreach (var module in runtime.EnumerateModules())
      {
        foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
        {
          var t = runtime.GetTypeByMethodTable(mt);
          if (t?.Name == typeName)
          {
            cachedMT = mt;
            return t;
          }
        }
      }
      return null;
    }

    public void Update()
    {
      if (!Attach()) return;

      try
      {
        using DataTarget dataTarget = DataTarget.AttachToProcess(
          terrariaProcess!.Id,
          suspend: false
        );

        using ClrRuntime? runtime = dataTarget.ClrVersions.FirstOrDefault()?.CreateRuntime();
        if (runtime == null) return;

        ClrAppDomain appDomain = runtime.AppDomains.First();

        // Find core types — use cached MethodTable for O(1) lookup on subsequent ticks.
        ClrType? mainType     = TryGetCachedType(runtime, ref _mainTypeMT,     "Terraria.Main");
        ClrType? worldGenType = TryGetCachedType(runtime, ref _worldGenTypeMT,  "Terraria.WorldGen");
        ClrType? playerType   = TryGetCachedType(runtime, ref _playerTypeMT,    "Terraria.Player");

        if (mainType == null)
        {
          Logger.Warn("Could not locate Terraria.Main type.");
          return;
        }

        // Read menuMode
        var menuModeField = mainType.StaticFields.FirstOrDefault(f => f.Name == "menuMode");
        if (menuModeField != null)
          CurrentState.RawMenuMode = menuModeField.Read<int>(appDomain);

        // Read netMode
        var netModeField = mainType.StaticFields.FirstOrDefault(f => f.Name == "netMode");
        if (netModeField != null)
          CurrentState.NetMode = netModeField.Read<int>(appDomain);

        // Read gameMenu (bool: true = paused/in menu, false = playing)
        var gameMenuField = mainType.StaticFields.FirstOrDefault(f => f.Name == "gameMenu");
        if (gameMenuField != null)
          CurrentState.GameMenu = gameMenuField.Read<bool>(appDomain);

        // Read UIStateName to disambiguate 888
        var menuUIField = mainType.StaticFields.FirstOrDefault(f => f.Name == "MenuUI");
        if (menuUIField != null)
        {
          ulong menuUIAddr = menuUIField.Read<ulong>(appDomain);
          if (menuUIAddr != 0)
          {
            ClrObject menuUIObj = runtime.Heap.GetObject(menuUIAddr);
            if (menuUIObj.IsValid)
            {
              var currentStateField = menuUIObj.Type.Fields.FirstOrDefault(f => f.Name == "_currentState");
              if (currentStateField != null)
              {
                ulong currentStateAddr = currentStateField.Read<ulong>(menuUIAddr, false);
                if (currentStateAddr != 0)
                {
                  ClrObject currentStateObj = runtime.Heap.GetObject(currentStateAddr);
                  if (currentStateObj.IsValid)
                  {
                    CurrentState.UIStateName = currentStateObj.Type.Name ?? "";
                  }
                }
              }
            }
          }
        }

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
                      var parts = rawSeed.Split('|');
                      CurrentState.WorldSeed = parts.Last();
                      CurrentState.WorldSecretSeeds = parts.Take(parts.Length - 1).ToArray();
                    }
                    else
                    {
                      CurrentState.WorldSeed = rawSeed;
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

                      byte itemPrefix = itemObj.ReadField<byte>("prefix");
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
                    CurrentState.PlayerItemHeld = "";
                  }
                  
                  // ── Weapon Damage & DPS ────────────────────────────────────────────────
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
                        DateTime lastHitTime = DateTime.FromBinary(lastHitTicks);
                        
                        // If it's been more than 2 seconds since the last hit, DPS is 0 (timed out).
                        if ((DateTime.Now - lastHitTime).TotalSeconds <= 2.0)
                        {
                          long startTicks = (long)startObj.ReadField<ulong>("dateData");
                          long endTicks = (long)endObj.ReadField<ulong>("dateData");
                          DateTime startTime = DateTime.FromBinary(startTicks);
                          DateTime endTime = DateTime.FromBinary(endTicks);
                          double seconds = (endTime - startTime).TotalSeconds;
                          if (seconds < 1.0) seconds = 1.0;
                          currentDps = (int)(dpsDamage / seconds);
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
                }
              }
            }
          }
        }

        // ── Biome / Depth Detection ──────────────────────────────────────────────
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
          // ── Horizontal biome type from zone bits (zone1 - zone5) ──────────────
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

          // ── Compose final biome string ─────────────────────────────────────────
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
                               : "";  // Surface/Sky → no prefix

            // Biome name (priority order)
            string biomeName;
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
      catch (Exception ex)
      {
        Logger.Error($"ClrMD read failed: {ex.Message}\n{ex}");
        return;
      }

      ResolveScreen();
      PrintState();
    }

    /// <summary>
    /// Converts raw menuMode + netMode + gameMenu into a clean GameScreen enum,
    /// using a state machine to disambiguate identical menuMode values (e.g. 888).
    /// </summary>
    private void ResolveScreen()
    {
      int mode = CurrentState.RawMenuMode;
      int net = CurrentState.NetMode;
      bool inMenu = CurrentState.GameMenu;

      GameScreen screen;

      // ── In-Game checks (gameMenu = false means actively playing) ──────────────
      if (!inMenu)
      {
        screen = net == 1 ? GameScreen.InGameMultiplayer : GameScreen.InGameSinglePlayer;
        _isMultiplayerFlow = false; // reset for next session
      }
      // ── MP in-game fallback (netMode=1, menuMode=14) ──────────────────────────
      else if (mode == MenuModes.MultiplayerInGame && net == 1)
      {
        screen = GameScreen.InGameMultiplayer;
        _isMultiplayerFlow = false;
      }
      // ── Main Menu ─────────────────────────────────────────────────────────────
      else if (mode == MenuModes.MainMenuOrInGame)
      {
        screen = GameScreen.MainMenu;
        _isMultiplayerFlow = false;
      }
      // ── Generic 888: UI States (Player Selection, World Selection, Achievements, etc.) ──
      else if (mode == MenuModes.GenericMenu)
      {
        string uiName = CurrentState.UIStateName;
        if (uiName.EndsWith("UICharacterSelect"))
        {
          screen = (_isMultiplayerFlow || net == 1) ? GameScreen.MultiplayerPlayerSelection : GameScreen.PlayerSelection;
        }
        else if (uiName.EndsWith("UIWorldSelect"))
        {
          screen = GameScreen.WorldSelection;
        }
        else if (uiName.EndsWith("UIAchievementsMenu") || uiName.EndsWith("UIWorkshopHub"))
        {
          screen = GameScreen.Unknown; // Fallback to "In Menus"
        }
        else
        {
          // Fallback if we don't recognize the UI state
          screen = GameScreen.Unknown;
        }
      }
      // ── Entering / Loading a world ────────────────────────────────────────────
      else if (mode == MenuModes.Entering)
      {
        screen = GameScreen.EnteringWorld;
      }
      // ── MP server browser / IP entry ──────────────────────────────────────────
      else if (mode == MenuModes.MultiplayerBrowser)
      {
        screen = GameScreen.MultiplayerBrowser;
        _isMultiplayerFlow = true;
      }
      // ── Entering IP to join ───────────────────────────────────────────────────
      else if (mode == MenuModes.MultiplayerEnterIp)
      {
        screen = GameScreen.MultiplayerIpSelection;
        _isMultiplayerFlow = true;
      }
      // ── Actively joining a MP server ──────────────────────────────────────────
      else if (mode == MenuModes.MultiplayerJoining)
      {
        screen = GameScreen.MultiplayerJoining;
      }
      // ── Everything else (settings, credits, etc.) → treat as "In Menus" ──────
      else
      {
        screen = GameScreen.Unknown;
      }

      if (screen != _previousScreen)
      {
        Logger.Debug($"menuMode={mode} netMode={net} gameMenu={inMenu} ui={CurrentState.UIStateName} -> Screen={screen}");
        
        bool isCurrentlyInGame = screen == GameScreen.InGameSinglePlayer || screen == GameScreen.InGameMultiplayer;
        bool wasInGame = _previousScreen == GameScreen.InGameSinglePlayer || _previousScreen == GameScreen.InGameMultiplayer;

        if (!isCurrentlyInGame && wasInGame)
        {
          CurrentState.HighestRecordedAtk = 0;
          CurrentState.PlayerAtk = "N/A";
        }
        
        _previousScreen = screen;
      }
      CurrentState.Screen = screen;

      Logger.Debug($"menuMode={CurrentState.RawMenuMode} netMode={CurrentState.NetMode} gameMenu={CurrentState.GameMenu} → Screen={screen}");
    }

    private void PrintState()
    {
      if (CurrentState.Screen == GameScreen.InGameSinglePlayer || CurrentState.Screen == GameScreen.InGameMultiplayer)
      {
        string hm = CurrentState.WorldIsHardmode ? "Hardmode" : "Pre-Hardmode";
        string special = CurrentState.WorldSpecialSeeds.Length > 0 ? string.Join(", ", CurrentState.WorldSpecialSeeds) : "None";
        string secret = CurrentState.WorldSecretSeeds.Length > 0 ? string.Join(", ", CurrentState.WorldSecretSeeds) : "None";
        Logger.Info($"--- Game State --- InGame World:\"{CurrentState.WorldName}\" Size:{CurrentState.WorldSize} Evil:{CurrentState.WorldEvil} Diff:{CurrentState.WorldDifficulty} State:{hm}");
        Logger.Info($"Seed:\"{CurrentState.WorldSeed}\" SpecialSeeds:[{special}] SecretSeeds:[{secret}] Biome:\"{CurrentState.Biome}\" HeldItem:\"{CurrentState.PlayerItemHeld}\"");
      }
      else
      {
        Logger.Info($"Screen:{CurrentState.Screen} World:\"{CurrentState.WorldName}\" Attached:{CurrentState.IsAttached}");
      }
    }
  }
}
