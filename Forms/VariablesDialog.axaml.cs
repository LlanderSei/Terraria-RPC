using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrariaRPC.Core;

namespace TerrariaRPC.Forms;

public partial class VariablesDialog : Window
{
  private sealed record VariableEntry(string Token, string Description, string Returns, string Color);

  private static readonly VariableEntry[] Variables =
  [
    new("{{IsAttached}}", "Whether Terraria is attached", "True or False", "#90caf9"),
    new("{{RawMenuMode}}", "Raw Terraria menuMode value", "Integer (commonly 0, 10, 11, 12, 13, 14, 31, 888)", "#90caf9"),
    new("{{NetMode}}", "Raw Terraria netMode value", "0 = single-player, 1 = client, 2 = server", "#90caf9"),
    new("{{GameMenu}}", "Whether Terraria is in a menu", "True or False", "#90caf9"),
    new("{{Screen}}", "Resolved screen name", "MainMenu, PlayerSelection, WorldSelection, EnteringWorld, InGameSinglePlayer, InGameMultiplayer, MultiplayerBrowser, MultiplayerPlayerSelection, MultiplayerIpSelection, MultiplayerJoining, or Unknown", "#90caf9"),
    new("{{ScreenName}}", "Alias for Screen", "Same values as Screen", "#90caf9"),
    new("{{IsInGame}}", "Whether the resolved screen is in-game", "True or False", "#90caf9"),
    new("{{IsMainMenu}}", "Whether the resolved screen is MainMenu", "True or False", "#90caf9"),
    new("---", "", "", ""),
    new("{{WorldName}}", "The current world's name", "String or empty", "#4fc3f7"),
    new("{{Biome}}", "Current biome and depth (e.g. Underground Jungle, Cavern Snow)", "String", "#4fc3f7"),
    new("{{WorldSize}}", "World size", "Small, Medium, or Large", "#4fc3f7"),
    new("{{WorldEvilType}}", "World evil type", "Corruption or Crimson", "#4fc3f7"),
    new("{{WorldDifficulty}}", "Base difficulty as set in world creation", "Classic, Expert, Master, or Journey", "#4fc3f7"),
    new("{{WorldDifficultyWithFtwEscalation}}", "Difficulty after For the Worthy/Zenith escalation", "Difficulty string, possibly escalated", "#4fc3f7"),
    new("{{WorldIsHardmode}}", "Whether the world is in hardmode", "True or False", "#4fc3f7"),
    new("{{WorldSpecialSeeds}}", "Active special seeds, comma-separated (e.g. Remix, For The Worthy)", "Comma-separated string", "#ffcc80"),
    new("{{WorldSecretSeeds}}", "Active secret seeds, comma-separated", "Comma-separated string", "#ffcc80"),
    new("{{WorldSecretSeedsAsNum}}", "Number of active secret seeds", "Integer", "#ffcc80"),
    new("---", "", "", ""),
    new("{{PlayerAtk}}", "Legacy highest weapon damage this session", "String number or N/A", "#a5d6a7"),
    new("{{PlayerHighestWeaponDmg}}", "Highest weapon damage this session", "Integer", "#a5d6a7"),
    new("{{PlayerHighestDps}}", "Highest DPS dealt this session", "Integer", "#a5d6a7"),
    new("{{PlayerDynamicWeaponDmg}}", "Live weapon damage for the currently held item", "Integer, or 0 when no damageable item is held initially. Pauses when next held item is non-damageable until next held item is damageable.", "#a5d6a7"),
    new("{{PlayerDynamicDps}}", "Live DPS sample from the current update", "Integer, or 0 when no valid DPS sample exists", "#a5d6a7"),
    new("{{PlayerDef}}", "Total defense (armor + accessories + buffs)", "Integer", "#a5d6a7"),
    new("{{PlayerHp}}", "Current HP", "Integer", "#a5d6a7"),
    new("{{PlayerMaxHp}}", "Maximum HP (Life Crystals, Life Fruits, accessories)", "Integer", "#a5d6a7"),
    new("{{PlayerMp}}", "Current MP", "Integer", "#a5d6a7"),
    new("{{PlayerMaxMp}}", "Maximum MP (Mana Crystals, accessories)", "Integer", "#a5d6a7"),
    new("{{PlayerItemHeld}}", "Name of the currently held item (blank if air/nothing)", "String or empty", "#a5d6a7"),
    new("{{PlayerHeldItem}}", "Alias for PlayerItemHeld", "String or empty", "#a5d6a7"),
    new("{{PlayerItemPrefix}}", "Prefix text on the held item", "String or empty", "#a5d6a7"),
    new("{{PlayerHeldItemPrefix}}", "Alias for PlayerItemPrefix", "String or empty", "#a5d6a7"),
    new("{{PlayerHeldItemWikiName}}", "Wiki-safe held item name with spaces replaced by underscores", "String or empty", "#a5d6a7"),
    new("{{PlayerIsSpectating}}", "Whether the local player is spectating another player or waiting to respawn", "True or False", "#a5d6a7"),
    new("{{IsSpectating}}", "Alias for PlayerIsSpectating", "True or False", "#a5d6a7"),
    new("{{SpectatedName}}", "Name of the currently spectated player", "Blank or spectated player name", "#a5d6a7"),
    new("{{RespawnTimer}}", "Raw respawn timer value", "0 / number", "#a5d6a7"),
    new("---", "", "", ""),
    new("{{ActiveBoss}}", "Name of active boss (e.g. Eye of Cthulhu)", "String or empty", "#ce93d8"),
    new("{{ActiveBossHasShield}}", "True when the active boss uses shield-style health", "True or False", "#ce93d8"),
    new("{{ActiveBossSp}}", "Current shield points for shielded bosses and pillars", "Integer", "#ce93d8"),
    new("{{ActiveBossMaxSp}}", "Maximum shield points for shielded bosses and pillars", "Integer", "#ce93d8"),
    new("{{ActiveBossHp}}", "Current HP of active boss or pillar shield/hp", "Integer", "#ce93d8"),
    new("{{ActiveBossMaxHp}}", "Max HP of active boss or pillar shield/hp", "Integer", "#ce93d8"),
    new("{{ActiveEvent}}", "Name of active event (e.g. Goblin Invasion)", "String or empty", "#ce93d8"),
    new("{{ActiveProgressiveEvent}}", "Name of the current progressive event", "String or empty", "#ce93d8"),
    new("{{ActiveEventHasWaves}}", "True when the active event has a wave counter", "True or False", "#ce93d8"),
    new("{{ActiveEventHasProgress}}", "True when the active event exposes percentage progress", "True or False", "#ce93d8"),
    new("{{ActiveEventHasProgression}}", "Alias for ActiveEventHasProgress", "True or False", "#ce93d8"),
    new("{{ActiveEventIsAtMaxWave}}", "True when the current wave is at its maximum", "True or False", "#ce93d8"),
    new("{{ActiveEventIsAtMaxProgression}}", "True when the current progression is at its maximum", "True or False", "#ce93d8"),
    new("{{ActiveEventProgress}}", "Completion percentage of active event", "Integer percent or -1", "#ce93d8"),
    new("{{ActiveEventProgression}}", "Progress value for the active event", "Integer percent or -1", "#ce93d8"),
    new("{{ActiveEventWaveNum}}", "Current wave number of event (e.g. Pumpkin Moon, Frost Moon, OOA)", "Integer or -1", "#ce93d8"),
    new("{{ActiveEventPoints}}", "Points counter used by late-wave moon events", "Integer", "#ce93d8"),
    new("{{ActiveEventUsesPoints}}", "True when the current progressive event has switched to points", "True or False", "#ce93d8"),
    new("{{ActiveEventIsOoa}}", "True when the active event is Old One's Army", "True or False", "#ce93d8"),
    new("{{ActiveEventWaveText}}", "Preformatted wave label", "String or empty", "#ce93d8"),
    new("{{ActiveEventProgressText}}", "Preformatted event progress text", "String or empty", "#ce93d8"),
    new("{{ActiveEventDetailText}}", "Combined wave/progress text for progressive events", "String or empty", "#ce93d8"),
    new("{{ActiveNonProgressiveEventText}}", "Formatted non-progressive event text", "String or empty", "#ce93d8"),
    new("{{ActiveProgressUsesPts}}", "Alias for ActiveEventUsesPoints", "True or False", "#ce93d8"),
    new("{{ActiveProgressIsAtMaxWave}}", "Alias for ActiveEventIsAtMaxWave", "True or False", "#ce93d8"),
    new("{{ActiveNonProgressiveEvent}}", "Name of the active non-progressive event", "String or empty", "#ce93d8"),
    new("{{ActiveNonProgressiveEventValue}}", "Raw value for the active non-progressive event", "String or empty", "#ce93d8"),
    new("{{ActivePeacefulEvent}}", "Display name of the active peaceful event", "String or empty", "#ce93d8"),
    new("{{ActivePeacefulEventValue}}", "Raw peaceful event key", "String or empty", "#ce93d8"),
    new("{{ActiveWeather}}", "Current weather description", "String or empty", "#ce93d8"),
    new("{{ActiveBossOrEventText}}", "Formatted text of current priority boss, event, or weather", "String or empty", "#ce93d8"),
  ];

  private readonly List<(string Expression, TextBlock LiveValueBlock)> _liveRows = new();
  private DispatcherTimer? _refreshTimer;

  public VariablesDialog()
  {
    InitializeComponent();
    BuildContent();
    StartRefreshTimer();
  }

  protected override void OnOpened(EventArgs e)
  {
    base.OnOpened(e);
    if (OperatingSystem.IsWindows())
    {
      try
      {
        var handle = TryGetPlatformHandle()?.Handle;
        if (handle.HasValue && handle.Value != IntPtr.Zero)
        {
          const int GWL_STYLE = -16;
          const int WS_MINIMIZEBOX = 0x00020000;
          const int WS_MAXIMIZEBOX = 0x00010000;
          int style = GetWindowLong(handle.Value, GWL_STYLE);
          style &= ~(WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
          SetWindowLong(handle.Value, GWL_STYLE, style);
        }
      }
      catch
      {
      }
    }
  }

  protected override void OnClosed(EventArgs e)
  {
    _refreshTimer?.Stop();
    _refreshTimer = null;
    base.OnClosed(e);
  }

  [DllImport("user32.dll")]
  private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

  [DllImport("user32.dll")]
  private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

  private void StartRefreshTimer()
  {
    _refreshTimer = new DispatcherTimer
    {
      Interval = TimeSpan.FromSeconds(1)
    };
    _refreshTimer.Tick += (_, _) => RefreshLiveData();
    _refreshTimer.Start();
    RefreshLiveData();
  }

  private void BuildContent()
  {
    var scrollArea = this.FindControl<ScrollViewer>("ScrollArea")!;
    var stack = new StackPanel { Spacing = 0 };

    stack.Children.Add(new TextBlock
    {
      Text = "Use these tokens anywhere templates are accepted. The Returns column shows the value type or expected values. Nested {{...}} expressions are supported, along with basic conditionals and comparisons.",
      TextWrapping = TextWrapping.Wrap,
      Opacity = 0.7,
      Margin = new(0, 0, 0, 12)
    });

    var header = new Grid { Margin = new(0, 0, 0, 4) };
    header.ColumnDefinitions.Add(new ColumnDefinition(180, GridUnitType.Pixel));
    header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
    header.ColumnDefinitions.Add(new ColumnDefinition(220, GridUnitType.Pixel));
    header.ColumnDefinitions.Add(new ColumnDefinition(240, GridUnitType.Pixel));
    header.Children.Add(CreateHeaderCell("Token", 0));
    header.Children.Add(CreateHeaderCell("Description", 1));
    header.Children.Add(CreateHeaderCell("Returns", 2));
    header.Children.Add(CreateHeaderCell("Live Data", 3));
    stack.Children.Add(header);
    stack.Children.Add(new Separator { Margin = new(0, 0, 0, 4) });

    foreach (var entry in Variables)
    {
      if (entry.Token == "---")
      {
        stack.Children.Add(new Separator { Margin = new(0, 6) });
        continue;
      }

      var row = new Grid { Margin = new(0, 2) };
      row.ColumnDefinitions.Add(new ColumnDefinition(180, GridUnitType.Pixel));
      row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
      row.ColumnDefinitions.Add(new ColumnDefinition(220, GridUnitType.Pixel));
      row.ColumnDefinitions.Add(new ColumnDefinition(240, GridUnitType.Pixel));

      var tokenBlock = new TextBlock
      {
        Text = StripBraces(entry.Token),
        FontFamily = new FontFamily("Cascadia Code,Consolas,monospace"),
        Foreground = SolidColorBrush.Parse(entry.Color),
        Margin = new(4, 0, 8, 0),
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Top
      };
      var descBlock = new TextBlock
      {
        Text = entry.Description,
        TextWrapping = TextWrapping.Wrap,
        Margin = new(8, 0),
        VerticalAlignment = VerticalAlignment.Top
      };
      var returnsBlock = new TextBlock
      {
        Text = entry.Returns,
        TextWrapping = TextWrapping.Wrap,
        Margin = new(8, 0),
        VerticalAlignment = VerticalAlignment.Top,
        Opacity = 0.9
      };
      var liveBlock = new TextBlock
      {
        Text = "",
        TextWrapping = TextWrapping.Wrap,
        Margin = new(8, 0),
        VerticalAlignment = VerticalAlignment.Top,
        Opacity = 0.95
      };

      Grid.SetColumn(tokenBlock, 0);
      Grid.SetColumn(descBlock, 1);
      Grid.SetColumn(returnsBlock, 2);
      Grid.SetColumn(liveBlock, 3);
      row.Children.Add(tokenBlock);
      row.Children.Add(descBlock);
      row.Children.Add(returnsBlock);
      row.Children.Add(liveBlock);
      stack.Children.Add(row);
      _liveRows.Add((entry.Token, liveBlock));
    }

    scrollArea.Content = stack;
  }

  private void RefreshLiveData()
  {
    var state = Program.GetLiveStateSnapshot();
    foreach (var (expression, liveValueBlock) in _liveRows)
    {
      liveValueBlock.Text = PresenceTemplateEngine.Format(expression, state);
    }
  }

  private static string StripBraces(string token)
  {
    if (token.StartsWith("{{", StringComparison.Ordinal) && token.EndsWith("}}", StringComparison.Ordinal) && token.Length >= 4)
    {
      return token[2..^2];
    }

    return token;
  }

  private static TextBlock CreateHeaderCell(string text, int column)
  {
    var block = new TextBlock
    {
      Text = text,
      FontWeight = FontWeight.Bold,
      Margin = new(8, 0, 0, 0)
    };
    Grid.SetColumn(block, column);
    return block;
  }

  private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
