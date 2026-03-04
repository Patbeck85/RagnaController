using System;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    /// <summary>
    /// Movement engine for Ragnarok Online — click-to-move via left stick.
    ///
    /// Basis: Original v1.0.x (SetCursorPos + SendInput + Thread.Sleep(8)) — bewiesene RO-Kompatibilität.
    /// v1.1.0 Ergänzungen:
    ///   + DualZone-Kurve (sanfte Feinzone + Snap-Vollgas bei 90%+)
    ///   + Coast-Cancel   (Richtungswechsel >120° bricht Nachgleiten sofort ab)
    ///   + CoastFrames Division-by-Zero Guard
    /// </summary>
    public class MovementEngine
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private int _centerX;
        private int _centerY;

        // ── Einstellungen ─────────────────────────────────────────────────────
        public float Sensitivity   { get; set; } = 1.0f;
        public float Deadzone      { get; set; } = 0.20f;
        public float Curve         { get; set; } = 1.5f;
        public int   LeashRadius   { get; set; } = 180;
        public bool  ActionRpgMode { get; set; } = true;

        /// <summary>Click-Cooldown bei voller Auslenkung (ms). Default 80 = Original.</summary>
        public int ClickCooldownMs { get; set; } = 80;

        /// <summary>Frames Nachgleiten nach Stick-Loslassen. Default 3 = Original.</summary>
        public int CoastFrames     { get; set; } = 3;

        /// <summary>
        /// Kurven-Modus.
        /// Classic  = einfache Potenz-Kurve (Original-Verhalten).
        /// DualZone = sanfte Feinzone + Snap-Vollgas bei hoher Auslenkung.
        /// </summary>
        public MovementCurveMode CurveMode    { get; set; } = MovementCurveMode.DualZone;
        public float DualZoneSnapAt           { get; set; } = 0.85f;
        public float DualZoneTransition       { get; set; } = 0.50f;

        // ── Interner Status ───────────────────────────────────────────────────
        private bool     _isMoving       = false;
        private DateTime _lastClickTime  = DateTime.MinValue;
        private int      _lastTargetX    = -1;
        private int      _lastTargetY    = -1;

        private int   _coastFramesLeft = 0;
        private float _coastDirX       = 0f;
        private float _coastDirY       = 0f;
        private float _coastMagnitude  = 0f;

        public MovementEngine() => RefreshScreenCenter();

        public void RefreshScreenCenter()
        {
            _centerX = GetSystemMetrics(SM_CXSCREEN) / 2;
            _centerY = GetSystemMetrics(SM_CYSCREEN) / 2;
        }

        public void Reset()
        {
            _isMoving        = false;
            _coastFramesLeft = 0;
            _lastTargetX     = -1;
            _lastTargetY     = -1;
        }

        public void Update(float stickX, float stickY)
        {
            float magnitude = MathF.Sqrt(stickX * stickX + stickY * stickY);

            if (magnitude > Deadzone)
            {
                float dirX = stickX / magnitude;
                float dirY = stickY / magnitude;

                // Coast-Cancel: Richtungswechsel >120° → sofort stoppen statt schlittern
                if (_coastFramesLeft > 0)
                {
                    float dot = dirX * _coastDirX + dirY * _coastDirY;
                    if (dot < -0.50f)
                        _coastFramesLeft = 0;
                }

                _coastFramesLeft = CoastFrames;
                _coastDirX       = dirX;
                _coastDirY       = dirY;

                float normalizedMag = (magnitude - Deadzone) / (1f - Deadzone);
                float curvedMag     = ApplyCurve(normalizedMag);
                _coastMagnitude     = curvedMag;

                MoveToTarget(dirX, dirY, curvedMag);

                // Adaptive cooldown — Original-Formel, bewährt in RO
                int adaptiveCooldown = (int)(ClickCooldownMs / MathF.Max(0.3f, curvedMag));

                if (ActionRpgMode)
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastClickTime).TotalMilliseconds >= adaptiveCooldown)
                    {
                        InputSimulator.LeftClick();
                        _lastClickTime = now;
                    }
                }

                _isMoving = true;
            }
            else if (_coastFramesLeft > 0)
            {
                _coastFramesLeft--;

                // Guard: CoastFrames=0 → Division-by-Zero verhindern
                float coastFactor = CoastFrames > 0 ? _coastFramesLeft / (float)CoastFrames : 0f;
                float coastCurved = _coastMagnitude * coastFactor;

                MoveToTarget(_coastDirX, _coastDirY, coastCurved);
            }
            else
            {
                if (_isMoving)
                {
                    _isMoving    = false;
                    _lastTargetX = -1;
                    _lastTargetY = -1;
                }
            }
        }

        /// <summary>
        /// Cursor-Position setzen — exakt wie Original.
        /// LeashRadius * curvedMag = Cursor bewegt sich mit dem Stick-Ausschlag.
        /// </summary>
        private void MoveToTarget(float dirX, float dirY, float curvedMag)
        {
            int targetX = _centerX + (int)(dirX *  LeashRadius * curvedMag * Sensitivity);
            int targetY = _centerY - (int)(dirY *  LeashRadius * curvedMag * Sensitivity);

            if (targetX != _lastTargetX || targetY != _lastTargetY)
            {
                InputSimulator.MoveMouseAbsolute(targetX, targetY);
                _lastTargetX = targetX;
                _lastTargetY = targetY;
            }
        }

        /// <summary>Kurve anwenden. normalizedMag = 0..1 nach Deadzone.</summary>
        private float ApplyCurve(float normalizedMag)
        {
            if (CurveMode == MovementCurveMode.Classic)
                return MathF.Pow(normalizedMag, Curve);

            // DualZone Zone 3: sofort Vollgas
            if (normalizedMag >= DualZoneSnapAt)
                return 1.0f;

            // DualZone Zone 2: lineare Rampe
            if (normalizedMag >= DualZoneTransition)
            {
                float t = (normalizedMag - DualZoneTransition) / (DualZoneSnapAt - DualZoneTransition);
                return 0.55f + t * 0.45f;
            }

            // DualZone Zone 1: quadratische Feinsteuerung
            if (DualZoneTransition <= 0f) return 0f;
            float tLow = normalizedMag / DualZoneTransition;
            return tLow * tLow * 0.55f;
        }
    }

    public enum MovementCurveMode
    {
        Classic,   // Potenz-Kurve — Original-Verhalten
        DualZone   // Fein + Rampe + Snap — besser für Mobben
    }
}
