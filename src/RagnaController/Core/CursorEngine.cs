using System;

namespace RagnaController.Core
{
    /// <summary>
    /// Controls the mouse cursor via the right stick.
    /// Analog speed curve: low deflection = precise, full deflection = fast.
    /// </summary>
    public class CursorEngine
    {
        // ── Einstellungen (werden vom Profil geladen) ────────────────────────

        /// <summary>
        /// Maximale Cursor-Geschwindigkeit in Pixel pro Sekunde bei voller Stick-Auslenkung.
        /// Default: 1200px/s. Recommended for Mage/Wizard: 800px/s (more precision for AoE).
        /// </summary>
        public float MaxSpeed { get; set; } = 1200f;

        /// <summary>
        /// Mindest-Auslenkung bevor der Cursor sich bewegt. Verhindert Drift.
        /// Default: 0.12 (slightly smaller than movement, as precision is more important).
        /// </summary>
        public float Deadzone { get; set; } = 0.12f;

        /// <summary>
        /// Strength of the speed curve.
        /// 1.5 = slightly curved (good for general use).
        /// 2.0 = quadratic (very precise at low deflection, recommended for Mage).
        /// </summary>
        public float Curve { get; set; } = 1.5f;

        /// <summary>
        /// Precision mode (Select button toggle).
        /// Reduces MaxSpeed to 30% for storage, vendors, etc.
        /// </summary>
        public bool PrecisionMode { get; set; } = false;

        // ── Interner Zustand ─────────────────────────────────────────────────
        private float _remainderX = 0f;
        private float _remainderY = 0f;

        public void Reset()
        {
            _remainderX = 0f;
            _remainderY = 0f;
        }

        /// <summary>
        /// Wird jeden Frame aufgerufen. Bewegt den Cursor relativ zur aktuellen Position.
        /// </summary>
        /// <param name="stickX">Rechter Stick X-Achse (-1.0 bis +1.0)</param>
        /// <param name="stickY">Rechter Stick Y-Achse (-1.0 bis +1.0)</param>
        /// <param name="tickMs">Zeit seit letztem Frame in Millisekunden</param>
        public void Update(float stickX, float stickY, int tickMs)
        {
            float magnitude = MathF.Sqrt(stickX * stickX + stickY * stickY);

            if (magnitude <= Deadzone)
            {
                _remainderX = 0f;
                _remainderY = 0f;
                return;
            }

            // Deadzone herausrechnen — Guard: Deadzone≥1.0 würde Division/0 verursachen
            if (Deadzone >= 1f) return;
            float normalizedMag = (magnitude - Deadzone) / (1f - Deadzone);

            // Analoge Kurve: kleine Auslenkung = langsam, volle Auslenkung = volle Geschwindigkeit
            float curvedMag = MathF.Pow(normalizedMag, Curve);

            // Precision mode: 30% of normal speed
            float effectiveSpeed = PrecisionMode ? MaxSpeed * 0.30f : MaxSpeed;

            // Calculate pixels per frame (including sub-pixel remainder for smooth movement)
            float deltaSeconds = tickMs / 1000f;
            float pixelsX = (stickX / magnitude) * curvedMag * effectiveSpeed * deltaSeconds + _remainderX;
            float pixelsY = -(stickY / magnitude) * curvedMag * effectiveSpeed * deltaSeconds + _remainderY; // Y invertiert

            // Move integer pixels, save remainder for next frame
            int moveX = (int)pixelsX;
            int moveY = (int)pixelsY;
            _remainderX = pixelsX - moveX;
            _remainderY = pixelsY - moveY;

            if (moveX != 0 || moveY != 0)
                InputSimulator.MoveMouseRelative(moveX, moveY);
        }
    }
}
