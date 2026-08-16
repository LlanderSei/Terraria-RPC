using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace TerrariaRPC.Core
{
  /// <summary>
  /// Manages single-instance enforcement via a named Mutex,
  /// and IPC via a named pipe so that a second launch can
  /// signal the first instance to show its window.
  /// </summary>
  public static class SingleInstance
  {
    private const string MutexName = "TerrariaRPC_SingleInstance";
    private const string PipeName  = "TerrariaRPC_IPC";

    private static Mutex? _mutex;

    /// <summary>
    /// Try to acquire the single-instance mutex.
    /// Returns true if THIS is the first instance.
    /// Returns false if another instance already owns the mutex.
    /// </summary>
    public static bool TryAcquire()
    {
      _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
      if (!createdNew)
      {
        // Another instance owns the mutex — we are a second launch.
        _mutex.Dispose();
        _mutex = null;
      }
      return createdNew;
    }

    /// <summary>
    /// Sends the "show" IPC command to the running instance.
    /// Call this from the second instance before exiting.
    /// </summary>
    public static void SendShowCommand()
    {
      try
      {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        client.Connect(2000); // 2s timeout
        using var writer = new StreamWriter(client);
        writer.WriteLine("show");
        writer.Flush();
        Logger.Info("IPC: sent 'show' to existing instance.");
      }
      catch (Exception ex)
      {
        Logger.Error($"IPC send failed: {ex.Message}");
      }
    }

    /// <summary>
    /// Starts the IPC server loop in a background thread.
    /// When the "show" command is received, <paramref name="onShow"/> is invoked.
    /// </summary>
    public static void StartIpcServer(Action onShow)
    {
      var thread = new Thread(() =>
      {
        while (true)
        {
          try
          {
            using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
            server.WaitForConnection();
            using var reader = new StreamReader(server);
            var cmd = reader.ReadLine();
            if (cmd?.Trim().Equals("show", StringComparison.OrdinalIgnoreCase) == true)
            {
              Logger.Info("IPC: received 'show' command.");
              onShow();
            }
          }
          catch (Exception ex)
          {
            Logger.Error($"IPC server error: {ex.Message}");
          }
        }
      });
      thread.IsBackground = true;
      thread.Name = "IPC-Server";
      thread.Start();
    }

    public static void Release()
    {
      try { _mutex?.ReleaseMutex(); } catch { }
      _mutex?.Dispose();
      _mutex = null;
    }
  }
}
