using System;
using System.Collections.Generic;

namespace RagnaController.Core
{
    /// <summary>
    /// Executes a configurable skill combo chain.
    /// While the combo button is HELD, the engine fires
    /// each skill in the sequence and waits exactly the
    /// class- and server-specific delay (Pre-Renewal vs. Renewal).
    /// Releasing the button aborts the combo immediately after the
    /// aktuellen Step.
    /// </summary>
    public class ComboEngine
    {
        // Configuration — loaded from profile
        public bool Enabled { get; set; }
        public List<VirtualKey> Sequence { get; set; } = new();
        public List<int> CurrentDelays { get; set; } = new();

        // Public state exposed to UI snapshot
        public int  CurrentStep { get; private set; }
        public bool IsActive    => CurrentStep > 0 && Enabled;
        public int  TotalSteps  => Sequence.Count;

        /// <summary>e.g. "COMBO 2/3"</summary>
        public string StateLabel => IsActive
            ? $"COMBO {CurrentStep}/{TotalSteps}"
            : "IDLE";

        public event Action<int>? ComboStepFired;

        // Internal state
        private int  _delayTimer;   // remaining delay until next step fires
        private bool _wasHeld;      // whether the combo button was held last tick

        // Reset
        public void Reset()
        {
            CurrentStep  = 0;
            _delayTimer  = 0;
            _wasHeld     = false;
        }

        /// <summary>
        /// Call when an overlay (Daisy Wheel, Radial Menu) closes while the combo
        /// button is still physically held. Without this, the engine would see
        /// isHeld=true with _wasHeld=false and fire Step 1 as a false "fresh press".
        /// </summary>
        public void SyncHeldState(bool currentlyHeld) => _wasHeld = currentlyHeld;

        /// <summary>
        /// Must be called every engine tick.
        /// isHeld = true while the user holds the combo button.
        /// ms = tick duration in milliseconds (8 at 125 Hz).
        /// </summary>
        public void Update(bool isHeld, int ms)
        {
            if (!Enabled || Sequence.Count == 0) return;

            // Count down inter-step delay
            _delayTimer = Math.Max(0, _delayTimer - ms);

            // Button released — abort combo immediately
            if (!isHeld)
            {
                if (_wasHeld) Reset();
                _wasHeld = false;
                return;
            }

            // Fresh button press (first frame) or inter-step delay elapsed
            bool freshPress = isHeld && !_wasHeld;
            if (freshPress || (isHeld && _delayTimer == 0 && CurrentStep > 0))
            {
                if (CurrentStep < Sequence.Count)
                {
                    // Fire the skill
                    InputSimulator.TapKey(Sequence[CurrentStep]);
                    ComboStepFired?.Invoke(CurrentStep + 1);

                    // Load delay for the next step
                    _delayTimer = (CurrentStep < CurrentDelays.Count)
                        ? CurrentDelays[CurrentStep]
                        : 500;

                    CurrentStep++;

                    // Chain complete — reset and apply cooldown to prevent immediate restart
                    if (CurrentStep >= Sequence.Count)
                    {
                        CurrentStep = 0;
                        _delayTimer = 800; // Cooldown after full chain completes
                    }
                }
            }

            _wasHeld = isHeld;
        }
    }
}
