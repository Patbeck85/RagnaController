using System;

namespace RagnaController.Core
{
    /// <summary>
    /// Left Stick → Action RPG movement for Ragnarok Online.
    ///
    /// ACTION RPG MODE (default):
    ///   Stick outside deadzone → hold LMB + move cursor slowly in stick direction.
    ///   Character continuously runs toward cursor as long as LMB is held.
    ///   Stick back to center → LMB released → character stops.
    ///   No manual clicking required — pure directional running like Diablo/Last Epoch.
    ///
    /// CLASSIC MODE:
    ///   Stick moves mouse cursor only. Click-to-move stays manual.
    /// </summary>
    public class MovementEngine
    {
        // ── Config ────────────────────────────────────────────────────────────────

        /// <summary>How far the cursor drifts per tick at full stick deflection (Action RPG mode).</summary>
        public float ActionSpeed  { get; set; } = 5.0f;

        /// <summary>Classic mode sensitivity multiplier.</summary>
        public float Sensitivity  { get; set; } = 1.2f;

        public float Deadzone     { get; set; } = 0.12f;
        public float Curve        { get; set; } = 1.5f;

        /// <summary>
        /// True  = Action RPG (LMB held + cursor drifts while stick active).
        /// False = Classic    (cursor moves, manual clicking).
        /// </summary>
        public bool ActionRpgMode { get; set; } = true;

        // ── State ─────────────────────────────────────────────────────────────────

        private float _smoothX;
        private float _smoothY;
        private float _accumX;
        private float _accumY;

        /// <summary>True while LMB is being held by the engine.</summary>
        private bool _lmbHeld;

        /// <summary>Accumulated idle ticks (used to periodically re-click to prevent RO move-stop).</summary>
        private int _idleTicks;

        // In Action RPG mode we re-issue an LMB click every ~600 ms to prevent
        // Ragnarok Online from stopping the character when the cursor stops drifting.
        private const int ReClickIntervalTicks = 38; // 38 × 16 ms ≈ 600 ms
        private const float SmoothFactor       = 0.22f;

        // ── Main update (called every 16 ms tick) ─────────────────────────────────

        public void Update(float rawX, float rawY)
        {
            float magnitude = MathF.Sqrt(rawX * rawX + rawY * rawY);
            bool  active    = magnitude >= Deadzone;

            if (!active)
            {
                ReleaseLmb();
                DampSmoothing();
                return;
            }

            // Rescale magnitude past deadzone → [0, 1]
            float normMag = (magnitude - Deadzone) / (1f - Deadzone);
            normMag = MathF.Pow(Math.Clamp(normMag, 0f, 1f), Curve);

            float nx = rawX / magnitude * normMag;
            float ny = rawY / magnitude * normMag;

            // Lerp smooth
            _smoothX += (nx - _smoothX) * SmoothFactor;
            _smoothY += (ny - _smoothY) * SmoothFactor;

            if (ActionRpgMode)
                TickActionRpg(normMag);
            else
                TickClassic();
        }

        // ── Action RPG tick ───────────────────────────────────────────────────────

        private void TickActionRpg(float normMag)
        {
            // Press LMB on first active tick
            if (!_lmbHeld)
            {
                InputSimulator.LeftButtonDown();
                _lmbHeld    = true;
                _idleTicks  = 0;
            }

            // Drift cursor slowly in stick direction so RO continuously re-targets
            float speed = ActionSpeed * normMag;
            _accumX += _smoothX * speed;
            _accumY += -_smoothY * speed;

            int dx = (int)_accumX;
            int dy = (int)_accumY;
            _accumX -= dx;
            _accumY -= dy;

            if (dx != 0 || dy != 0)
            {
                InputSimulator.MoveMouseRelative(dx, dy);
                _idleTicks = 0;
            }
            else
            {
                // Cursor barely moving (stick nearly centered but past deadzone):
                // periodically re-click so RO doesn't stop the character.
                _idleTicks++;
                if (_idleTicks >= ReClickIntervalTicks)
                {
                    InputSimulator.LeftButtonUp();
                    InputSimulator.LeftButtonDown();
                    _idleTicks = 0;
                }
            }
        }

        // ── Classic cursor-only tick ──────────────────────────────────────────────

        private void TickClassic()
        {
            float speed = Sensitivity * 18f;
            _accumX += _smoothX * speed;
            _accumY += -_smoothY * speed;

            int dx = (int)_accumX;
            int dy = (int)_accumY;
            _accumX -= dx;
            _accumY -= dy;

            if (dx != 0 || dy != 0)
                InputSimulator.MoveMouseRelative(dx, dy);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void ReleaseLmb()
        {
            if (!_lmbHeld) return;
            InputSimulator.LeftButtonUp();
            _lmbHeld   = false;
            _idleTicks = 0;
        }

        private void DampSmoothing()
        {
            _smoothX *= 0.4f;
            _smoothY *= 0.4f;
        }

        /// <summary>Hard-reset all state. Call when engine stops.</summary>
        public void Reset()
        {
            ReleaseLmb();
            _smoothX = _smoothY = 0f;
            _accumX  = _accumY  = 0f;
            _idleTicks = 0;
        }
    }
}
