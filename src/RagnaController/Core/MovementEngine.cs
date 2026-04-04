using System;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    public class MovementEngine
    {
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int n);
        private int _cx, _cy, _lx = -1, _ly = -1, _cf = 0;
        private float _cdx, _cdy, _cm, _lootAngle;
        private DateTime _lc = DateTime.MinValue;
        private bool _mv;

        public float Sensitivity { get; set; } = 1.0f;
        public float Deadzone { get; set; } = 0.20f;
        public float Curve { get; set; } = 1.5f;
        public int LeashRadius { get; set; } = 180;
        public bool ActionRpgMode { get; set; } = true;
        public int ClickCooldownMs { get; set; } = 80;
        public int CoastFrames { get; set; } = 3;

        public MovementEngine() => Refresh();
        public void Refresh() { _cx = GetSystemMetrics(0) >> 1; _cy = GetSystemMetrics(1) >> 1; }
        public void Reset() { _mv = false; _cf = 0; _lx = -1; _ly = -1; _lootAngle = 0; }
        public void CenterCursor() { Refresh(); InputSimulator.MoveMouseAbsolute(_cx, _cy); }

        public void Update(float x, float y)
        {
            float sq = x * x + y * y;
            if (sq > Deadzone * Deadzone)
            {
                float m = MathF.Sqrt(sq);
                // FIX: Verhindere Division by Zero
                if (m < 0.001f) m = 0.001f; 
                
                float dx = x / m, dy = y / m;
                if (_cf > 0 && (dx * _cdx + dy * _cdy) < -0.5f) _cf = 0;
                
                _cf = CoastFrames; _cdx = dx; _cdy = dy;
                
                // FIX: Clamp den normierten Wert zwischen 0 und 1, um Infinity zu vermeiden
                float nm = Math.Clamp((m - Deadzone) / (1.0f - Deadzone), 0f, 1f);
                float cv = MathF.Pow(nm, Curve); 
                _cm = cv;
                
                Move(dx, dy, cv);
                
                // Verhindere zu schnelle Klicks
                float clickDiv = Math.Max(0.3f, cv);
                if (ActionRpgMode && (DateTime.UtcNow - _lc).TotalMilliseconds >= (ClickCooldownMs / clickDiv))
                { 
                    InputSimulator.LeftClick(); 
                    _lc = DateTime.UtcNow; 
                }
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
            InputSimulator.MoveMouseAbsolute(_cx + (int)(MathF.Cos(_lootAngle) * r), _cy + (int)(MathF.Sin(_lootAngle) * r));
            InputSimulator.LeftClick();
        }

        private void Move(float dx, float dy, float cv)
        {
            // FIX: NaN/Infinity check
            if (float.IsNaN(dx) || float.IsNaN(dy) || float.IsInfinity(dx) || float.IsInfinity(dy)) return;
            
            float s = LeashRadius * cv * Sensitivity;
            int tx = _cx + (int)(dx * s), ty = _cy - (int)(dy * s);
            
            if (tx != _lx || ty != _ly) 
            { 
                InputSimulator.MoveMouseAbsolute(tx, ty); 
                _lx = tx; 
                _ly = ty; 
            }
        }
    }
}