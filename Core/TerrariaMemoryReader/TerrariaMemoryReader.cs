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
    public int PlayerHighestWeaponDmg { get; set; } = 0;
    public int PlayerHighestDps { get; set; } = 0;
    public int PlayerDynamicWeaponDmg { get; set; } = 0;
    public int PlayerDynamicDps { get; set; } = 0;
    public int PlayerDef { get; set; } = 0;
    public string PlayerItemHeld { get; set; } = "";
    public string PlayerItemPrefix { get; set; } = "";

    // World Stats
    public string WorldSeed { get; set; } = "";
    public string WorldSize { get; set; } = "";
    public string WorldEvil { get; set; } = "";
    /// <summary>Raw difficulty as stored in memory (Classic/Expert/Master/Journey), before FTW escalation.</summary>
    public string WorldRawDifficulty { get; set; } = "";
    /// <summary>Difficulty after FTW/Zenith escalation (e.g. Classic?Expert with FTW active).</summary>
    public string WorldDifficulty { get; set; } = "";
    public bool WorldIsHardmode { get; set; } = false;

    // Special Seeds (Drunk, Not The Bees, etc.)
    public string[] WorldSpecialSeeds { get; set; } = Array.Empty<string>();

    // Secret Seeds (abandoned manors, etc. from seed string)
    public string[] WorldSecretSeeds { get; set; } = Array.Empty<string>();
    public int WorldSecretSeedsAsNum => WorldSecretSeeds.Length;

    // Added: UI State Name for disambiguating menuMode 888
    public string UIStateName { get; set; } = "";

    // -- Active Boss Info --------------------------------------------------
    public bool HasActiveBoss => !string.IsNullOrEmpty(ActiveBossName);
    public string ActiveBossName { get; set; } = "";
    public int ActiveBossHp { get; set; } = 0;
    public int ActiveBossMaxHp { get; set; } = 0;
    public string ActiveBossText => HasActiveBoss ? $"Fighting: {ActiveBossName} ({ActiveBossHp}/{ActiveBossMaxHp})" : "";

    // -- Active Progressive Event Info --------------------------------------
    public bool HasActiveEvent => !string.IsNullOrEmpty(ActiveEventName);
    public string ActiveEventName { get; set; } = "";
    public int ActiveEventProgress { get; set; } = -1; // -1 if non-% (e.g. Slime Rain)
    public int ActiveEventWaveNum { get; set; } = -1; // -1 if no wave number

    public string ActiveEventText
    {
      get
      {
        if (!HasActiveEvent) return "";

        bool hasWave = ActiveEventWaveNum > 0;
        bool hasPct = ActiveEventProgress >= 0;

        if (hasWave && hasPct)
          return $"Clearing: {ActiveEventName} (Wave {ActiveEventWaveNum}: {ActiveEventProgress}%)";
        if (hasWave)
          return $"Clearing: {ActiveEventName} (Wave {ActiveEventWaveNum})";
        if (hasPct)
          return $"Clearing: {ActiveEventName} ({ActiveEventProgress}%)";

        return $"Clearing: {ActiveEventName}";
      }
    }

    // -- Non-Progressive Event Info -----------------------------------------
    public bool HasActiveNonProgressiveEvent => !string.IsNullOrEmpty(ActiveNonProgressiveEventName);
    public string ActiveNonProgressiveEventName { get; set; } = ""; // e.g. "Blood Moon", "Solar Eclipse"

    // -- Peaceful Event Info ------------------------------------------------
    public bool HasActivePeacefulEvent => !string.IsNullOrEmpty(ActivePeacefulEventName);
    public string ActivePeacefulEventName { get; set; } = ""; // e.g. "Party is occurring.", "Lantern Night is occurring"

    // -- Weather Event Info -------------------------------------------------
    public bool HasActiveWeather => !string.IsNullOrEmpty(ActiveWeatherName);
    public string ActiveWeatherName { get; set; } = ""; // e.g. "Rain", "Thunderstorm", "Sandstorm", "Windy Day"

    public TerrariaGameState Clone()
    {
      return new TerrariaGameState
      {
        IsAttached = IsAttached,
        RawMenuMode = RawMenuMode,
        NetMode = NetMode,
        GameMenu = GameMenu,
        WorldName = WorldName,
        Screen = Screen,
        Biome = Biome,
        PlayerHp = PlayerHp,
        PlayerMaxHp = PlayerMaxHp,
        PlayerMp = PlayerMp,
        PlayerMaxMp = PlayerMaxMp,
        PlayerAtk = PlayerAtk,
        HighestRecordedAtk = HighestRecordedAtk,
        PlayerHighestWeaponDmg = PlayerHighestWeaponDmg,
        PlayerHighestDps = PlayerHighestDps,
        PlayerDynamicWeaponDmg = PlayerDynamicWeaponDmg,
        PlayerDynamicDps = PlayerDynamicDps,
        PlayerDef = PlayerDef,
        PlayerItemHeld = PlayerItemHeld,
        PlayerItemPrefix = PlayerItemPrefix,
        WorldSeed = WorldSeed,
        WorldSize = WorldSize,
        WorldEvil = WorldEvil,
        WorldRawDifficulty = WorldRawDifficulty,
        WorldDifficulty = WorldDifficulty,
        WorldIsHardmode = WorldIsHardmode,
        WorldSpecialSeeds = WorldSpecialSeeds.ToArray(),
        WorldSecretSeeds = WorldSecretSeeds.ToArray(),
        UIStateName = UIStateName,
        ActiveBossName = ActiveBossName,
        ActiveBossHp = ActiveBossHp,
        ActiveBossMaxHp = ActiveBossMaxHp,
        ActiveEventName = ActiveEventName,
        ActiveEventProgress = ActiveEventProgress,
        ActiveEventWaveNum = ActiveEventWaveNum,
        ActiveNonProgressiveEventName = ActiveNonProgressiveEventName,
        ActivePeacefulEventName = ActivePeacefulEventName,
        ActiveWeatherName = ActiveWeatherName
      };
    }
  }

  public partial class TerrariaMemoryReader
  {
    private Process? terrariaProcess;
    public TerrariaGameState CurrentState { get; private set; } = new TerrariaGameState();

    /// <summary>True if Terraria was successfully attached during the last Update() call.</summary>
    public bool IsConnected { get; private set; } = false;

    // State machine: track the previous screen to disambiguate 888 (player vs world selection)
    private GameScreen _previousScreen = GameScreen.MainMenu;
    private bool _isMultiplayerFlow = false;

    // Cached MethodTable addresses for fast type lookup — populated on first successful scan.
    private ulong _mainTypeMT          = 0;
    private ulong _worldGenTypeMT       = 0;
    private ulong _playerTypeMT        = 0;
    private ulong _langTypeMT          = 0;
    private ulong _birthdayPartyTypeMT = 0;
    private ulong _lanternNightTypeMT   = 0;
    private ulong _sandstormTypeMT      = 0;
    private ulong _dd2EventTypeMT       = 0;
    private int _lastKnownOoaWave       = -1;
    private int _lastTwinsMaxHp         = 0;
    private int _lastEowMaxHp           = 0;
    private int _lastBocMaxHp           = 0;
    private int _lastPrimeMaxHp         = 0;
    private int _lastGolemMaxHp         = 0;
    private int _lastMoonLordMaxHp      = 0;
    private string _lastAtkItemSignature = "";
    private readonly object _stateLock = new();

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
        _birthdayPartyTypeMT = _lanternNightTypeMT = _sandstormTypeMT = _dd2EventTypeMT = 0;
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

    public TerrariaGameState GetStateSnapshot()
    {
      lock (_stateLock)
      {
        return CurrentState.Clone();
      }
    }

    public void Update()
    {
      if (!Attach()) return;

      lock (_stateLock)
      {
        try
        {
          using DataTarget dataTarget = DataTarget.AttachToProcess(
            terrariaProcess!.Id,
            suspend: false
          );

          using ClrRuntime? runtime = dataTarget.ClrVersions.FirstOrDefault()?.CreateRuntime();
          if (runtime == null) return;

          ClrAppDomain appDomain = runtime.AppDomains.First();

          // Find core types - use cached MethodTable for O(1) lookup on subsequent ticks.
          ClrType? mainType     = TryGetCachedType(runtime, ref _mainTypeMT,     "Terraria.Main");
          ClrType? worldGenType = TryGetCachedType(runtime, ref _worldGenTypeMT,  "Terraria.WorldGen");
          ClrType? playerType   = TryGetCachedType(runtime, ref _playerTypeMT,    "Terraria.Player");

          if (mainType == null)
          {
            Logger.Warn("Could not locate Terraria.Main type.");
            return;
          }

          ReadMenuState(runtime, appDomain, mainType);
          ReadWorldState(runtime, appDomain, mainType, worldGenType);
          ReadPlayerState(runtime, appDomain, mainType, playerType);
          ScanBossesAndEvents(runtime, appDomain, mainType);
        }
        catch (Exception ex)
        {
          Logger.Error($"ClrMD read failed: {ex.Message}\n{ex}");
          return;
        }

        ResolveScreen();
        PrintState();
      }
    }
  }
}
