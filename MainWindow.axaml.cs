using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrariaRPC.Core;

namespace TerrariaRPC;

public partial class MainWindow : Window
{
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
      this.FindControl<TextBox>("Line1Box")!.Text = config.Line1;
      this.FindControl<TextBox>("Line2Box")!.Text = config.Line2;

      this.FindControl<ComboBox>("SmallImageStyleBox")!.SelectedIndex = config.SmallImageStyleIndex;
      this.FindControl<TextBox>("SmallImageCustomUrlBox")!.Text = config.SmallImageCustomUrl;
      this.FindControl<TextBox>("SmallImageCustomTextBox")!.Text = config.SmallImageCustomText;

      this.FindControl<ComboBox>("LargeImageStyleBox")!.SelectedIndex = config.LargeImageStyleIndex;
      this.FindControl<TextBox>("LargeImageCustomUrlBox")!.Text = config.LargeImageCustomUrl;
      this.FindControl<TextBox>("LargeImageCustomTextBox")!.Text = config.LargeImageCustomText;

      this.FindControl<TextBox>("ClientIdBox")!.Text = config.ClientId;

      UpdateVisibility();
    }
  }

  private void UpdateVisibility()
  {
    var smallStyleBox = this.FindControl<ComboBox>("SmallImageStyleBox");
    bool isSmallCustom = smallStyleBox != null && smallStyleBox.SelectedIndex == 1;

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

    config.LargeImageStyleIndex = this.FindControl<ComboBox>("LargeImageStyleBox")!.SelectedIndex;
    config.LargeImageCustomUrl = this.FindControl<TextBox>("LargeImageCustomUrlBox")!.Text ?? "";
    config.LargeImageCustomText = this.FindControl<TextBox>("LargeImageCustomTextBox")!.Text ?? "";

    config.ClientId = this.FindControl<TextBox>("ClientIdBox")!.Text ?? "123456789012345678";

    ConfigManager.SaveConfig();
  }

  /// <summary>
  /// While Terraria is running: minimize to tray instead of closing.
  /// Once Terraria is gone: allow normal exit.
  /// </summary>
  protected override void OnClosing(WindowClosingEventArgs e)
  {
    bool terrariaRunning = Process.GetProcessesByName("Terraria").Any();
    if (terrariaRunning)
    {
      // Cancel the close and hide instead — app stays in tray
      e.Cancel = true;
      Hide();
      Logger.Info("Window hidden to tray (Terraria still running).");
    }
    else
    {
      // Terraria is gone — allow the close and release single-instance mutex
      Logger.Info("Window closing — Terraria not running, exiting.");
      SingleInstance.Release();
      base.OnClosing(e);
    }
  }

  /// <summary>Called by the tray icon or IPC "show" command to restore the window.</summary>
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