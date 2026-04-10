using System;
using System.Media;
using System.Collections.Generic;
using System.Windows.Threading;
using RagnaController.Controller;

namespace RagnaController.Core
{
    public class FeedbackSystem
    {
        private readonly ControllerService _controller;
        private readonly DispatcherTimer _rumbleTimer;
        private bool _soundEnabled = true;
        private bool _rumbleEnabled = true;
        private float _lastL, _lastR;
        private readonly Dictionary<FeedbackType, SystemSound> _sounds = new();

        public bool SoundEnabled { get => _soundEnabled; set => _soundEnabled = value; }
        public bool RumbleEnabled { get => _rumbleEnabled; set => _rumbleEnabled = value; }

        public FeedbackSystem(ControllerService controller)
        {
            _controller = controller;
            _sounds[FeedbackType.CombatModeOn] = SystemSounds.Asterisk;
            _sounds[FeedbackType.CombatModeOff] = SystemSounds.Hand;
            _sounds[FeedbackType.TargetLocked] = SystemSounds.Beep;
            _sounds[FeedbackType.PhaseChange] = SystemSounds.Beep;
            _sounds[FeedbackType.Warning] = SystemSounds.Exclamation;
            _sounds[FeedbackType.ProfileSwitched] = SystemSounds.Asterisk;
            _sounds[FeedbackType.Error] = SystemSounds.Hand;

            _rumbleTimer = new DispatcherTimer();
            _rumbleTimer.Tick += (s, e) => { _rumbleTimer.Stop(); StopRumble(); };
        }

        public void Trigger(FeedbackType type)
        {
            if (_soundEnabled && _sounds.TryGetValue(type, out var s)) s.Play();
            if (!_rumbleEnabled || !_controller.IsConnected) return;

            var (l, r, d) = type switch
            {
                FeedbackType.CombatModeOn => (0.3f, 0.3f, 150),
                FeedbackType.CombatModeOff => (0.2f, 0.0f, 100),
                FeedbackType.TargetLocked => (0.4f, 0.2f, 80),
                FeedbackType.PhaseChange => (0.2f, 0.1f, 60),
                FeedbackType.Warning => (0.6f, 0.6f, 200),
                FeedbackType.ProfileSwitched => (0.5f, 0.5f, 200),
                FeedbackType.Error => (0.8f, 0.4f, 250),
                FeedbackType.KiteRetreat => (0.3f, 0.0f, 100),
                FeedbackType.HealCast => (0.1f, 0.2f, 50),
                FeedbackType.Success => (0.4f, 0.4f, 100),
                FeedbackType.PrecisionModeOn => (0.1f, 0.1f, 80),
                _ => (0f, 0f, 0)
            };
            if (d > 0) StartRumble(l, r, d);
        }

        public void TriggerSkillFired() { if (_rumbleEnabled && _controller.IsConnected) StartRumble(0f, 0.25f, 60); }

        public void StopRumble()
        {
            if (_lastL == 0 && _lastR == 0) return;
            _controller.SetRumble(0, 0);
            _lastL = 0; _lastR = 0;
        }

        private void StartRumble(float l, float r, int ms)
        {
            _rumbleTimer.Stop();
            _controller.SetRumble(l, r);
            _lastL = l; _lastR = r;
            _rumbleTimer.Interval = TimeSpan.FromMilliseconds(ms);
            _rumbleTimer.Start();
        }

        public async void TriggerRhythmicPattern(RumblePattern p)
        {
            if (!_rumbleEnabled || !_controller.IsConnected) return;
            try
            {
                switch (p)
                {
                    case RumblePattern.KiteCycle:
                        StartRumble(0.3f, 0.3f, 80);
                        await System.Threading.Tasks.Task.Delay(280);
                        // Guard: only fire second pulse if still connected and rumble wasn't cancelled
                        if (_controller.IsConnected && _lastL > 0) StartRumble(0.2f, 0f, 100);
                        break;
                    case RumblePattern.LowSPPulse:
                        for (int i = 0; i < 3; i++)
                        {
                            if (!_controller.IsConnected || !_rumbleEnabled) return;
                            StartRumble(0.4f, 0f, 100);
                            await System.Threading.Tasks.Task.Delay(250);
                        }
                        break;
                    case RumblePattern.AutoCycleSweep:
                        StartRumble(0.1f, 0.2f, 200);
                        break;
                }
            }
            catch { }
        }
    }

    public enum FeedbackType { CombatModeOn, CombatModeOff, TargetLocked, PhaseChange, Warning, ProfileSwitched, Error, KiteRetreat, HealCast, Success, PrecisionModeOn }
    public enum RumblePattern { KiteCycle, LowSPPulse, AutoCycleSweep }
}