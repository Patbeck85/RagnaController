using System;
using System.Runtime.InteropServices;

namespace RagnaController.Core
{
    /// <summary>
    /// Movement engine for Ragnarok Online — click-to-move via left stick.
    ///
    /// v1.1.0 improvements for mobbing:
    ///   1. Dual-Zone curve  — sanfte Kurve unten, Snap bei 90%+ für sofortige Vollgeschwindigkeit
    ///   2. Forward-Bias     — Cursor bei hoher Geschwindigkeit weiter voraus platzieren
    ///   3. Coast-Cancel     — Richtungswechsel >120° bricht coast sofort ab, kein Schlittern
    ///   4. Click-Rate       — Min-Cooldown 267ms → 120ms für flüssiges Mobben
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
        public float Deadzone           { get; set; } = 0.12f;

        /// <summary>
        /// Kurven-Modus.
        /// Classic  = einfache Potenz-Kurve (wie v1.0).
        /// DualZone = sanfte Feinzone + Snap-Vollgas bei hoher Auslenkung (Mobben).
        /// </summary>
        public MovementCurveMode CurveMode { get; set; } = MovementCurveMode.DualZone;

        /// <summary>Classic-Kurve: Potenz-Exponent.</summary>
        public float Curve              { get; set; } = 1.5f;

        /// <summary>
        /// DualZone: Normierter Wert (0..1) ab dem sofort volle Geschwindigkeit gilt.
        /// Default 0.85 = Snap greift bei ~88% Stick-Auslenkung.
        /// </summary>
        public float DualZoneSnapAt     { get; set; } = 0.85f;

        /// <summary>
        /// DualZone: Trennpunkt zwischen Fein- und Normal-Zone (0..1).
        /// Default 0.50 = untere Hälfte Fein, obere Hälfte lineare Rampe.
        /// </summary>
        public float DualZoneTransition { get; set; } = 0.50f;

        public int   LeashRadius        { get; set; } = 180;
        public bool  ActionRpgMode      { get; set; } = true;

        /// <summary>Click-Cooldown bei voller Auslenkung (ms). Default 50 = 20 Klicks/s.</summary>
        public int   ClickCooldownMs    { get; set; } = 50;

        /// <summary>Maximaler Click-Cooldown auch bei sehr geringem Stick (ms). Default 120 = mind. 8 Klicks/s.</summary>
        public int   ClickCooldownMaxMs { get; set; } = 120;

        /// <summary>
        /// Frames Nachgleit nach Stick-Loslassen. 0 = sofort stopp.
        /// Bei Richtungswechsel >120° wird coast immer sofort abgebrochen.
        /// </summary>
        public int   CoastFrames        { get; set; } = 2;

        /// <summary>
        /// Forward-Bias: Cursor-Vorsprung bei hoher Geschwindigkeit (0..1).
        /// 0.35 = bei Vollgas 35% weiter voraus → Charakter läuft weiter durch.
        /// 0.0 = kein Bias (wie v1.0).
        /// </summary>
        public float ForwardBias        { get; set; } = 0.35f;

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

                // ── Fix 3: Coast-Cancel bei Richtungswechsel > 120° ──────────
                // dot < -0.5 entspricht einem Winkel von mehr als 120° zur Küstenrichtung
                if (_coastFramesLeft > 0)
                {
                    float dot = dirX * _coastDirX + dirY * _coastDirY;
                    if (dot < -0.50f)
                        _coastFramesLeft = 0;
                }

                float normalizedMag = (magnitude - Deadzone) / (1f - Deadzone);

                // ── Fix 1: Dual-Zone Kurve ────────────────────────────────────
                float curvedMag = ApplyCurve(normalizedMag);

                _coastFramesLeft = CoastFrames;
                _coastDirX       = dirX;
                _coastDirY       = dirY;
                _coastMagnitude  = curvedMag;

                MoveToTarget(dirX, dirY, curvedMag);

                // ── Fix 4: Bessere Click-Rate ─────────────────────────────────
                if (ActionRpgMode)
                {
                    float rawCooldown    = ClickCooldownMs / MathF.Max(0.42f, curvedMag);
                    int adaptiveCooldown = (int)MathF.Min(rawCooldown, ClickCooldownMaxMs);

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

                // Guard: CoastFrames=0 wäre Division-by-Zero
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
            // ── Fix 2: Forward-Bias ────────────────────────────────────────────
            // Cursor-Radius wächst proportional zur Geschwindigkeit → Charakter läuft weiter
            float effectiveRadius = LeashRadius * (1.0f + ForwardBias * curvedMag);

            int targetX = _centerX + (int)(dirX *  effectiveRadius * curvedMag * Sensitivity);
            int targetY = _centerY - (int)(dirY *  effectiveRadius * curvedMag * Sensitivity);

            if (targetX != _lastTargetX || targetY != _lastTargetY)
            {
                InputSimulator.MoveMouseAbsolute(targetX, targetY);
                _lastTargetX = targetX;
                _lastTargetY = targetY;
            }
        }

        /// <summary>
        /// Geschwindigkeitskurve: normalizedMag (0..1 nach Deadzone) → curvedMag (0..1).
        /// </summary>
        private float ApplyCurve(float normalizedMag)
        {
            if (CurveMode == MovementCurveMode.Classic)
                return MathF.Pow(normalizedMag, Curve);

            // DualZone: Zone 3 – Snap
            if (normalizedMag >= DualZoneSnapAt)
                return 1.0f;

            // DualZone: Zone 2 – lineare Rampe von Transition→Snap (Output 0.55→1.0)
            if (normalizedMag >= DualZoneTransition)
            {
                float span = DualZoneSnapAt - DualZoneTransition;
                if (span <= 0f) return 1.0f; // Guard: Snap==Transition → sofort max
                float t = (normalizedMag - DualZoneTransition) / span;
                return 0.55f + t * 0.45f;
            }

            // DualZone: Zone 1 – quadratische Kurve für Feinsteuerung (Output 0..0.55)
            // Guard: DualZoneTransition=0 wäre Division-by-Zero
            if (DualZoneTransition <= 0f) return 0f;
            float tLow = normalizedMag / DualZoneTransition;
            return tLow * tLow * 0.55f;
        }
    }

    public enum MovementCurveMode
    {
        Classic,   // Einfache Potenz-Kurve (smooth, aber träge bei mittlerem Stick)
        DualZone   // Fein + Rampe + Snap — optimal für Mobben
    }
}
