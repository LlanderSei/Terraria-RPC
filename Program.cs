using Avalonia;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using TerrariaRPC.Core;

namespace TerrariaRPC;

class Program
{
  private static volatile bool keepRunning = true;

  // Headless mode: track Terraria connection state for exit detection
  private static volatile bool _terrariaEverDetected = false;
  private static volatile int  _consecutiveMisses    = 0;
  private const int MaxMisses = 3;

  [STAThread]
  public static void Main(string[] args)
  {
    Logger.Info($"Args received: {string.Join(", ", args)}");

    ConfigManager.LoadConfig();

    bool noGui = args.Any(a => a.Equals("--no-gui", StringComparison.OrdinalIgnoreCase))
                 || Environment.GetEnvironmentVariable("TERRARIARPC_HEADLESS") == "1";

    Logger.Info(noGui ? "Mode: Headless" : "Mode: GUI");

    // ── Single-instance check ────────────────────────────────────────────────
    bool isFirst = SingleInstance.TryAcquire();
    if (!isFirst)
    {
      Logger.Info("Another instance is already running — sending 'show' command and exiting.");
      SingleInstance.SendShowCommand();
      return;
    }

    // ── Headless: startup Terraria poll (3 attempts, 5s apart) ───────────────
    if (noGui)
    {
      Logger.Info("Headless: waiting for Terraria to start (up to 3 attempts, 5s apart)...");
      bool found = false;
      for (int attempt = 1; attempt <= MaxMisses; attempt++)
      {
        if (Process.GetProcessesByName("Terraria").Any())
        {
          Logger.Info($"Terraria detected on attempt {attempt}.");
          found = true;
          break;
        }
        Logger.Info($"Attempt {attempt}/{MaxMisses}: Terraria not found. Waiting 5s...");
        Thread.Sleep(5000);
      }

      if (!found)
      {
        Logger.Info("Terraria not found after 3 attempts — exiting headless mode.");
        SingleInstance.Release();
        return;
      }
    }

    // ── Start RPC loop ───────────────────────────────────────────────────────
    Thread rpcThread = new Thread(noGui ? (ThreadStart)HeadlessRpcLoop : RpcLoop);
    rpcThread.IsBackground = true;
    rpcThread.Name = "RPC-Loop";
    rpcThread.Start();

    if (noGui)
    {
      Logger.Info("Running headless. Press Ctrl+C to exit.");
      Console.CancelKeyPress += (sender, e) =>
      {
        e.Cancel = true;
        keepRunning = false;
        Logger.Info("Ctrl+C received — shutting down...");
      };

      while (keepRunning)
        Thread.Sleep(100);

      SingleInstance.Release();
      Logger.Info("Headless mode exited cleanly.");
    }
    else
    {
      Logger.Info("Starting Avalonia GUI...");
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
      keepRunning = false;
      SingleInstance.Release();
      Logger.Info("GUI closed.");
    }
  }

  /// <summary>
  /// RPC loop for GUI mode — runs indefinitely, no exit logic needed
  /// (the GUI window handles shutting down).
  /// </summary>
  private static void RpcLoop()
  {
    var iconManager  = new IconManager();
    var memoryReader = new TerrariaMemoryReader();
    using var rpcManager = new DiscordRpcManager(iconManager);

    Logger.Info("RPC loop started (GUI mode).");

    while (keepRunning)
    {
      try
      {
        memoryReader.Update();
        rpcManager.UpdatePresence(memoryReader.CurrentState, ConfigManager.CurrentConfig);
      }
      catch (Exception ex)
      {
        Logger.Error($"RPC loop error: {ex.Message}");
      }

      for (int i = 0; i < 50 && keepRunning; i++)
        Thread.Sleep(100);
    }

    Logger.Info("RPC loop stopped.");
  }

  /// <summary>
  /// RPC loop for headless mode — also watches for Terraria exiting:
  /// after 3 consecutive missed connections (post first detection), exits the app.
  /// </summary>
  private static void HeadlessRpcLoop()
  {
    var iconManager  = new IconManager();
    var memoryReader = new TerrariaMemoryReader();
    using var rpcManager = new DiscordRpcManager(iconManager);

    Logger.Info("RPC loop started (headless mode).");

    while (keepRunning)
    {
      try
      {
        memoryReader.Update();
        rpcManager.UpdatePresence(memoryReader.CurrentState, ConfigManager.CurrentConfig);

        if (memoryReader.IsConnected)
        {
          _terrariaEverDetected = true;
          _consecutiveMisses = 0;
        }
        else if (_terrariaEverDetected)
        {
          _consecutiveMisses++;
          Logger.Info($"Terraria connection lost ({_consecutiveMisses}/{MaxMisses})...");

          if (_consecutiveMisses >= MaxMisses)
          {
            Logger.Info("Terraria closed — headless mode exiting after 3 missed connections.");
            keepRunning = false;
            break;
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Error($"RPC loop error: {ex.Message}");
      }

      for (int i = 0; i < 50 && keepRunning; i++)
        Thread.Sleep(100);
    }

    Logger.Info("Headless RPC loop stopped.");
  }

  public static AppBuilder BuildAvaloniaApp()
      => AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()
          .LogToTrace();
}
