using System;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaRPC.Core
{
  public partial class TerrariaMemoryReader
  {
    private void ReadMenuState(ClrRuntime runtime, ClrAppDomain appDomain, ClrType mainType)
    {
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
    }
    private void ResolveScreen()
    {
      int mode = CurrentState.RawMenuMode;
      int net = CurrentState.NetMode;
      bool inMenu = CurrentState.GameMenu;

      GameScreen screen;

      // -- In-Game checks (gameMenu = false means actively playing) --------------
      if (!inMenu)
      {
        screen = net == 1 ? GameScreen.InGameMultiplayer : GameScreen.InGameSinglePlayer;
        _isMultiplayerFlow = false; // reset for next session
      }
      // -- MP in-game fallback (netMode=1, menuMode=14) --------------------------
      else if (mode == MenuModes.MultiplayerInGame && net == 1)
      {
        screen = GameScreen.InGameMultiplayer;
        _isMultiplayerFlow = false;
      }
      // -- Main Menu -------------------------------------------------------------
      else if (mode == MenuModes.MainMenuOrInGame)
      {
        screen = GameScreen.MainMenu;
        _isMultiplayerFlow = false;
      }
      // -- Generic 888: UI States (Player Selection, World Selection, Achievements, etc.) --
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
      // -- Entering / Loading a world --------------------------------------------
      else if (mode == MenuModes.Entering)
      {
        screen = GameScreen.EnteringWorld;
      }
      // -- MP server browser / IP entry ------------------------------------------
      else if (mode == MenuModes.MultiplayerBrowser)
      {
        screen = GameScreen.MultiplayerBrowser;
        _isMultiplayerFlow = true;
      }
      // -- Entering IP to join ---------------------------------------------------
      else if (mode == MenuModes.MultiplayerEnterIp)
      {
        screen = GameScreen.MultiplayerIpSelection;
        _isMultiplayerFlow = true;
      }
      // -- Actively joining a MP server ------------------------------------------
      else if (mode == MenuModes.MultiplayerJoining)
      {
        screen = GameScreen.MultiplayerJoining;
      }
      // -- Everything else (settings, credits, etc.) ? treat as "In Menus" ------
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
          CurrentState.PlayerHighestWeaponDmg = 0;
          CurrentState.PlayerHighestDps = 0;
          CurrentState.PlayerDynamicWeaponDmg = 0;
          CurrentState.PlayerDynamicDps = 0;
        }
        
        _previousScreen = screen;
      }
      CurrentState.Screen = screen;

      Logger.Debug($"menuMode={CurrentState.RawMenuMode} netMode={CurrentState.NetMode} gameMenu={CurrentState.GameMenu} ? Screen={screen}");
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
