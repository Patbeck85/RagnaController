using System;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    public class MovementEngine
    {
        private int _cx, _cy, _lx = -1, _ly = -1, _cf = 0;
        private float _cdx, _cdy, _cm, _lootAngle;
        private DateTime _lc = DateTime.MinValue;
        private bool _mv;
        private int _vacuumClickTimer = 0;

        public float Sensitivity    { get; set; } = 1.0f;
        public float Deadzone       { get; set; } = 0.20f;
        public float Curve          { get; set; } = 1.5f;
        public int   LeashRadius    { get; set; } = 180;
        public bool  ActionRpgMode  { get; set; } = true;
        public int   ClickCooldownMs { get; set; } = 80;
        public int   CoastFrames    { get; set; } = 3;

        public MovementEngine() => RefreshCenter();

        /// <summary>
        /// Update the cached screen centre from the WindowTracker result.
        /// Called by HybridEngine every ~500 ms after WindowTracker.Tick().
        /// </summary>
        public void SetCenter(int cx, int cy)
        {
            _cx = cx;
            _cy = cy;
        }

        /// <summary>
        /// Fallback: centre on primary monitor (used before first WindowTracker hit).
        /// </summary>
        public void RefreshCenter()
        {
            _cx = GetSystemMetrics(0) / 2;
            _cy = GetSystemMetrics(1) / 2;
        }

        public void Reset() { _mv = false; _cf = 0; _lx = -1; _ly = -1; _lootAngle = 0; }

        /// <summary>Snaps the mouse cursor to the RO character position.</summary>
        public void CenterCursor() => InputSimulator.MoveMouseAbsolute(_cx, _cy);

        public void Update(float x, float y)
        {
            float sq = x * x + y * y;
            if (sq > Deadzone * Deadzone)
            {
                float m = Math.Min(1.0f, MathF.Sqrt(sq)), dx = x / m, dy = y / m;
                if (_cf > 0 && (dx * _cdx + dy * _cdy) < -0.5f) _cf = 0;
                _cf = CoastFrames; _cdx = dx; _cdy = dy;
                float nm = (m - Deadzone) / (1.0f - Deadzone);
                float cv = MathF.Pow(nm, Curve); _cm = cv;
                Move(dx, dy, cv);
                if (ActionRpgMode && (DateTime.UtcNow - _lc).TotalMilliseconds >= (ClickCooldownMs / Math.Max(0.3f, cv)))
                { InputSimulator.LeftClick(); _lc = DateTime.UtcNow; }
                _mv = true;
            }
            else if (_cf > 0)
            {
                float t = (float)_cf / CoastFrames;
                Move(_cdx, _cdy, _cm * (t * t));
                _cf--;
            }
            else if (_mv) Reset();
        }

        public void PerformLootVacuum(int ms)
        {
            _lootAngle += 0.005f * ms;
            float r = 70 + MathF.Sin(_lootAngle * 0.2f) * 25;
            InputSimulator.MoveMouseAbsolute(
                _cx + (int)(MathF.Cos(_lootAngle) * r),
                _cy + (int)(MathF.Sin(_lootAngle) * r));

            // Throttle clicks to every 50ms — prevents Task.Run spam
            _vacuumClickTimer -= ms;
            if (_vacuumClickTimer <= 0)
            {
                InputSimulator.LeftClick();
                _vacuumClickTimer = 50;
            }
        }

        private void Move(float dx, float dy, float cv)
        {
            float s = LeashRadius * cv * Sensitivity;
            int tx = _cx + (int)(dx * s), ty = _cy - (int)(dy * s);
            if (tx != _lx || ty != _ly) { InputSimulator.MoveMouseAbsolute(tx, ty); _lx = tx; _ly = ty; }
        }

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int n);
    }
}
