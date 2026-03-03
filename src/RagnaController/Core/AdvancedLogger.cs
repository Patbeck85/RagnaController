using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace RagnaController.Core
{
    /// <summary>
    /// Advanced logging system with file export, performance metrics, and session statistics.
    /// </summary>
    public class AdvancedLogger
    {
        private readonly StringBuilder _logBuffer = new();
        private readonly string _logDirectory;
        private readonly string _currentSessionFile;
        private readonly Stopwatch _sessionTimer = new();

        // Statistics
        private readonly Dictionary<string, int> _eventCounts = new();
        private readonly List<long> _tickTimes = new();
        private const int MaxTickSamples = 1000;

        // Settings
        public bool FileLoggingEnabled { get; set; } = true;
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        // Session stats
        public int TotalEvents => _eventCounts.Values.Sum();
        public TimeSpan SessionDuration => _sessionTimer.Elapsed;
        public double AverageTickTimeMs => _tickTimes.Count > 0 ? _tickTimes.Average() / 10000.0 : 0;
        public long MaxTickTimeMs => _tickTimes.Count > 0 ? _tickTimes.Max() / 10000 : 0;

        public AdvancedLogger()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RagnaController", "Logs");

            Directory.CreateDirectory(_logDirectory);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _currentSessionFile = Path.Combine(_logDirectory, $"session_{timestamp}.log");

            _sessionTimer.Start();
            CleanupOldLogs();
            Log(LogLevel.Info, "Session", "Session started");
        }

        /// <summary>
        /// Keep at most 20 session files and 20 export files; delete anything older than 7 days.
        /// Runs silently – log cleanup must never crash the app.
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                var cutoff  = DateTime.Now.AddDays(-7);
                const int maxFiles = 20;

                foreach (var pattern in new[] { "session_*.log", "export_*.txt" })
                {
                    var files = Directory.GetFiles(_logDirectory, pattern)
                                         .Select(f => new FileInfo(f))
                                         .OrderByDescending(f => f.LastWriteTime)
                                         .ToList();

                    // Delete files beyond the keep-limit or older than the cutoff
                    foreach (var f in files.Skip(maxFiles).Concat(
                                 files.Where(f => f.LastWriteTime < cutoff)))
                    {
                        try { f.Delete(); } catch { /* ignore individual delete errors */ }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Logger] Cleanup error: {ex.Message}");
            }
        }

        public void Log(LogLevel level, string category, string message)
        {
            if (level < MinimumLevel) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string levelStr = level.ToString().ToUpper().PadRight(5);
            string catStr = category.PadRight(12);
            string logLine = $"[{timestamp}] {levelStr} {catStr} {message}";

            // Add to buffer
            _logBuffer.AppendLine(logLine);

            // Trim buffer if too large
            if (_logBuffer.Length > 500000) // ~500KB
            {
                var lines = _logBuffer.ToString().Split('\n');
                _logBuffer.Clear();
                _logBuffer.AppendLine(string.Join('\n', lines.TakeLast(5000)));
            }

            // Write to file
            if (FileLoggingEnabled)
            {
                try
                {
                    File.AppendAllText(_currentSessionFile, logLine + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Logger] File write error: {ex.Message}");
                }
            }

            // Track event count
            if (!_eventCounts.ContainsKey(category))
                _eventCounts[category] = 0;
            _eventCounts[category]++;
        }

        /// <summary>
        /// Returns the warning message if the tick exceeded 8ms, null otherwise.
        /// Caller (HybridEngine) can then forward the message via LogMessage event so it appears in the UI.
        /// </summary>
        public string? LogPerformance(string operation, long elapsedTicks)
        {
            _tickTimes.Add(elapsedTicks);
            if (_tickTimes.Count > MaxTickSamples)
                _tickTimes.RemoveAt(0);

            if (elapsedTicks > 80000)  // >8ms (125 FPS threshold)
            {
                double ms = elapsedTicks / 10000.0;
                string warning = $"{operation} took {ms:F2}ms (>8ms threshold)";
                Log(LogLevel.Warning, "Performance", warning);
                return warning; // signal to caller to also fire LogMessage event
            }
            return null;
        }

        public string GetBuffer() => _logBuffer.ToString();

        public void ClearBuffer()
        {
            _logBuffer.Clear();
            Log(LogLevel.Info, "Logger", "Buffer cleared");
        }

        public string ExportSession()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("  RAGNA CONTROLLER — SESSION EXPORT");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine($"Session Start:    {DateTime.Now - SessionDuration:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Session Duration: {SessionDuration:hh\\:mm\\:ss}");
            sb.AppendLine($"Total Events:     {TotalEvents}");
            sb.AppendLine();
            sb.AppendLine("─── Performance Metrics ───────────────────────────────────");
            sb.AppendLine($"Average Tick:     {AverageTickTimeMs:F2}ms");
            sb.AppendLine($"Max Tick:         {MaxTickTimeMs}ms");
            sb.AppendLine($"Frame Rate:       {(1000.0 / Math.Max(AverageTickTimeMs, 1)):F1} FPS");
            sb.AppendLine();
            sb.AppendLine("─── Event Counts ──────────────────────────────────────────");
            foreach (var kvp in _eventCounts.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"{kvp.Key.PadRight(15)} {kvp.Value,6}");
            }
            sb.AppendLine();
            sb.AppendLine("─── Session Log ───────────────────────────────────────────");
            sb.Append(_logBuffer.ToString());
            sb.AppendLine("═══════════════════════════════════════════════════════════");

            string exportPath = Path.Combine(_logDirectory, $"export_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            File.WriteAllText(exportPath, sb.ToString());

            return exportPath;
        }

        public Dictionary<string, int> GetEventCounts() => new Dictionary<string, int>(_eventCounts);

        public void ResetStatistics()
        {
            _eventCounts.Clear();
            _tickTimes.Clear();
            _sessionTimer.Restart();
            Log(LogLevel.Info, "Statistics", "Statistics reset");
        }
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }
}
