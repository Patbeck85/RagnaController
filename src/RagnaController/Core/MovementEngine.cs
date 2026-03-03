using System;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    /// <summary>
    /// Movement engine for Ragnarok Online — click-to-move via left stick.
    ///
    /// v1.1.0 improvements:
    ///   1. Dual-Zone curve  — sanfte Kurve unten, Snap bei 90%+
    ///   2. Coast-Cancel     — Richtungswechsel >120° bricht coast sofort ab
    ///   3. Click-Rate       — Min-Cooldown für flüssiges Mobben
    ///
    /// WICHTIG: MoveMouseAbsolute (SetCursorPos) + LeftClick (SendInput mit Thread.Sleep(8))
    /// bleiben wie im Original — das ist die einzige Kombination die RO zuverlässig registriert.
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
        public float Sensitivity        { get; set; } = 1.0f;
        public float Deadzone           { get; set; } = 0.20f;

        /// <summary>Classic = Potenz-Kurve. DualZone = Fein+Snap (empfohlen für Mobben).</summary>
        public MovementCurveMode CurveMode { get; set; } = MovementCurveMode.DualZone;

        public float Curve              { get; set; } = 1.5f;
        public float DualZoneSnapAt     { get; set; } = 0.85f;
        public float DualZoneTransition { get; set; } = 0.50f;

        public int   LeashRadius        { get; set; } = 180;
        public bool  ActionRpgMode      { get; set; } = true;

        /// <summary>Click-Cooldown bei voller Auslenkung (ms). Default 80 = Original.</summary>
        public int   ClickCooldownMs    { get; set; } = 80;

        /// <summary>Maximaler Click-Cooldown bei sehr geringem Stick (ms). Default 200.</summary>
        public int   ClickCooldownMaxMs { get; set; } = 200;

        /// <summary>Coast-Frames nach Stick-Loslassen. Bei Richtungswechsel >120° immer sofort 0.</summary>
        public int   CoastFrames        { get; set; } = 3;

        /// <summary>Forward-Bias: Cursor-Vorsprung bei hoher Geschwindigkeit. 0 = deaktiviert.</summary>
        public float ForwardBias        { get; set; } = 0.0f;

        // ── Interner Status ───────────────────────────────────────────────────
        private bool     _isMoving       = false;
        private DateTime _lastClickTime  = DateTime.MinValue;
        private int      _lastTargetX    = -1;
        private int      _lastTargetY    = -1;

        private int   _coastFramesLeft  = 0;
        private float _coastDirX        = 0f;
        private float _coastDirY        = 0f;
        private float _coastMagnitude   = 0f;

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

                // Coast-Cancel bei Richtungswechsel > 120°
                if (_coastFramesLeft > 0)
                {
                    float dot = dirX * _coastDirX + dirY * _coastDirY;
                    if (dot < -0.50f)
                        _coastFramesLeft = 0;
                }

                float normalizedMag = (magnitude - Deadzone) / (1f - Deadzone);
                float curvedMag     = ApplyCurve(normalizedMag);

                _coastFramesLeft = CoastFrames;
                _coastDirX       = dirX;
                _coastDirY       = dirY;
                _coastMagnitude  = curvedMag;

                MoveToTarget(dirX, dirY, curvedMag);

                if (ActionRpgMode)
                {
                    float rawCooldown    = ClickCooldownMs / MathF.Max(0.3f, curvedMag);
                    int adaptiveCooldown = (int)MathF.Min(rawCooldown, ClickCooldownMaxMs);

                    var now = DateTime.UtcNow;
                    if ((now - _lastClickTime).TotalMilliseconds >= adaptiveCooldown)
                    {
                        InputSimulator.LeftClick(); // Original: SetCursorPos + SendInput + Thread.Sleep(8)
                        _lastClickTime = now;
                    }
                }

                _isMoving = true;
            }
            else if (_coastFramesLeft > 0)
            {
                _coastFramesLeft--;

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

        private void MoveToTarget(float dirX, float dirY, float curvedMag)
        {
            // Forward-Bias: bei ForwardBias=0 (default) exakt wie Original
            float radius = LeashRadius * (1.0f + ForwardBias * curvedMag);

            int targetX = _centerX + (int)(dirX *  radius * curvedMag * Sensitivity);
            int targetY = _centerY - (int)(dirY *  radius * curvedMag * Sensitivity);

            if (targetX != _lastTargetX || targetY != _lastTargetY)
            {
                InputSimulator.MoveMouseAbsolute(targetX, targetY); // SetCursorPos — Original
                _lastTargetX = targetX;
                _lastTargetY = targetY;
            }
        }

        private float ApplyCurve(float normalizedMag)
        {
            if (CurveMode == MovementCurveMode.Classic)
                return MathF.Pow(normalizedMag, Curve);

            if (normalizedMag >= DualZoneSnapAt)
                return 1.0f;

            if (normalizedMag >= DualZoneTransition)
            {
                float t = (normalizedMag - DualZoneTransition) / (DualZoneSnapAt - DualZoneTransition);
                return 0.55f + t * 0.45f;
            }

            if (DualZoneTransition <= 0f) return 0f;
            float tLow = normalizedMag / DualZoneTransition;
            return tLow * tLow * 0.55f;
        }
    }

    public enum MovementCurveMode
    {
        Classic,
        DualZone
    }
}
