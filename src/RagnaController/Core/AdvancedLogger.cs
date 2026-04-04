using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace RagnaController.Core
{
    public class AdvancedLogger
    {
        private readonly string[] _buf = new string[1000];
        private int _h, _c, _ti, _tc;
        private readonly object _l = new();
        private readonly string _d, _f;
        private readonly System.Diagnostics.Stopwatch _sw = new();
        private readonly Dictionary<string, int> _cnt = new();
        private readonly long[] _smp = new long[1000];

        public bool FileLoggingEnabled { get; set; } = true;
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        public int TotalEvents { get; private set; }
        public TimeSpan SessionDuration => _sw.Elapsed;

        public AdvancedLogger() {
            _d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagnaController", "Logs");
            Directory.CreateDirectory(_d);
            _f = Path.Combine(_d, $"session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            _sw.Start();
            try { var files = Directory.GetFiles(_d, "session_*.log").Select(f => new FileInfo(f)).OrderByDescending(f => f.LastWriteTime).Skip(20); foreach (var f in files) f.Delete(); } catch { }
        }

        public void Log(LogLevel l, string c, string m) {
            if (l < MinimumLevel) return;
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {l.ToString().ToUpper().PadRight(5)} {c.PadRight(12)} {m}";
            lock (_l) { _buf[_h] = line; _h = (_h + 1) % 1000; if (_c < 1000) _c++; if (!_cnt.ContainsKey(c)) _cnt[c] = 0; _cnt[c]++; TotalEvents++; }
            if (FileLoggingEnabled) try { File.AppendAllText(_f, line + Environment.NewLine); } catch { }
        }

        public string? LogPerformance(string o, long t) {
            lock (_l) { _smp[_ti] = t; _ti = (_ti + 1) % 1000; if (_tc < 1000) _tc++; }
            if (t > 80000) { string w = $"{o} took {t / 10000.0:F2}ms"; Log(LogLevel.Warning, "Perf", w); return w; }
            return null;
        }

        public string GetBuffer() { lock (_l) { StringBuilder sb = new(); for (int i = 0; i < _c; i++) sb.AppendLine(_buf[(_h - _c + i + 1000) % 1000]); return sb.ToString(); } }
        public void ClearBuffer() { lock (_l) { _h = 0; _c = 0; _cnt.Clear(); TotalEvents = 0; } }
        public string ExportSession() { StringBuilder sb = new(); sb.AppendLine("RAGNA EXPORT").AppendLine(GetBuffer()); string p = Path.Combine(_d, $"export_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"); File.WriteAllText(p, sb.ToString()); return p; }
    }

    public enum LogLevel { Debug = 0, Info = 1, Warning = 2, Error = 3 }
}