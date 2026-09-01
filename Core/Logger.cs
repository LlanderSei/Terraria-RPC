using System;
using System.Collections.Generic;
using System.IO;

namespace TerrariaRPC.Core
{
  /// <summary>
  /// Simple file-based logger. Writes timestamped lines to terraria-rpc.log
  /// next to the executable. Mirrors output to Console as well.
  /// </summary>
  public static class Logger
  {
    private static readonly string LogPath = Path.Combine(
      AppContext.BaseDirectory, "terraria-rpc.log"
    );

    private static readonly object _lock = new();
    private static readonly Dictionary<string, DateTime> _lastThrottledWriteUtc = new(StringComparer.OrdinalIgnoreCase);
    private static TimeSpan _throttleWindow = TimeSpan.FromSeconds(2);
    public static bool IsDebugEnabled { get; private set; } = false;

    public static void SetDebugEnabled(bool enabled)
    {
      IsDebugEnabled = enabled;
    }

    public static void SetThrottleWindow(TimeSpan window)
    {
      if (window < TimeSpan.Zero)
      {
        window = TimeSpan.Zero;
      }

      lock (_lock)
      {
        _throttleWindow = window;
      }
    }

    static Logger()
    {
      // Start fresh each run — rotate old log out
      if (File.Exists(LogPath))
      {
        string backup = Path.ChangeExtension(LogPath, ".prev.log");
        File.Copy(LogPath, backup, overwrite: true);
        File.Delete(LogPath);
      }

      Write("INFO", $"=== TerrariaRPC started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
      Write("INFO", $"Log file: {LogPath}");
    }

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Debug(string message)
    {
      if (!IsDebugEnabled) return;
      Write("DEBUG", message);
    }

    public static void DebugThrottled(string key, string message)
    {
      if (!IsDebugEnabled) return;
      WriteThrottled("DEBUG", key, message);
    }

    public static void InfoThrottled(string key, string message)
      => WriteThrottled("INFO ", key, message);

    public static void WarnThrottled(string key, string message)
      => WriteThrottled("WARN ", key, message);

    public static void ErrorThrottled(string key, string message)
      => WriteThrottled("ERROR", key, message);

    private static void Write(string level, string message)
    {
      string line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
      Console.WriteLine(line);
      lock (_lock)
      {
        try { File.AppendAllText(LogPath, line + Environment.NewLine); }
        catch { /* never crash on logging failure */ }
      }
    }

    private static void WriteThrottled(string level, string key, string message)
    {
      if (string.IsNullOrWhiteSpace(key))
      {
        Write(level, message);
        return;
      }

      DateTime now = DateTime.UtcNow;
      lock (_lock)
      {
        if (_throttleWindow > TimeSpan.Zero &&
            _lastThrottledWriteUtc.TryGetValue(key, out DateTime lastWrite) &&
            now - lastWrite < _throttleWindow)
        {
          return;
        }

        _lastThrottledWriteUtc[key] = now;
      }

      Write(level, message);
    }
  }
}
