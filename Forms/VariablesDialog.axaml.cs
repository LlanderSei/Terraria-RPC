using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrariaRPC.Forms;

public partial class VariablesDialog : Window
{
  private static readonly (string Token, string Description, string Color)[] Variables =
  [
    // ── World ──────────────────────────────────────────────────────────────
    ("{{WorldName}}",             "The current world's name",                                                  "#4fc3f7"),
    ("{{Biome}}",                 "Current biome and depth (e.g. Underground Jungle, Cavern Snow)",           "#4fc3f7"),
    ("{{WorldSize}}",             "World size: Small, Medium, or Large",                                      "#4fc3f7"),
    ("{{WorldEvilType}}",         "World evil type: Corruption or Crimson",                                   "#4fc3f7"),
    ("{{WorldDifficulty}}",       "Base difficulty as set in world creation (Classic/Expert/Master/Journey)", "#4fc3f7"),
    ("{{WorldDifficultyWithFtwEscalation}}", "Difficulty after For the Worthy/Zenith escalation (e.g. Classic → Expert with FTW)", "#4fc3f7"),
    ("{{WorldIsHardmode}}",       "'Hardmode' or 'Pre-hardmode'",                                             "#4fc3f7"),
    ("{{WorldSpecialSeeds}}",     "Active special seeds, comma-separated (e.g. Remix, For The Worthy)",       "#ffcc80"),
    ("{{WorldSecretSeeds}}",      "Active secret seeds, comma-separated",                                     "#ffcc80"),
    ("{{WorldSecretSeedsAsNum}}", "Number of active secret seeds (e.g. 2)",                                   "#ffcc80"),
    ("---", "", ""),
    // ── Player ─────────────────────────────────────────────────────────────
    ("{{PlayerAtk}}",             "Legacy highest weapon damage this session",                                 "#a5d6a7"),
    ("{{PlayerHighestWeaponDmg}}", "Highest weapon damage this session",                                        "#a5d6a7"),
    ("{{PlayerHighestDps}}",      "Highest DPS dealt this session",                                           "#a5d6a7"),
    ("{{PlayerDynamicWeaponDmg}}", "Live weapon damage for the currently held item",                           "#a5d6a7"),
    ("{{PlayerDynamicDps}}",      "Live DPS sample from the current update",                                  "#a5d6a7"),
    ("{{PlayerDef}}",             "Total defense (armor + accessories + buffs)",                              "#a5d6a7"),
    ("{{PlayerHp}}",              "Current HP",                                                               "#a5d6a7"),
    ("{{PlayerMaxHp}}",           "Maximum HP (Life Crystals, Life Fruits, accessories)",                    "#a5d6a7"),
    ("{{PlayerMp}}",              "Current MP",                                                               "#a5d6a7"),
    ("{{PlayerMaxMp}}",           "Maximum MP (Mana Crystals, accessories)",                                  "#a5d6a7"),
    ("{{PlayerItemHeld}}",        "Name of the currently held item (blank if air/nothing)",                   "#a5d6a7"),
    ("---", "", ""),
    // ── Boss & Events ──────────────────────────────────────────────────────
    ("{{ActiveBoss}}",            "Name of active boss (e.g. Eye of Cthulhu)",                                "#ce93d8"),
    ("{{ActiveBossHasShield}}",   "True when the active boss uses shield-style health",                        "#ce93d8"),
    ("{{ActiveBossSp}}",          "Current shield points for shielded bosses and pillars",                    "#ce93d8"),
    ("{{ActiveBossMaxSp}}",       "Maximum shield points for shielded bosses and pillars",                    "#ce93d8"),
    ("{{ActiveBossHp}}",          "Current HP of active boss or pillar shield/hp",                            "#ce93d8"),
    ("{{ActiveBossMaxHp}}",       "Max HP of active boss or pillar shield/hp",                                "#ce93d8"),
    ("{{ActiveEvent}}",           "Name of active event (e.g. Goblin Invasion)",                              "#ce93d8"),
    ("{{ActiveProgressiveEvent}}", "Name of the current progressive event",                                    "#ce93d8"),
    ("{{ActiveEventHasWaves}}",   "True when the active event has a wave counter",                             "#ce93d8"),
    ("{{ActiveEventHasProgress}}","True when the active event exposes percentage progress",                     "#ce93d8"),
    ("{{ActiveEventIsAtMaxWave}}","True when the current wave is at its maximum",                               "#ce93d8"),
    ("{{ActiveEventIsAtMaxProgression}}", "True when the current progression is at its maximum",                  "#ce93d8"),
    ("{{ActiveEventProgress}}",   "Completion percentage of active event (e.g. 90)",                          "#ce93d8"),
    ("{{ActiveEventProgression}}", "Progress value for the active event",                                       "#ce93d8"),
    ("{{ActiveEventWaveNum}}",    "Current wave number of event (e.g. Pumpkin Moon, Frost Moon, OOA)",        "#ce93d8"),
    ("{{ActiveEventPoints}}",     "Points counter used by late-wave moon events",                              "#ce93d8"),
    ("{{ActiveNonProgressiveEvent}}", "Name of the active non-progressive event",                               "#ce93d8"),
    ("{{ActiveNonProgressiveEventValue}}", "Raw value for the active non-progressive event",                    "#ce93d8"),
    ("{{ActivePeacefulEvent}}",   "Display name of the active peaceful event",                                 "#ce93d8"),
    ("{{ActivePeacefulEventValue}}", "Raw peaceful event key (Party, LanternNight, Starfall)",                  "#ce93d8"),
    ("{{ActiveWeather}}",         "Current weather description",                                              "#ce93d8"),
    ("{{ActiveBossOrEventText}}", "Formatted text of current priority boss, event, or weather",               "#ce93d8"),
  ];

  public VariablesDialog()
  {
    InitializeComponent();
    BuildContent();
  }

  protected override void OnOpened(EventArgs e)
  {
    base.OnOpened(e);
    // Remove minimize and maximize buttons via Win32 window style
    if (OperatingSystem.IsWindows())
    {
      try
      {
        var handle = TryGetPlatformHandle()?.Handle;
        if (handle.HasValue && handle.Value != IntPtr.Zero)
        {
          const int GWL_STYLE     = -16;
          const int WS_MINIMIZEBOX = 0x00020000;
          const int WS_MAXIMIZEBOX = 0x00010000;
          int style = GetWindowLong(handle.Value, GWL_STYLE);
          style &= ~(WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
          SetWindowLong(handle.Value, GWL_STYLE, style);
        }
      }
      catch { /* non-critical */ }
    }
  }

  [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
  [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

  private void BuildContent()
  {
    var scrollArea = this.FindControl<ScrollViewer>("ScrollArea")!;
    var stack = new StackPanel { Spacing = 0 };

    // Intro text
    stack.Children.Add(new TextBlock
    {
      Text = "Use these tokens anywhere templates are accepted. Nested {{...}} expressions are supported, along with basic conditionals and comparisons.",
      TextWrapping = TextWrapping.Wrap,
      Opacity = 0.7,
      Margin = new(0, 0, 0, 12)
    });

    // Header
    var header = new Grid { Margin = new(0, 0, 0, 4) };
    header.ColumnDefinitions.Add(new ColumnDefinition(240, GridUnitType.Pixel));
    header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
    var h1 = new TextBlock { Text = "Token", FontWeight = FontWeight.Bold, Margin = new(4, 0, 0, 0) };
    var h2 = new TextBlock { Text = "Description", FontWeight = FontWeight.Bold, Margin = new(8, 0, 0, 0) };
    Grid.SetColumn(h1, 0); Grid.SetColumn(h2, 1);
    header.Children.Add(h1); header.Children.Add(h2);
    stack.Children.Add(header);
    stack.Children.Add(new Separator { Margin = new(0, 0, 0, 4) });

    // Rows
    foreach (var (token, desc, color) in Variables)
    {
      if (token == "---")
      {
        stack.Children.Add(new Separator { Margin = new(0, 6) });
        continue;
      }

      var row = new Grid { Margin = new(0, 2) };
      row.ColumnDefinitions.Add(new ColumnDefinition(240, GridUnitType.Pixel));
      row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

      var tokenBlock = new TextBlock
      {
        Text = token,
        FontFamily = new FontFamily("Cascadia Code,Consolas,monospace"),
        Foreground = SolidColorBrush.Parse(color),
        Margin = new(4, 0),
        TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Top
      };
      var descBlock = new TextBlock
      {
        Text = desc,
        TextWrapping = TextWrapping.Wrap,
        Margin = new(8, 0),
        VerticalAlignment = VerticalAlignment.Top
      };

      Grid.SetColumn(tokenBlock, 0);
      Grid.SetColumn(descBlock, 1);
      row.Children.Add(tokenBlock);
      row.Children.Add(descBlock);
      stack.Children.Add(row);
    }

    scrollArea.Content = stack;
  }

  private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
