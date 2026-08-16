using System;
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
    public static void Debug(string message) => Write("DEBUG", message);

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
  }
}
