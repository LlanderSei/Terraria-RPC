using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace TerrariaRPC;

public partial class App : Application
{
  private TrayIcon? _trayIcon;
  private MainWindow? _mainWindow;

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      _mainWindow = new MainWindow();
      desktop.MainWindow = _mainWindow;

      SetupTrayIcon();

      // IPC: when another instance sends "show", restore the window
      Core.SingleInstance.StartIpcServer(() => _mainWindow.ShowAndBringToFront());

      // Clean up tray icon on exit
      desktop.Exit += (_, _) => _trayIcon?.Dispose();
    }

    base.OnFrameworkInitializationCompleted();
  }

  private void SetupTrayIcon()
  {
    _trayIcon = new TrayIcon();
    _trayIcon.ToolTipText = "Terraria RPC";

    // Load icon from embedded resource
    try
    {
      var uri = new Uri("avares://TerrariaRPC/Assets/tray.png");
      using var stream = AssetLoader.Open(uri);
      _trayIcon.Icon = new WindowIcon(stream);
    }
    catch (Exception ex)
    {
      Core.Logger.Error($"Tray icon load failed: {ex.Message}");
    }

    // Build tray menu
    var menu = new NativeMenu();

    var showItem = new NativeMenuItem("Show");
    showItem.Click += (_, _) => _mainWindow?.ShowAndBringToFront();
    menu.Add(showItem);

    menu.Add(new NativeMenuItemSeparator());

    var exitItem = new NativeMenuItem("Exit");
    exitItem.Click += (_, _) =>
    {
      Core.Logger.Info("Exit via tray icon.");
      Core.SingleInstance.Release();
      Environment.Exit(0);
    };
    menu.Add(exitItem);

    _trayIcon.Menu = menu;
    _trayIcon.IsVisible = true;

    // Also let double-clicking the tray icon show the window
    _trayIcon.Clicked += (_, _) => _mainWindow?.ShowAndBringToFront();
  }

}