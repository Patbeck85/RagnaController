using System;

namespace RagnaController.Core
{
    public class CursorEngine
    {
        public float MaxSpeed { get; set; } = 1200f;
        public float Deadzone { get; set; } = 0.12f;
        public float Curve { get; set; } = 1.5f;
        public bool PrecisionMode { get; set; } = false;

        private float _remainderX = 0f;
        private float _remainderY = 0f;

        public void Reset()
        {
            _remainderX = 0f;
            _remainderY = 0f;
        }

        public void Update(float stickX, float stickY, int tickMs)
        {
            float sqMag = stickX * stickX + stickY * stickY;
            float dzSq = Deadzone * Deadzone;

            if (sqMag <= dzSq)
            {
                _remainderX = 0f;
                _remainderY = 0f;
                return;
            }

            float magnitude = MathF.Sqrt(sqMag);
            float normalizedMag = (magnitude - Deadzone) / (1f - Deadzone);
            float curvedMag = MathF.Pow(normalizedMag, Curve);
            float effectiveSpeed = PrecisionMode ? MaxSpeed * 0.30f : MaxSpeed;

            float deltaSeconds = tickMs * 0.001f;
            float speedFactor = curvedMag * effectiveSpeed * deltaSeconds;
            
            float invMag = 1.0f / magnitude;
            float pixelsX = (stickX * invMag) * speedFactor + _remainderX;
            float pixelsY = -(stickY * invMag) * speedFactor + _remainderY;

            int moveX = (int)pixelsX;
            int moveY = (int)pixelsY;
            _remainderX = pixelsX - moveX;
            _remainderY = pixelsY - moveY;

            if (moveX != 0 || moveY != 0)
                InputSimulator.MoveMouseRelative(moveX, moveY);
        }
    }
}