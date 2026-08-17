using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrariaRPC.Core;

namespace TerrariaRPC;

public partial class MainWindow : Window
{
  private bool _isUpdatingCheckboxes = false;

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

      this.FindControl<TextBox>("Line1Box")!.Text = config.Line1;
      this.FindControl<TextBox>("Line2Box")!.Text = config.Line2;

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
      UpdateVisibility();
    }
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

  public void OnSaveClick(object sender, RoutedEventArgs e)
  {
    var config = ConfigManager.CurrentConfig ?? new RpcConfig();

    config.Line1 = this.FindControl<TextBox>("Line1Box")!.Text ?? "";
    config.Line2 = this.FindControl<TextBox>("Line2Box")!.Text ?? "";

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
  }

  protected override void OnClosing(WindowClosingEventArgs e)
  {
    bool terrariaRunning = Process.GetProcessesByName("Terraria").Any();
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
      base.OnClosing(e);
    }
  }

  public void ShowAndBringToFront()
  {
    Dispatcher.UIThread.Post(() =>
    {
      Show();
      WindowState = WindowState.Normal;
      Activate();
    });
  }
}