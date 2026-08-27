using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using TerrariaRPC.Core;

namespace TerrariaRPC.Forms;

public partial class MainWindow : Window
{
  private enum ConfigPane
  {
    MainMenu,
    InGame
  }

  private bool _isUpdatingCheckboxes = false;
  private bool _isUpdatingPaneState = false;
  private ConfigPane _activePane = ConfigPane.MainMenu;
  private bool _isAppShutdownRequested = false;
  private TextBox? _expandedMainMenuField;

  public MainWindow()
  {
    InitializeComponent();
    LoadConfigData();
  }

  private void LoadConfigData()
  {
    var config = ConfigManager.CurrentConfig;
    if (config != null)
    {
      _isUpdatingCheckboxes = true;

      this.FindControl<TextBox>("MainMenuLine1Box")!.Text = config.MainMenuLine1;
      this.FindControl<TextBox>("MainMenuLine2Box")!.Text = config.MainMenuLine2;
      this.FindControl<TextBox>("MainMenuSmallImageUrlBox")!.Text = config.MainMenuSmallImageUrl;
      this.FindControl<TextBox>("MainMenuSmallImageTextBox")!.Text = config.MainMenuSmallImageText;
      this.FindControl<TextBox>("MainMenuLargeImageUrlBox")!.Text = config.MainMenuLargeImageUrl;
      this.FindControl<TextBox>("MainMenuLargeImageTextBox")!.Text = config.MainMenuLargeImageText;

      var inGameLine1Box = this.FindControl<TextBox>("Line1Box");
      var inGameLine2Box = this.FindControl<TextBox>("Line2Box");
      if (inGameLine1Box != null) inGameLine1Box.Text = config.InGameLine1;
      if (inGameLine2Box != null) inGameLine2Box.Text = config.InGameLine2;

      this.FindControl<ComboBox>("SmallImageStyleBox")!.SelectedIndex = config.SmallImageStyleIndex;
      this.FindControl<TextBox>("SmallImageCustomUrlBox")!.Text = config.SmallImageCustomUrl;
      this.FindControl<TextBox>("SmallImageCustomTextBox")!.Text = config.SmallImageCustomText;

      // Small Rotation checkboxes
      this.FindControl<CheckBox>("SmallItemEnabledBox")!.IsChecked = config.SmallItemEnabled;
      this.FindControl<CheckBox>("SmallBossEventEnabledBox")!.IsChecked = config.SmallBossEventEnabled;

      // Excludes
      this.FindControl<CheckBox>("ExcludeBossBox")!.IsChecked = config.ExcludeBoss;
      this.FindControl<CheckBox>("ExcludeEventsBox")!.IsChecked = config.ExcludeEvents;
      this.FindControl<CheckBox>("ExcludeNonProgressiveBox")!.IsChecked = config.ExcludeNonProgressiveEvents;
      this.FindControl<CheckBox>("ExcludePeacefulBox")!.IsChecked = config.ExcludePeacefulEvents;
      this.FindControl<CheckBox>("ExcludeWeatherBox")!.IsChecked = config.ExcludeWeather;

      // Includes
      this.FindControl<CheckBox>("IncludeEventsBox")!.IsChecked = config.IncludeEvents;
      this.FindControl<CheckBox>("IncludeNonProgressiveBox")!.IsChecked = config.IncludeNonProgressiveEvents;
      this.FindControl<CheckBox>("IncludePeacefulBox")!.IsChecked = config.IncludePeacefulEvents;
      this.FindControl<CheckBox>("IncludeWeatherBox")!.IsChecked = config.IncludeWeather;

      this.FindControl<ComboBox>("LargeImageStyleBox")!.SelectedIndex = config.LargeImageStyleIndex;
      this.FindControl<TextBox>("LargeImageCustomUrlBox")!.Text = config.LargeImageCustomUrl;
      this.FindControl<TextBox>("LargeImageCustomTextBox")!.Text = config.LargeImageCustomText;

      this.FindControl<TextBox>("ClientIdBox")!.Text = config.ClientId;

      _isUpdatingCheckboxes = false;
      SetActivePane(ConfigPane.MainMenu);
      UpdateVisibility();
    }
  }

  private void SetActivePane(ConfigPane pane)
  {
    if (_isUpdatingPaneState) return;
    _activePane = pane;

    var mainMenuPanel = this.FindControl<Panel>("MainMenuPanel");
    var inGamePanel = this.FindControl<Panel>("InGamePanel");
    if (mainMenuPanel != null) mainMenuPanel.IsVisible = pane == ConfigPane.MainMenu;
    if (inGamePanel != null) inGamePanel.IsVisible = pane == ConfigPane.InGame;

    var mainMenuButton = this.FindControl<Button>("MainMenuPaneButton");
    var inGameButton = this.FindControl<Button>("InGamePaneButton");

    ApplyPaneButtonState(mainMenuButton, pane == ConfigPane.MainMenu);
    ApplyPaneButtonState(inGameButton, pane == ConfigPane.InGame);
  }

  private static Color GetThemeAccentColor()
  {
    if (OperatingSystem.IsWindows())
    {
      try
      {
        DwmGetColorizationColor(out uint colorizationColor, out _);
        byte a = (byte)((colorizationColor >> 24) & 0xFF);
        byte r = (byte)((colorizationColor >> 16) & 0xFF);
        byte g = (byte)((colorizationColor >> 8) & 0xFF);
        byte b = (byte)(colorizationColor & 0xFF);
        return Color.FromArgb(a, r, g, b);
      }
      catch
      {
      }
    }

    return Color.FromRgb(0x38, 0x8f, 0xff);
  }

  [DllImport("dwmapi.dll")]
  private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

  private static void ApplyPaneButtonState(Button? button, bool selected)
  {
    if (button == null) return;

    var accent = new SolidColorBrush(GetThemeAccentColor());
    button.Background = selected ? accent : Brushes.Transparent;
    button.BorderBrush = selected ? accent : new SolidColorBrush(Color.FromArgb(0x70, 0x80, 0x80, 0x80));
    button.Foreground = selected ? Brushes.White : Brushes.White;
    button.FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal;
  }

  private void UpdateVisibility()
  {
    var smallStyleBox = this.FindControl<ComboBox>("SmallImageStyleBox");
    bool isSmallRotation = smallStyleBox != null && smallStyleBox.SelectedIndex == 0;
    bool isSmallCustom = smallStyleBox != null && smallStyleBox.SelectedIndex == 1;

    var smallRotationPanel = this.FindControl<StackPanel>("SmallRotationPanel");
    if (smallRotationPanel != null) smallRotationPanel.IsVisible = isSmallRotation;

    var bossEventCheck = this.FindControl<CheckBox>("SmallBossEventEnabledBox");
    var bossEventSubPanel = this.FindControl<StackPanel>("BossEventSubPanel");
    if (bossEventSubPanel != null)
    {
      bossEventSubPanel.IsVisible = isSmallRotation && (bossEventCheck?.IsChecked ?? false);
    }

    var smallUrlLabel = this.FindControl<TextBlock>("SmallImageCustomUrlLabel");
    var smallUrlBox = this.FindControl<TextBox>("SmallImageCustomUrlBox");
    var smallTextLabel = this.FindControl<TextBlock>("SmallImageCustomTextLabel");
    var smallTextBox = this.FindControl<TextBox>("SmallImageCustomTextBox");

    if (smallUrlLabel != null) smallUrlLabel.IsVisible = isSmallCustom;
    if (smallUrlBox != null) smallUrlBox.IsVisible = isSmallCustom;
    if (smallTextLabel != null) smallTextLabel.IsVisible = isSmallCustom;
    if (smallTextBox != null) smallTextBox.IsVisible = isSmallCustom;

    var largeStyleBox = this.FindControl<ComboBox>("LargeImageStyleBox");
    bool isLargeCustom = largeStyleBox != null && largeStyleBox.SelectedIndex == 1;

    var largeUrlLabel = this.FindControl<TextBlock>("LargeImageCustomUrlLabel");
    var largeUrlBox = this.FindControl<TextBox>("LargeImageCustomUrlBox");
    var largeTextLabel = this.FindControl<TextBlock>("LargeImageCustomTextLabel");
    var largeTextBox = this.FindControl<TextBox>("LargeImageCustomTextBox");

    if (largeUrlLabel != null) largeUrlLabel.IsVisible = isLargeCustom;
    if (largeUrlBox != null) largeUrlBox.IsVisible = isLargeCustom;
    if (largeTextLabel != null) largeTextLabel.IsVisible = isLargeCustom;
    if (largeTextBox != null) largeTextBox.IsVisible = isLargeCustom;
  }

  public void OnSmallImageStyleChanged(object sender, SelectionChangedEventArgs e)
  {
    UpdateVisibility();
  }

  public void OnLargeImageStyleChanged(object sender, SelectionChangedEventArgs e)
  {
    UpdateVisibility();
  }

  public void OnMainMenuPaneClick(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingPaneState) return;
    SetActivePane(ConfigPane.MainMenu);
  }

  public void OnInGamePaneClick(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingPaneState) return;
    SetActivePane(ConfigPane.InGame);
  }

  public void OnMainMenuFieldGotFocus(object sender, RoutedEventArgs e)
  {
    if (sender is TextBox field)
    {
      ExpandMainMenuField(field);
    }
  }

  public void OnMainMenuFieldLostFocus(object sender, RoutedEventArgs e)
  {
    if (sender is TextBox field)
    {
      CollapseMainMenuField(field);
    }
  }

  private void ExpandMainMenuField(TextBox field)
  {
    if (_expandedMainMenuField != null && _expandedMainMenuField != field)
    {
      CollapseMainMenuField(_expandedMainMenuField);
    }

    _expandedMainMenuField = field;
    field.TextWrapping = TextWrapping.Wrap;
    field.MinHeight = 72;
    field.Height = double.NaN;
  }

  private void CollapseMainMenuField(TextBox field)
  {
    field.TextWrapping = TextWrapping.NoWrap;
    field.MinHeight = 32;
    field.Height = double.NaN;

    if (_expandedMainMenuField == field)
    {
      _expandedMainMenuField = null;
    }
  }

  public void OnBossEventCheckChanged(object sender, RoutedEventArgs e)
  {
    UpdateVisibility();
  }

  // ── Mutual Exclusivity Handlers (Exclude vs Include) ─────────────────────

  public void OnExcludeEventsChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var exc = sender as CheckBox;
    if (exc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("IncludeEventsBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public void OnIncludeEventsChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var inc = sender as CheckBox;
    if (inc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("ExcludeEventsBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public void OnExcludeNonProgressiveChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var exc = sender as CheckBox;
    if (exc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("IncludeNonProgressiveBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public void OnIncludeNonProgressiveChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var inc = sender as CheckBox;
    if (inc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("ExcludeNonProgressiveBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public void OnExcludePeacefulChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var exc = sender as CheckBox;
    if (exc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("IncludePeacefulBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public void OnIncludePeacefulChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var inc = sender as CheckBox;
    if (inc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("ExcludePeacefulBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public void OnExcludeWeatherChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var exc = sender as CheckBox;
    if (exc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("IncludeWeatherBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public void OnIncludeWeatherChanged(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingCheckboxes) return;
    var inc = sender as CheckBox;
    if (inc?.IsChecked == true)
    {
      _isUpdatingCheckboxes = true;
      this.FindControl<CheckBox>("ExcludeWeatherBox")!.IsChecked = false;
      _isUpdatingCheckboxes = false;
    }
  }

  public async void OnVariablesClick(object sender, RoutedEventArgs e)
  {
    var dialog = new VariablesDialog();
    await dialog.ShowDialog(this);
  }

  private bool _isSaving = false;

  public async void OnSaveClick(object sender, RoutedEventArgs e)
  {
    if (_isSaving) return;
    _isSaving = true;

    var saveBtn = this.FindControl<Button>("SaveButton");
    if (saveBtn != null)
    {
      saveBtn.Content = "Saving Config...";
    }

    var config = ConfigManager.CurrentConfig ?? new RpcConfig();

    config.MainMenuLine1 = this.FindControl<TextBox>("MainMenuLine1Box")!.Text ?? "";
    config.MainMenuLine2 = this.FindControl<TextBox>("MainMenuLine2Box")!.Text ?? "";
    config.MainMenuSmallImageUrl = this.FindControl<TextBox>("MainMenuSmallImageUrlBox")!.Text ?? "";
    config.MainMenuSmallImageText = this.FindControl<TextBox>("MainMenuSmallImageTextBox")!.Text ?? "";
    config.MainMenuLargeImageUrl = this.FindControl<TextBox>("MainMenuLargeImageUrlBox")!.Text ?? "";
    config.MainMenuLargeImageText = this.FindControl<TextBox>("MainMenuLargeImageTextBox")!.Text ?? "";

    config.InGameLine1 = this.FindControl<TextBox>("Line1Box")!.Text ?? "";
    config.InGameLine2 = this.FindControl<TextBox>("Line2Box")!.Text ?? "";

    config.SmallImageStyleIndex = this.FindControl<ComboBox>("SmallImageStyleBox")!.SelectedIndex;
    config.SmallImageCustomUrl = this.FindControl<TextBox>("SmallImageCustomUrlBox")!.Text ?? "";
    config.SmallImageCustomText = this.FindControl<TextBox>("SmallImageCustomTextBox")!.Text ?? "";

    // Small Rotation Checkboxes
    config.SmallItemEnabled = this.FindControl<CheckBox>("SmallItemEnabledBox")!.IsChecked ?? true;
    config.SmallBossEventEnabled = this.FindControl<CheckBox>("SmallBossEventEnabledBox")!.IsChecked ?? false;

    config.ExcludeBoss = this.FindControl<CheckBox>("ExcludeBossBox")!.IsChecked ?? false;
    config.ExcludeEvents = this.FindControl<CheckBox>("ExcludeEventsBox")!.IsChecked ?? false;
    config.ExcludeNonProgressiveEvents = this.FindControl<CheckBox>("ExcludeNonProgressiveBox")!.IsChecked ?? false;
    config.ExcludePeacefulEvents = this.FindControl<CheckBox>("ExcludePeacefulBox")!.IsChecked ?? false;
    config.ExcludeWeather = this.FindControl<CheckBox>("ExcludeWeatherBox")!.IsChecked ?? false;

    config.IncludeEvents = this.FindControl<CheckBox>("IncludeEventsBox")!.IsChecked ?? false;
    config.IncludeNonProgressiveEvents = this.FindControl<CheckBox>("IncludeNonProgressiveBox")!.IsChecked ?? false;
    config.IncludePeacefulEvents = this.FindControl<CheckBox>("IncludePeacefulBox")!.IsChecked ?? false;
    config.IncludeWeather = this.FindControl<CheckBox>("IncludeWeatherBox")!.IsChecked ?? false;

    config.LargeImageStyleIndex = this.FindControl<ComboBox>("LargeImageStyleBox")!.SelectedIndex;
    config.LargeImageCustomUrl = this.FindControl<TextBox>("LargeImageCustomUrlBox")!.Text ?? "";
    config.LargeImageCustomText = this.FindControl<TextBox>("LargeImageCustomTextBox")!.Text ?? "";

    config.ClientId = this.FindControl<TextBox>("ClientIdBox")!.Text ?? "123456789012345678";

    ConfigManager.SaveConfig();

    if (saveBtn != null)
    {
      saveBtn.Content = "Configuration Saved!";
    }

    await System.Threading.Tasks.Task.Delay(2000);

    if (saveBtn != null)
    {
      saveBtn.Content = "Save Configuration";
    }

    _isSaving = false;
  }

  protected override void OnClosing(WindowClosingEventArgs e)
  {
    if (_isAppShutdownRequested)
    {
      base.OnClosing(e);
      return;
    }

    bool terrariaRunning = IsTerrariaRunning();
    if (terrariaRunning)
    {
      e.Cancel = true;
      Hide();
      Logger.Info("Window hidden to tray (Terraria still running).");
    }
    else
    {
      Logger.Info("Window closing — Terraria not running, exiting.");
      SingleInstance.Release();
      _isAppShutdownRequested = true;
      e.Cancel = true;
      if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
      {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
      }
      else
      {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Environment.Exit(0));
      }
    }
  }

  private static bool IsTerrariaRunning()
  {
    foreach (var process in Process.GetProcessesByName("Terraria"))
    {
      try
      {
        if (process.HasExited)
        {
          continue;
        }

        var fileName = process.MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
          return true;
        }

        if (string.Equals(Path.GetFileName(fileName), "Terraria.exe", StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }
      catch
      {
        return true;
      }
    }

    return false;
  }

  public void ShowAndBringToFront()
  {
    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
      Show();
      WindowState = WindowState.Normal;
      Activate();
    });
  }
}
