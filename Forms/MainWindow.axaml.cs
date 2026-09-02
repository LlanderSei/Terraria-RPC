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
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Layout;
using TerrariaRPC.Core;

namespace TerrariaRPC.Forms;

public partial class MainWindow : Window
{
  private enum ConfigPane
  {
    MainMenu,
    InGame
  }

  private bool _isUpdatingPaneState = false;
  private bool _isAppShutdownRequested = false;
  private bool _isNormalizingSingleLineText = false;
  private TextBox? _expandedMainMenuField;
  private bool _isRefreshingSmallStatusRows = false;

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
      this.FindControl<TextBox>("MainMenuLine1Box")!.Text = config.MainMenuLine1;
      this.FindControl<TextBox>("MainMenuLine2Box")!.Text = config.MainMenuLine2;
      this.FindControl<TextBox>("MainMenuSmallImageUrlBox")!.Text = config.MainMenuSmallImageUrl;
      this.FindControl<TextBox>("MainMenuSmallImageTextBox")!.Text = config.MainMenuSmallImageText;
      this.FindControl<TextBox>("MainMenuLargeImageUrlBox")!.Text = config.MainMenuLargeImageUrl;
      this.FindControl<TextBox>("MainMenuLargeImageTextBox")!.Text = config.MainMenuLargeImageText;

      var inGameLine1Box = this.FindControl<TextBox>("Line1Box");
      var inGameLine2Box = this.FindControl<TextBox>("Line2Box");
      inGameLine1Box?.Text = config.InGameLine1;
      inGameLine2Box?.Text = config.InGameLine2;

      this.FindControl<ComboBox>("SmallImageStyleBox")!.SelectedIndex = config.SmallImageStyleIndex;
      this.FindControl<TextBox>("SmallImageCustomUrlBox")!.Text = config.SmallImageCustomUrl;
      this.FindControl<TextBox>("SmallImageCustomTextBox")!.Text = config.SmallImageCustomText;

      this.FindControl<ComboBox>("LargeImageStyleBox")!.SelectedIndex = config.LargeImageStyleIndex;
      this.FindControl<TextBox>("LargeImageCustomUrlBox")!.Text = config.LargeImageCustomUrl;
      this.FindControl<TextBox>("LargeImageCustomTextBox")!.Text = config.LargeImageCustomText;

      this.FindControl<TextBox>("UpdateIntervalBox")!.Text = Math.Max(1, config.UpdateInterval).ToString();
      this.FindControl<TextBox>("ClientIdBox")!.Text = config.ClientId;

      SetActivePane(ConfigPane.MainMenu);
      UpdateVisibility();
      RefreshSmallStatusRows();
    }
  }

  private void SetActivePane(ConfigPane pane)
  {
    if (_isUpdatingPaneState) return;

    var mainMenuPanel = this.FindControl<Panel>("MainMenuPanel");
    var inGamePanel = this.FindControl<Panel>("InGamePanel");
    mainMenuPanel?.IsVisible = pane == ConfigPane.MainMenu;
    inGamePanel?.IsVisible = pane == ConfigPane.InGame;

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

    var smallStatusListPane = this.FindControl<StackPanel>("SmallStatusListPane");
    smallStatusListPane?.IsVisible = isSmallRotation;

    var smallUrlLabel = this.FindControl<TextBlock>("SmallImageCustomUrlLabel");
    var smallUrlBox = this.FindControl<TextBox>("SmallImageCustomUrlBox");
    var smallTextLabel = this.FindControl<TextBlock>("SmallImageCustomTextLabel");
    var smallTextBox = this.FindControl<TextBox>("SmallImageCustomTextBox");

    smallUrlLabel?.IsVisible = isSmallCustom;
    smallUrlBox?.IsVisible = isSmallCustom;
    smallTextLabel?.IsVisible = isSmallCustom;
    smallTextBox?.IsVisible = isSmallCustom;

    var largeStyleBox = this.FindControl<ComboBox>("LargeImageStyleBox");
    bool isLargeCustom = largeStyleBox != null && largeStyleBox.SelectedIndex == 1;

    var largeUrlLabel = this.FindControl<TextBlock>("LargeImageCustomUrlLabel");
    var largeUrlBox = this.FindControl<TextBox>("LargeImageCustomUrlBox");
    var largeTextLabel = this.FindControl<TextBlock>("LargeImageCustomTextLabel");
    var largeTextBox = this.FindControl<TextBox>("LargeImageCustomTextBox");

    largeUrlLabel?.IsVisible = isLargeCustom;
    largeUrlBox?.IsVisible = isLargeCustom;
    largeTextLabel?.IsVisible = isLargeCustom;
    largeTextBox?.IsVisible = isLargeCustom;
  }

  public void OnSmallImageStyleChanged(object sender, SelectionChangedEventArgs e)
  {
    UpdateVisibility();
    RefreshSmallStatusRows();
  }

  public void OnLargeImageStyleChanged(object sender, SelectionChangedEventArgs e)
  {
    UpdateVisibility();
  }

  public async void OnBossEventPriorityClick(object? sender, RoutedEventArgs e)
  {
    var dialog = new SmallDetailsTemplatesDialog();
    await dialog.ShowDialog(this);
  }

  public void OnSingleLineTextChanged(object sender, TextChangedEventArgs e)
  {
    if (_isNormalizingSingleLineText) return;
    if (sender is not TextBox textBox) return;

    string currentText = textBox.Text ?? "";
    if (!currentText.Contains('\r') && !currentText.Contains('\n'))
      return;

    string normalizedText = currentText
      .Replace("\r\n", " ")
      .Replace('\r', ' ')
      .Replace('\n', ' ');

    if (normalizedText == currentText)
      return;

    _isNormalizingSingleLineText = true;
    textBox.Text = normalizedText;
    _isNormalizingSingleLineText = false;
  }

  public async void OnSingleLinePastingFromClipboard(object sender, RoutedEventArgs e)
  {
    if (sender is not TextBox textBox)
      return;

    var clipboard = Avalonia.Controls.TopLevel.GetTopLevel(textBox)?.Clipboard;
    if (clipboard == null)
      return;

    string? pastedText = await clipboard.TryGetTextAsync();
    if (string.IsNullOrEmpty(pastedText))
      return;

    string normalizedText = pastedText
      .Replace("\r\n", " ")
      .Replace('\r', ' ')
      .Replace('\n', ' ');

    int selectionStart = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
    int selectionEnd = Math.Max(textBox.SelectionStart, textBox.SelectionEnd);
    string currentText = textBox.Text ?? "";

    if (selectionStart < 0) selectionStart = 0;
    if (selectionEnd < selectionStart) selectionEnd = selectionStart;
    if (selectionStart > currentText.Length) selectionStart = currentText.Length;
    if (selectionEnd > currentText.Length) selectionEnd = currentText.Length;

    string newText = currentText[..selectionStart] + normalizedText + currentText[selectionEnd..];

    _isNormalizingSingleLineText = true;
    textBox.Text = newText;
    textBox.CaretIndex = selectionStart + normalizedText.Length;
    textBox.SelectionStart = textBox.CaretIndex;
    textBox.SelectionEnd = textBox.CaretIndex;
    _isNormalizingSingleLineText = false;
    e.Handled = true;
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

  public void OnVariablesClick(object sender, RoutedEventArgs e)
  {
    VariablesDialog.ShowOrActivate(this);
  }

  public void OnUpdateIntervalTextChanged(object sender, TextChangedEventArgs e)
  {
    if (_isNormalizingSingleLineText || sender is not TextBox textBox)
      return;

    string digitsOnly = new((textBox.Text ?? "").Where(char.IsDigit).ToArray());
    if (digitsOnly == textBox.Text)
      return;

    int caretIndex = Math.Min(textBox.CaretIndex, digitsOnly.Length);
    _isNormalizingSingleLineText = true;
    textBox.Text = digitsOnly;
    textBox.CaretIndex = caretIndex;
    _isNormalizingSingleLineText = false;
  }

  public async void OnUpdateIntervalPastingFromClipboard(object sender, RoutedEventArgs e)
  {
    if (sender is not TextBox textBox)
      return;

    var clipboard = Avalonia.Controls.TopLevel.GetTopLevel(textBox)?.Clipboard;
    string? pastedText = clipboard == null ? null : await clipboard.TryGetTextAsync();
    if (string.IsNullOrEmpty(pastedText))
      return;

    string digitsOnly = new(pastedText.Where(char.IsDigit).ToArray());
    int selectionStart = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
    int selectionEnd = Math.Max(textBox.SelectionStart, textBox.SelectionEnd);
    string currentText = textBox.Text ?? "";
    selectionStart = Math.Clamp(selectionStart, 0, currentText.Length);
    selectionEnd = Math.Clamp(selectionEnd, selectionStart, currentText.Length);
    string newText = currentText[..selectionStart] + digitsOnly + currentText[selectionEnd..];

    _isNormalizingSingleLineText = true;
    textBox.Text = newText;
    textBox.CaretIndex = selectionStart + digitsOnly.Length;
    textBox.SelectionStart = textBox.CaretIndex;
    textBox.SelectionEnd = textBox.CaretIndex;
    _isNormalizingSingleLineText = false;
    e.Handled = true;
  }

  private bool _isSaving = false;

  public async void OnSaveClick(object sender, RoutedEventArgs e)
  {
    if (_isSaving) return;
    _isSaving = true;

    var saveBtn = this.FindControl<Button>("SaveButton");
    saveBtn?.Content = "Saving Config...";

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
    config.LargeImageStyleIndex = this.FindControl<ComboBox>("LargeImageStyleBox")!.SelectedIndex;
    config.LargeImageCustomUrl = this.FindControl<TextBox>("LargeImageCustomUrlBox")!.Text ?? "";
    config.LargeImageCustomText = this.FindControl<TextBox>("LargeImageCustomTextBox")!.Text ?? "";

    if (!int.TryParse(this.FindControl<TextBox>("UpdateIntervalBox")!.Text, out int updateInterval))
    {
      updateInterval = 2;
    }
    config.UpdateInterval = Math.Max(1, updateInterval);
    config.ClientId = this.FindControl<TextBox>("ClientIdBox")!.Text ?? "123456789012345678";

    ConfigManager.SaveConfig();

    saveBtn?.Content = "Configuration Saved!";

    await System.Threading.Tasks.Task.Delay(2000);

    saveBtn?.Content = "Save Configuration";

    _isSaving = false;
  }

  private void RefreshSmallStatusRows()
  {
    if (_isRefreshingSmallStatusRows)
    {
      return;
    }

    _isRefreshingSmallStatusRows = true;

    var rowsPanel = this.FindControl<StackPanel>("SmallStatusRowsPanel");
    if (rowsPanel != null)
    {
      rowsPanel.Children.Clear();

      var templates = ConfigManager.CurrentConfig.InGame.SmallDetails.Templates;
      for (int i = 0; i < templates.Count; i++)
      {
        rowsPanel.Children.Add(BuildSmallStatusRow(i, templates[i]));
      }
    }

    _isRefreshingSmallStatusRows = false;
  }

  private Control BuildSmallStatusRow(int index, StatusTemplateEntry entry)
  {
    var row = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("44,86,*,Auto"),
      Margin = new Thickness(0)
    };

    var moveStack = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 2,
      VerticalAlignment = VerticalAlignment.Center,
      Margin = new Thickness(0, 0, 8, 0)
    };

    var upButton = new Button
    {
      Content = " ↑ ",
      Padding = new Thickness(0),
      Width = 22,
      Height = 22,
      Tag = index,
      IsEnabled = index > 0
    };
    upButton.Click += OnMoveSmallStatusUpClick;
    ToolTip.SetTip(upButton, "Move Up");

    var downButton = new Button
    {
      Content = " ↓ ",
      Padding = new Thickness(0),
      Width = 22,
      Height = 22,
      Tag = index,
      IsEnabled = index < ConfigManager.CurrentConfig.InGame.SmallDetails.Templates.Count - 1
    };
    downButton.Click += OnMoveSmallStatusDownClick;
    ToolTip.SetTip(downButton, "Move Down");

    moveStack.Children.Add(upButton);
    moveStack.Children.Add(downButton);
    Grid.SetColumn(moveStack, 0);
    row.Children.Add(moveStack);

    var enabledBox = new CheckBox
    {
      IsChecked = entry.Enabled,
      VerticalAlignment = VerticalAlignment.Center,
      Margin = new Thickness(6, 0, 0, 0),
      Tag = entry
    };
    enabledBox.IsCheckedChanged += OnSmallStatusEnabledChanged;
    Grid.SetColumn(enabledBox, 1);
    row.Children.Add(enabledBox);

    var nameText = new TextBlock
    {
      Text = string.IsNullOrWhiteSpace(entry.StatusName) ? "(Unnamed status)" : entry.StatusName,
      VerticalAlignment = VerticalAlignment.Center,
      TextWrapping = TextWrapping.NoWrap
    };
    Grid.SetColumn(nameText, 2);
    row.Children.Add(nameText);

    var actionStack = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6,
      HorizontalAlignment = HorizontalAlignment.Right
    };

    var editButton = new Button
    {
      Content = "Edit",
      Tag = entry
    };
    editButton.Click += OnEditSmallStatusClick;

    var deleteButton = new Button
    {
      Content = "Delete",
      Tag = entry
    };
    deleteButton.Click += OnDeleteSmallStatusClick;

    actionStack.Children.Add(editButton);
    actionStack.Children.Add(deleteButton);
    Grid.SetColumn(actionStack, 3);
    row.Children.Add(actionStack);

    return row;
  }

  public void OnAddSmallStatusClick(object? sender, RoutedEventArgs e)
  {
    _ = OpenSmallStatusEditorAsync(null);
  }

  private async System.Threading.Tasks.Task OpenSmallStatusEditorAsync(StatusTemplateEntry? entry)
  {
    var templates = ConfigManager.CurrentConfig.InGame.SmallDetails.Templates;
    var working = entry != null
      ? new StatusTemplateEntry
      {
        StatusName = entry.StatusName,
        Image = entry.Image,
        Text = entry.Text,
        Enabled = entry.Enabled
      }
      : new StatusTemplateEntry
      {
        StatusName = "New Status",
        Enabled = true
      };

    var dialog = entry == null
      ? new StatusTemplateDialog("Add Status", working.StatusName, working.Image, working.Text)
      : new StatusTemplateDialog(working.StatusName, working.Image, working.Text);

    await dialog.ShowDialog(this);

    if (!dialog.WasSaved)
    {
      return;
    }

    working.StatusName = dialog.StatusName;
    working.Image = dialog.ImageTemplate;
    working.Text = dialog.TextTemplate;

    if (entry == null)
    {
      templates.Add(working);
    }
    else
    {
      int index = templates.IndexOf(entry);
      if (index >= 0)
      {
        templates[index] = working;
      }
    }

    RefreshSmallStatusRows();
  }

  public async void OnEditSmallStatusClick(object? sender, RoutedEventArgs e)
  {
    if (sender is Button button && button.Tag is StatusTemplateEntry entry)
    {
      await OpenSmallStatusEditorAsync(entry);
    }
  }

  public void OnDeleteSmallStatusClick(object? sender, RoutedEventArgs e)
  {
    if (sender is Button button && button.Tag is StatusTemplateEntry entry)
    {
      _ = ConfirmDeleteSmallStatusAsync(entry);
    }
  }

  private async System.Threading.Tasks.Task ConfirmDeleteSmallStatusAsync(StatusTemplateEntry entry)
  {
    string statusName = string.IsNullOrWhiteSpace(entry.StatusName) ? "this status" : $"\"{entry.StatusName}\"";
    var dialog = new ConfirmationDialog(
      $"Delete {statusName}?",
      "This will remove the status from the rotation list.");

    bool confirmed = await dialog.ShowDialog<bool>(this);
    if (!confirmed)
    {
      return;
    }

    ConfigManager.CurrentConfig.InGame.SmallDetails.Templates.Remove(entry);
    RefreshSmallStatusRows();
  }

  public void OnSmallStatusEnabledChanged(object? sender, RoutedEventArgs e)
  {
    if (sender is CheckBox checkBox && checkBox.Tag is StatusTemplateEntry entry)
    {
      entry.Enabled = checkBox.IsChecked == true;
    }
  }

  public void OnMoveSmallStatusUpClick(object? sender, RoutedEventArgs e)
  {
    if (sender is Button button && button.Tag is int index)
    {
      var templates = ConfigManager.CurrentConfig.InGame.SmallDetails.Templates;
      if (index <= 0 || index >= templates.Count)
      {
        return;
      }

      (templates[index - 1], templates[index]) = (templates[index], templates[index - 1]);
      RefreshSmallStatusRows();
    }
  }

  public void OnMoveSmallStatusDownClick(object? sender, RoutedEventArgs e)
  {
    if (sender is Button button && button.Tag is int index)
    {
      var templates = ConfigManager.CurrentConfig.InGame.SmallDetails.Templates;
      if (index < 0 || index >= templates.Count - 1)
      {
        return;
      }

      (templates[index], templates[index + 1]) = (templates[index + 1], templates[index]);
      RefreshSmallStatusRows();
    }
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

