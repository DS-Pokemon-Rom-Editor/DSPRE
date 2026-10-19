using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Text;

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

 public static class AppLogger
    {
        private static readonly object FileLock = new object();
        private static readonly ConcurrentQueue<string> RecentLogBuffer = new ConcurrentQueue<string>();
        private static string _logFilePath;
        private const int MaxLogFileLines = 2000;
        private static int _writesSinceLastTrim = 0;
        private const int TrimInterval = 10; 
        public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;


        public static void Initialize(string logFileName = "application.log", LogLevel minLevel = LogLevel.Debug)
        {
            string logDir = Path.Combine(AppPaths.DspreDataPath, "Logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, logFileName);
            MinimumLevel = minLevel;
            Log(LogLevel.Info, "Logger initialized.");
        }

        public static void Log(LogLevel level, string message)
        {
            if (level < MinimumLevel)
                return;

            string timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpper()}] {message}";

            if (_logFilePath != null)   // not Initialize()d yet → in-memory buffer only
            {
                lock (FileLock)
                {
                    File.AppendAllText(_logFilePath, timestamped + Environment.NewLine);

                    _writesSinceLastTrim++;
                    if (_writesSinceLastTrim >= TrimInterval)
                    {
                        TrimLogFile();
                        _writesSinceLastTrim = 0;
                    }
                }
            }

            RecentLogBuffer.Enqueue(timestamped);
            while (RecentLogBuffer.Count > MaxLogFileLines && RecentLogBuffer.TryDequeue(out _)) { }
        }

        private static void TrimLogFile()
        {
            if (!File.Exists(_logFilePath)) return;
    
            string[] lines = File.ReadAllLines(_logFilePath);
            if (lines.Length > MaxLogFileLines)
            {
                System.Collections.Generic.IEnumerable<string> trimmedLines = lines.Skip(lines.Length - MaxLogFileLines);
                PlainFiles.WriteLines(_logFilePath, trimmedLines);
            }
        }

        public static void Debug(string message) => Log(LogLevel.Debug, message);
        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warn(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message) => Log(LogLevel.Error, message);
        public static void Fatal(string message) => Log(LogLevel.Fatal, message);

        private static readonly ConcurrentDictionary<string, bool> WarnedAt = new ConcurrentDictionary<string, bool>();

        /// <summary>Logs a failure DSPRE recovered from, the first time it happens at <paramref name="where"/>, so a
        /// fallback that runs every frame doesn't flood the log.</summary>
        public static void WarnOnce(string where, Exception e)
        {
            if (WarnedAt.TryAdd(where, true)) Warn(where + ": " + e.Message);
        }

        public static string GetRecentLogs()
        {
            return string.Join(Environment.NewLine, RecentLogBuffer);
        }

    }
}
