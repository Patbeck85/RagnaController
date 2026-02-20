using System;
using System.Media;
using System.Collections.Generic;
using RagnaController.Controller;

namespace RagnaController.Core
{
    /// <summary>
    /// Centralized feedback system for sound effects and controller rumble.
    /// Provides haptic and audio feedback for engine state changes.
    /// </summary>
    public class FeedbackSystem
    {
        private readonly ControllerService _controller;
        private bool _soundEnabled = true;
        private bool _rumbleEnabled = true;

        // Sound cache
        private readonly Dictionary<FeedbackType, SystemSound> _sounds = new();

        public bool SoundEnabled
        {
            get => _soundEnabled;
            set => _soundEnabled = value;
        }

        public bool RumbleEnabled
        {
            get => _rumbleEnabled;
            set => _rumbleEnabled = value;
        }

        public FeedbackSystem(ControllerService controller)
        {
            _controller = controller;
            InitializeSounds();
        }

        private void InitializeSounds()
        {
            // Map feedback types to system sounds
            _sounds[FeedbackType.CombatModeOn]      = SystemSounds.Asterisk;
            _sounds[FeedbackType.CombatModeOff]     = SystemSounds.Hand;
            _sounds[FeedbackType.TargetLocked]      = SystemSounds.Beep;
            _sounds[FeedbackType.PhaseChange]       = SystemSounds.Beep;
            _sounds[FeedbackType.Warning]           = SystemSounds.Exclamation;
            _sounds[FeedbackType.ProfileSwitched]   = SystemSounds.Asterisk;
            _sounds[FeedbackType.Error]             = SystemSounds.Hand;
        }

        public void Trigger(FeedbackType type)
        {
            TriggerSound(type);
            TriggerRumble(type);
        }

        private void TriggerSound(FeedbackType type)
        {
            if (!_soundEnabled) return;

            if (_sounds.TryGetValue(type, out var sound))
            {
                try
                {
                    sound.Play();
                }
                catch
                {
                    // Ignore sound errors
                }
            }
        }

        private void TriggerRumble(FeedbackType type)
        {
            if (!_rumbleEnabled || !_controller.IsConnected) return;

            var (leftMotor, rightMotor, duration) = type switch
            {
                FeedbackType.CombatModeOn    => (0.3f, 0.3f, 150),
                FeedbackType.CombatModeOff   => (0.2f, 0.0f, 100),
                FeedbackType.TargetLocked    => (0.4f, 0.2f, 80),
                FeedbackType.PhaseChange     => (0.2f, 0.1f, 60),
                FeedbackType.Warning         => (0.6f, 0.6f, 200),
                FeedbackType.ProfileSwitched => (0.5f, 0.5f, 120),
                FeedbackType.Error           => (0.8f, 0.4f, 250),
                FeedbackType.KiteRetreat     => (0.3f, 0.0f, 100),
                FeedbackType.HealCast        => (0.1f, 0.1f, 50),
                FeedbackType.Success         => (0.4f, 0.4f, 100),
                _ => (0f, 0f, 0)
            };

            if (duration > 0)
            {
                try
                {
                    _controller.SetRumble(leftMotor, rightMotor, duration);
                }
                catch
                {
                    // Ignore rumble errors
                }
            }
        }

        // ── Rhythmic Patterns ─────────────────────────────────────────────────────

        public async void TriggerRhythmicPattern(RumblePattern pattern)
        {
            if (!_rumbleEnabled || !_controller.IsConnected) return;

            switch (pattern)
            {
                case RumblePattern.KiteCycle:
                    // Attack → Retreat rhythm
                    _controller.SetRumble(0.3f, 0.3f, 80);
                    await System.Threading.Tasks.Task.Delay(80);
                    _controller.SetRumble(0.0f, 0.0f, 0);
                    await System.Threading.Tasks.Task.Delay(200);
                    _controller.SetRumble(0.2f, 0.0f, 100);
                    break;

                case RumblePattern.LowSPPulse:
                    // Warning pulse
                    for (int i = 0; i < 3; i++)
                    {
                        _controller.SetRumble(0.4f, 0.0f, 100);
                        await System.Threading.Tasks.Task.Delay(150);
                        _controller.SetRumble(0.0f, 0.0f, 0);
                        await System.Threading.Tasks.Task.Delay(100);
                    }
                    break;

                case RumblePattern.AutoCycleSweep:
                    // Gentle sweep through party
                    _controller.SetRumble(0.1f, 0.2f, 200);
                    break;
            }
        }
    }

    // ── Enums ─────────────────────────────────────────────────────────────────────

    public enum FeedbackType
    {
        CombatModeOn,
        CombatModeOff,
        TargetLocked,
        PhaseChange,
        Warning,
        ProfileSwitched,
        Error,
        KiteRetreat,
        HealCast,
        Success
    }

    public enum RumblePattern
    {
        KiteCycle,
        LowSPPulse,
        AutoCycleSweep
    }
}
