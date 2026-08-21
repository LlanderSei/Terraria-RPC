using Avalonia;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TerrariaRPC.Core;

namespace TerrariaRPC
{
  class Program
  {
    private static volatile bool _terrariaEverDetected = false;
    private static volatile int _consecutiveMisses = 0;
    private const int MaxMisses = 3;
    private static readonly TimeSpan ReaderInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PresenceInterval = TimeSpan.FromSeconds(5);

    [STAThread]
    public static void Main(string[] args)
    {
      Logger.Info($"Args received: {string.Join(", ", args)}");

      ConfigManager.LoadConfig();

      bool noGui = args.Any(a => a.Equals("--no-gui", StringComparison.OrdinalIgnoreCase))
        || Environment.GetEnvironmentVariable("TERRARIARPC_HEADLESS") == "1";

      App.IsHeadless = noGui;
      Logger.Info(noGui ? "Mode: Headless" : "Mode: GUI");

      bool isFirst = SingleInstance.TryAcquire();
      if (!isFirst)
      {
        Logger.Info("Another instance is already running - sending 'show' command and exiting.");
        SingleInstance.SendShowCommand();
        return;
      }

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
          Logger.Info("Terraria not found after 3 attempts - exiting headless mode.");
          SingleInstance.Release();
          return;
        }
      }

      var cancellationSource = new CancellationTokenSource();
      var memoryReader = new TerrariaMemoryReader();
      Task readerTask = ReaderLoopAsync(memoryReader, cancellationSource.Token);
      Task presenceTask = PresenceLoopAsync(noGui, memoryReader, cancellationSource);

      Logger.Info("Starting Avalonia Framework...");
      try
      {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
      }
      finally
      {
        cancellationSource.Cancel();

        try
        {
          Task.WaitAll([readerTask, presenceTask], TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex)
        {
          foreach (var inner in ex.Flatten().InnerExceptions)
            Logger.Warn($"Background task shutdown issue: {inner.Message}");
        }
        catch (Exception ex)
        {
          Logger.Warn($"Background task shutdown issue: {ex.Message}");
        }

        SingleInstance.Release();
        Logger.Info("Application exited.");
      }
    }

    private static async Task ReaderLoopAsync(TerrariaMemoryReader memoryReader, CancellationToken cancellationToken)
    {
      Logger.Info("Reader loop started.");
      using var timer = new PeriodicTimer(ReaderInterval);

      try
      {
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
          try
          {
            memoryReader.Update();
          }
          catch (Exception ex)
          {
            Logger.Error($"Reader loop error: {ex.Message}");
          }
        }
      }
      catch (OperationCanceledException)
      {
      }

      Logger.Info("Reader loop stopped.");
    }

    private static async Task PresenceLoopAsync(bool noGui, TerrariaMemoryReader memoryReader, CancellationTokenSource cancellationSource)
    {
      var iconManager = new IconManager();
      using var rpcManager = new DiscordRpcManager(iconManager);
      Logger.Info(noGui ? "Presence loop started (headless mode)." : "Presence loop started (GUI mode).");
      using var timer = new PeriodicTimer(PresenceInterval);

      try
      {
        while (await timer.WaitForNextTickAsync(cancellationSource.Token).ConfigureAwait(false))
        {
          try
          {
            TerrariaGameState snapshot = memoryReader.GetStateSnapshot();
            rpcManager.UpdatePresence(snapshot, ConfigManager.CurrentConfig);

            if (noGui)
            {
              if (snapshot.IsAttached)
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
                  Logger.Info("Terraria closed - headless mode exiting after 3 missed connections.");
                  cancellationSource.Cancel();
                  Dispatcher.UIThread.Post(() =>
                  {
                    SingleInstance.Release();
                    Environment.Exit(0);
                  });
                  break;
                }
              }
            }
          }
          catch (Exception ex)
          {
            Logger.Error($"Presence loop error: {ex.Message}");
          }
        }
      }
      catch (OperationCanceledException)
      {
      }

      Logger.Info("Presence loop stopped.");
    }

    public static AppBuilder BuildAvaloniaApp()
      => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
  }
}
