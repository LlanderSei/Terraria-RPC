using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrariaRPC.Forms;

public partial class VariablesDialog : Window
{
  private static readonly (string Token, string Description, string Returns, string Color)[] Variables =
  [
    ("{{WorldName}}", "The current world's name", "String or empty", "#4fc3f7"),
    ("{{Biome}}", "Current biome and depth (e.g. Underground Jungle, Cavern Snow)", "String", "#4fc3f7"),
    ("{{WorldSize}}", "World size", "Small, Medium, or Large", "#4fc3f7"),
    ("{{WorldEvilType}}", "World evil type", "Corruption or Crimson", "#4fc3f7"),
    ("{{WorldDifficulty}}", "Base difficulty as set in world creation", "Classic, Expert, Master, or Journey", "#4fc3f7"),
    ("{{WorldDifficultyWithFtwEscalation}}", "Difficulty after For the Worthy/Zenith escalation", "Difficulty string, possibly escalated", "#4fc3f7"),
    ("{{WorldIsHardmode}}", "Whether the world is in hardmode", "True or False", "#4fc3f7"),
    ("{{WorldSpecialSeeds}}", "Active special seeds, comma-separated (e.g. Remix, For The Worthy)", "Comma-separated string", "#ffcc80"),
    ("{{WorldSecretSeeds}}", "Active secret seeds, comma-separated", "Comma-separated string", "#ffcc80"),
    ("{{WorldSecretSeedsAsNum}}", "Number of active secret seeds", "Integer", "#ffcc80"),
    ("---", "", "", ""),
    ("{{PlayerAtk}}", "Legacy highest weapon damage this session", "String number or N/A", "#a5d6a7"),
    ("{{PlayerHighestWeaponDmg}}", "Highest weapon damage this session", "Integer", "#a5d6a7"),
    ("{{PlayerHighestDps}}", "Highest DPS dealt this session", "Integer", "#a5d6a7"),
    ("{{PlayerDynamicWeaponDmg}}", "Live weapon damage for the currently held item", "Integer, or 0 when no damageable item is held initially. Pauses when next held item is non-damageable until next held item is damageable.", "#a5d6a7"),
    ("{{PlayerDynamicDps}}", "Live DPS sample from the current update", "Integer, or 0 when no valid DPS sample exists", "#a5d6a7"),
    ("{{PlayerDef}}", "Total defense (armor + accessories + buffs)", "Integer", "#a5d6a7"),
    ("{{PlayerHp}}", "Current HP", "Integer", "#a5d6a7"),
    ("{{PlayerMaxHp}}", "Maximum HP (Life Crystals, Life Fruits, accessories)", "Integer", "#a5d6a7"),
    ("{{PlayerMp}}", "Current MP", "Integer", "#a5d6a7"),
    ("{{PlayerMaxMp}}", "Maximum MP (Mana Crystals, accessories)", "Integer", "#a5d6a7"),
    ("{{PlayerItemHeld}}", "Name of the currently held item (blank if air/nothing)", "String or empty", "#a5d6a7"),
    ("---", "", "", ""),
    ("{{ActiveBoss}}", "Name of active boss (e.g. Eye of Cthulhu)", "String or empty", "#ce93d8"),
    ("{{ActiveBossHasShield}}", "True when the active boss uses shield-style health", "True or False", "#ce93d8"),
    ("{{ActiveBossSp}}", "Current shield points for shielded bosses and pillars", "Integer", "#ce93d8"),
    ("{{ActiveBossMaxSp}}", "Maximum shield points for shielded bosses and pillars", "Integer", "#ce93d8"),
    ("{{ActiveBossHp}}", "Current HP of active boss or pillar shield/hp", "Integer", "#ce93d8"),
    ("{{ActiveBossMaxHp}}", "Max HP of active boss or pillar shield/hp", "Integer", "#ce93d8"),
    ("{{ActiveEvent}}", "Name of active event (e.g. Goblin Invasion)", "String or empty", "#ce93d8"),
    ("{{ActiveProgressiveEvent}}", "Name of the current progressive event", "String or empty", "#ce93d8"),
    ("{{ActiveEventHasWaves}}", "True when the active event has a wave counter", "True or False", "#ce93d8"),
    ("{{ActiveEventHasProgress}}", "True when the active event exposes percentage progress", "True or False", "#ce93d8"),
    ("{{ActiveEventHasProgression}}", "Alias for ActiveEventHasProgress", "True or False", "#ce93d8"),
    ("{{ActiveEventIsAtMaxWave}}", "True when the current wave is at its maximum", "True or False", "#ce93d8"),
    ("{{ActiveEventIsAtMaxProgression}}", "True when the current progression is at its maximum", "True or False", "#ce93d8"),
    ("{{ActiveEventProgress}}", "Completion percentage of active event", "Integer percent or -1", "#ce93d8"),
    ("{{ActiveEventProgression}}", "Progress value for the active event", "Integer percent or -1", "#ce93d8"),
    ("{{ActiveEventWaveNum}}", "Current wave number of event (e.g. Pumpkin Moon, Frost Moon, OOA)", "Integer or -1", "#ce93d8"),
    ("{{ActiveEventPoints}}", "Points counter used by late-wave moon events", "Integer", "#ce93d8"),
    ("{{ActiveEventUsesPoints}}", "True when the current progressive event has switched to points", "True or False", "#ce93d8"),
    ("{{ActiveEventIsOoa}}", "True when the active event is Old One's Army", "True or False", "#ce93d8"),
    ("{{ActiveEventWaveText}}", "Preformatted wave label", "String or empty", "#ce93d8"),
    ("{{ActiveEventProgressText}}", "Preformatted event progress text", "String or empty", "#ce93d8"),
    ("{{ActiveEventDetailText}}", "Combined wave/progress text for progressive events", "String or empty", "#ce93d8"),
    ("{{ActiveNonProgressiveEventText}}", "Formatted non-progressive event text", "String or empty", "#ce93d8"),
    ("{{ActiveProgressUsesPts}}", "Alias for ActiveEventUsesPoints", "True or False", "#ce93d8"),
    ("{{ActiveProgressIsAtMaxWave}}", "Alias for ActiveEventIsAtMaxWave", "True or False", "#ce93d8"),
    ("{{ActiveNonProgressiveEvent}}", "Name of the active non-progressive event", "String or empty", "#ce93d8"),
    ("{{ActiveNonProgressiveEventValue}}", "Raw value for the active non-progressive event", "String or empty", "#ce93d8"),
    ("{{ActivePeacefulEvent}}", "Display name of the active peaceful event", "String or empty", "#ce93d8"),
    ("{{ActivePeacefulEventValue}}", "Raw peaceful event key", "String or empty", "#ce93d8"),
    ("{{ActiveWeather}}", "Current weather description", "String or empty", "#ce93d8"),
    ("{{ActiveBossOrEventText}}", "Formatted text of current priority boss, event, or weather", "String or empty", "#ce93d8"),
  ];

  public VariablesDialog()
  {
    InitializeComponent();
    BuildContent();
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
      catch { }
    }
  }

  [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
  [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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
    header.ColumnDefinitions.Add(new ColumnDefinition(220, GridUnitType.Pixel));
    header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
    header.ColumnDefinitions.Add(new ColumnDefinition(240, GridUnitType.Pixel));
    var h1 = new TextBlock { Text = "Token", FontWeight = FontWeight.Bold, Margin = new(4, 0, 0, 0) };
    var h2 = new TextBlock { Text = "Description", FontWeight = FontWeight.Bold, Margin = new(8, 0, 0, 0) };
    var h3 = new TextBlock { Text = "Returns", FontWeight = FontWeight.Bold, Margin = new(8, 0, 0, 0) };
    Grid.SetColumn(h1, 0);
    Grid.SetColumn(h2, 1);
    Grid.SetColumn(h3, 2);
    header.Children.Add(h1);
    header.Children.Add(h2);
    header.Children.Add(h3);
    stack.Children.Add(header);
    stack.Children.Add(new Separator { Margin = new(0, 0, 0, 4) });

    foreach (var (token, desc, returns, color) in Variables)
    {
      if (token == "---")
      {
        stack.Children.Add(new Separator { Margin = new(0, 6) });
        continue;
      }

      var row = new Grid { Margin = new(0, 2) };
      row.ColumnDefinitions.Add(new ColumnDefinition(220, GridUnitType.Pixel));
      row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
      row.ColumnDefinitions.Add(new ColumnDefinition(240, GridUnitType.Pixel));

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
      var returnsBlock = new TextBlock
      {
        Text = returns,
        TextWrapping = TextWrapping.Wrap,
        Margin = new(8, 0),
        VerticalAlignment = VerticalAlignment.Top,
        Opacity = 0.9
      };

      Grid.SetColumn(tokenBlock, 0);
      Grid.SetColumn(descBlock, 1);
      Grid.SetColumn(returnsBlock, 2);
      row.Children.Add(tokenBlock);
      row.Children.Add(descBlock);
      row.Children.Add(returnsBlock);
      stack.Children.Add(row);
    }

    scrollArea.Content = stack;
  }

  private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
