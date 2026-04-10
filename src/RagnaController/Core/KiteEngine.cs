using System;

namespace RagnaController.Core
{
    public class KiteEngine
    {
        public bool KiteEnabled { get; set; } = false;
        public int AttackKeyVK { get; set; } = 0x5A;
        public int AttackIntervalMs { get; set; } = 55;
        public int AttacksBeforeRetreat { get; set; } = 3;
        public int RetreatDurationMs { get; set; } = 600;
        public int PivotDurationMs { get; set; } = 180;
        public int RelockDelayMs { get; set; } = 120;
        public float RetreatCursorDist { get; set; } = 90f;
        public float AimSensitivity { get; set; } = 20f;
        public float AimDeadzone { get; set; } = 0.18f;

        public KitePhase Phase { get; private set; } = KitePhase.Idle;
        public bool IsActive { get; private set; } = false;
        public int AttacksFired { get; private set; } = 0;
        public string PhaseLabel => Phase switch
        {
            KitePhase.Locking => "LOCKING TARGET",
            KitePhase.Attacking => "ATTACKING",
            KitePhase.Retreating => "RETREATING",
            KitePhase.Pivoting => "PIVOTING",
            KitePhase.Relocking => "RELOCKING",
            _ => "IDLE"
        };

        public event Action<KitePhase>? PhaseChanged;

        private int _phaseTimer = 0;
        private int _attackCooldown = 0;
        private int _attacksThisCycle = 0;
        private float _lastAimX = 1f;
        private float _lastAimY = 0f;
        private float _retreatAccumX = 0f;
        private float _retreatAccumY = 0f;
        private float _retreatDirX = 0f;
        private float _retreatDirY = 0f;
        private int _retreatClickTimer = 0;

        public void ToggleKiteMode()
        {
            IsActive = !IsActive;
            SetPhase(IsActive ? KitePhase.Locking : KitePhase.Idle);
        }

        public void ForceRetreat()
        {
            if (IsActive) BeginRetreat();
        }

        public void Update(float rx, float ry, bool r3, bool hold, bool manual, int ms)
        {
            if (!IsActive) return;

            float sqMag = rx * rx + ry * ry;
            if (sqMag > AimDeadzone * AimDeadzone)
            {
                float mag = MathF.Sqrt(sqMag);
                float invMag = 1.0f / mag;
                float norm = (mag - AimDeadzone) / (1f - AimDeadzone);
                _lastAimX = (rx * invMag) * norm;
                _lastAimY = (ry * invMag) * norm;
            }

            if (r3)
            {
                InputSimulator.RightClick();
                SetPhase(KitePhase.Attacking);
            }

            _phaseTimer = Math.Max(0, _phaseTimer - ms);
            _attackCooldown = Math.Max(0, _attackCooldown - ms);

            if (manual && Phase != KitePhase.Retreating && Phase != KitePhase.Idle)
            {
                BeginRetreat();
                return;
            }

            switch (Phase)
            {
                case KitePhase.Locking:
                    MoveCursorTowardAim(AimSensitivity * 1.5f);
                    if (_phaseTimer <= 0)
                    {
                        InputSimulator.RightClick();
                        _attacksThisCycle = 0;
                        SetPhase(KitePhase.Attacking);
                    }
                    break;
                case KitePhase.Attacking:
                    MoveCursorTowardAim(AimSensitivity * 0.6f);
                    if (_attackCooldown <= 0)
                    {
                        InputSimulator.TapKey((VirtualKey)AttackKeyVK);
                        _attackCooldown = AttackIntervalMs;
                        _attacksThisCycle++;
                        AttacksFired++;
                    }
                    if (_attacksThisCycle >= AttacksBeforeRetreat && !hold) BeginRetreat();
                    break;
                case KitePhase.Retreating:
                    if (hold) { SetPhase(KitePhase.Pivoting); return; }
                    float step = RetreatCursorDist / (RetreatDurationMs / (float)ms);
                    _retreatAccumX += _retreatDirX * step;
                    _retreatAccumY += _retreatDirY * step;
                    int mx = (int)_retreatAccumX; int my = (int)_retreatAccumY;
                    _retreatAccumX -= mx; _retreatAccumY -= my;
                    _retreatClickTimer -= ms;
                    if (_retreatClickTimer <= 0) { InputSimulator.LeftClick(); _retreatClickTimer = 180; }
                    if (mx != 0 || my != 0) InputSimulator.MoveMouseRelative(mx, my);
                    if (_phaseTimer <= 0) SetPhase(KitePhase.Pivoting);
                    break;
                case KitePhase.Pivoting:
                    MoveCursorTowardAim(AimSensitivity * 2.5f);
                    if (_phaseTimer <= 0) SetPhase(KitePhase.Relocking);
                    break;
                case KitePhase.Relocking:
                    if (_phaseTimer <= 0)
                    {
                        InputSimulator.RightClick();
                        _attacksThisCycle = 0;
                        _attackCooldown = 0;
                        SetPhase(KitePhase.Attacking);
                    }
                    break;
            }
        }

        private void BeginRetreat()
        {
            _retreatDirX = -_lastAimX;
            _retreatDirY = -_lastAimY; // negative: retreat AWAY from aim direction (screen Y inverted)
            _retreatAccumX = 0f;
            _retreatAccumY = 0f;
            _retreatClickTimer = 0;
            SetPhase(KitePhase.Retreating, RetreatDurationMs);
        }

        private void MoveCursorTowardAim(float speed)
        {
            int mx = (int)(_lastAimX * speed);
            int my = (int)(-_lastAimY * speed);
            if (mx != 0 || my != 0) InputSimulator.MoveMouseRelative(mx, my);
        }

        private void SetPhase(KitePhase p, int dur = -1)
        {
            Phase = p;
            _phaseTimer = dur >= 0 ? dur : p switch
            {
                KitePhase.Locking => 200,
                KitePhase.Retreating => RetreatDurationMs,
                KitePhase.Pivoting => PivotDurationMs,
                KitePhase.Relocking => RelockDelayMs,
                _ => 0
            };
            PhaseChanged?.Invoke(p);
        }
    }

    public enum KitePhase { Idle, Locking, Attacking, Retreating, Pivoting, Relocking }
}