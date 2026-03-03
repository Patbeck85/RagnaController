using System;
using System.Media;
using System.Collections.Generic;
using System.Windows.Threading;
using RagnaController.Controller;

namespace RagnaController.Core
{
    /// <summary>
    /// Central feedback system for sound and controller vibration.
    /// Supports automatic vibration stop after a defined time.
    /// </summary>
    public class FeedbackSystem
    {
        private readonly ControllerService _controller;
        private readonly DispatcherTimer   _rumbleStopTimer;

        private bool _soundEnabled  = true;
        private bool _rumbleEnabled = true;

        private readonly Dictionary<FeedbackType, SystemSound> _sounds = new();

        public bool SoundEnabled  { get => _soundEnabled;  set => _soundEnabled  = value; }
        public bool RumbleEnabled { get => _rumbleEnabled; set => _rumbleEnabled = value; }

        public FeedbackSystem(ControllerService controller)
        {
            _controller = controller;
            InitializeSounds();

            // Timer that stops vibration after the configured duration
            _rumbleStopTimer = new DispatcherTimer();
            _rumbleStopTimer.Tick += (_, _) =>
            {
                _rumbleStopTimer.Stop();
                StopRumble();
            };
        }

        private void InitializeSounds()
        {
            _sounds[FeedbackType.CombatModeOn]    = SystemSounds.Asterisk;
            _sounds[FeedbackType.CombatModeOff]   = SystemSounds.Hand;
            _sounds[FeedbackType.TargetLocked]    = SystemSounds.Beep;
            _sounds[FeedbackType.PhaseChange]     = SystemSounds.Beep;
            _sounds[FeedbackType.Warning]         = SystemSounds.Exclamation;
            _sounds[FeedbackType.ProfileSwitched] = SystemSounds.Asterisk;
            _sounds[FeedbackType.Error]           = SystemSounds.Hand;
        }

        public void Trigger(FeedbackType type)
        {
            TriggerSound(type);
            TriggerRumble(type);
        }

        /// <summary>Skill feedback: short light rumble on the right motor (finger side).</summary>
        public void TriggerSkillFired()
        {
            if (!_rumbleEnabled || !_controller.IsConnected) return;
            StartRumble(0.0f, 0.25f, 60);
        }

        /// <summary>Stop rumble immediately.</summary>
        public void StopRumble()
        {
            try { _controller.SetRumble(0f, 0f); }
catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FeedbackSystem] Rumble error: {ex.Message}"); }
        }

        private void StartRumble(float left, float right, int durationMs)
        {
            try
            {
                _rumbleStopTimer.Stop();
                _controller.SetRumble(left, right);
                _rumbleStopTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
                _rumbleStopTimer.Start();
            }
catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FeedbackSystem] Rumble error: {ex.Message}"); }
        }

        private void TriggerSound(FeedbackType type)
        {
            if (!_soundEnabled) return;
            if (_sounds.TryGetValue(type, out var sound))
                try { sound.Play(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FeedbackSystem] Sound error: {ex.Message}"); }
        }

        private void TriggerRumble(FeedbackType type)
        {
            if (!_rumbleEnabled || !_controller.IsConnected) return;

            var (left, right, duration) = type switch
            {
                FeedbackType.CombatModeOn    => (0.3f, 0.3f, 150),
                FeedbackType.CombatModeOff   => (0.2f, 0.0f, 100),
                FeedbackType.TargetLocked    => (0.4f, 0.2f,  80),
                FeedbackType.PhaseChange     => (0.2f, 0.1f,  60),
                FeedbackType.Warning         => (0.6f, 0.6f, 200),
                FeedbackType.ProfileSwitched => (0.5f, 0.5f, 200),
                FeedbackType.Error           => (0.8f, 0.4f, 250),
                FeedbackType.KiteRetreat     => (0.3f, 0.0f, 100),
                FeedbackType.HealCast        => (0.1f, 0.2f,  50),
                FeedbackType.Success         => (0.4f, 0.4f, 100),
                FeedbackType.PrecisionModeOn => (0.1f, 0.1f,  80),
                _ => (0f, 0f, 0)
            };

            if (duration > 0)
                StartRumble(left, right, duration);
        }

        public async void TriggerRhythmicPattern(RumblePattern pattern)
        {
            if (!_rumbleEnabled || !_controller.IsConnected) return;

            // async void: wrap entire body so any post-await exception can't crash the app
            try
            {
                switch (pattern)
                {
                    case RumblePattern.KiteCycle:
                        StartRumble(0.3f, 0.3f, 80);
                        await System.Threading.Tasks.Task.Delay(80);
                        if (!_controller.IsConnected) return; // guard after await
                        await System.Threading.Tasks.Task.Delay(200);
                        if (!_controller.IsConnected) return;
                        StartRumble(0.2f, 0.0f, 100);
                        break;

                    case RumblePattern.LowSPPulse:
                        for (int i = 0; i < 3; i++)
                        {
                            if (!_controller.IsConnected) return;
                            StartRumble(0.4f, 0.0f, 100);
                            await System.Threading.Tasks.Task.Delay(250);
                        }
                        break;

                    case RumblePattern.AutoCycleSweep:
                        StartRumble(0.1f, 0.2f, 200);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FeedbackSystem] RhythmicPattern error: {ex.Message}");
            }
        }
    }

    public enum FeedbackType
    {
        CombatModeOn, CombatModeOff, TargetLocked, PhaseChange,
        Warning, ProfileSwitched, Error, KiteRetreat, HealCast,
        Success, PrecisionModeOn
    }

    public enum RumblePattern { KiteCycle, LowSPPulse, AutoCycleSweep }
}
