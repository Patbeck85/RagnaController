using System;
using System.Collections.Generic;
using RagnaController.Core;

namespace RagnaController.Profiles
{
    public class RadialItem
    {
        public string     Name      { get; set; } = "Empty";
        /// <summary>Chat command for emotes, e.g. "/lv" or "/kis"</summary>
        public string     Command   { get; set; } = "";
        /// <summary>Fallback: direct key press (for backwards-compatible profiles)</summary>
        public VirtualKey Key       { get; set; } = VirtualKey.None;
        /// <summary>true = Command is sent as a chat string, false = Key is pressed directly</summary>
        public bool       IsEmote   { get; set; } = true;
        /// <summary>Path to the local emote image (displayed in the radial overlay)</summary>
        public string     ImagePath { get; set; } = "";
    }

    public class Profile
    {
        public string Name { get; set; } = "New Profile";
        public string Class { get; set; } = "Melee";
        public float MouseSensitivity { get; set; } = 1.2f;
        public float Deadzone { get; set; } = 0.12f;
        public float MovementCurve { get; set; } = 1.5f;
        public float CursorMaxSpeed { get; set; } = 1200f;
        public float CursorDeadzone { get; set; } = 0.12f;
        public float CursorCurve { get; set; } = 1.5f;
        public int MovementCoastFrames { get; set; } = 3;
        public bool ActionRpgMode { get; set; } = true;
        public float ActionSpeed { get; set; } = 5.0f;
        public int MovementCurveMode { get; set; } = 1;
        public int ClickCooldownMs { get; set; } = 80;
        public bool MobSweepEnabled { get; set; } = true;
        public int MobSweepTabIntervalMs { get; set; } = 350;
        public int MobSweepAttackDelayMs { get; set; } = 60;
        public int MobSweepAttackKeyVK { get; set; } = 0x5A;
        public int PreRenewalAttackIntervalMs { get; set; } = 100;
        public int RenewalAttackIntervalMs { get; set; } = 60;
        public int PreRenewalSkillInterruptMs { get; set; } = 800;
        public int RenewalSkillInterruptMs { get; set; } = 400;
        public bool KiteEnabled { get; set; }
        public int KiteAttackKeyVK { get; set; } = 90;
        public int KiteAttackIntervalMs { get; set; } = 55;
        public bool AutoAttackEnabled   { get; set; }
        public bool AutoRetargetEnabled { get; set; }
        public bool SmartSkillEnabled   { get; set; } = true;  // Cursor-Juggling Auto-Aim
        public int AutoAttackKeyVK { get; set; } = 90;
        public int TabCycleMs { get; set; } = 80;
        public float AimSensitivity { get; set; } = 22f;
        public float AimDeadzone { get; set; } = 0.20f;
        public bool MageEnabled { get; set; }
        public int MageBoltKeyVK { get; set; } = 86;
        public int MageBoltCastDelayMs { get; set; } = 1200;
        public bool SupportEnabled { get; set; }
        public int SupportHealKeyVK { get; set; } = 90;

        // --- Combo Engine ---
        public bool ComboEnabled { get; set; }
        public List<string>     ComboSkillNames       { get; set; } = new();
        public List<VirtualKey> ComboSequenceVK       { get; set; } = new();
        public List<int>        PreRenewalComboDelays { get; set; } = new();
        public List<int>        RenewalComboDelays    { get; set; } = new();
        // --------------------

        public Dictionary<string, ButtonAction> ButtonMappings { get; set; } = new();
        public List<string> SkillRecommendations { get; set; } = new();
        public string ClassTips { get; set; } = "";
        public bool IsBuiltIn { get; set; }
        public List<RadialItem> RadialMenuItems { get; set; } = new()
        {
            new RadialItem { Name = "❤ LOVE",    Command = "/lv",   IsEmote = true },
            new RadialItem { Name = "💋 KISS",    Command = "/kis",  IsEmote = true },
            new RadialItem { Name = "😂 HAHA",    Command = "/heh",  IsEmote = true },
            new RadialItem { Name = "😢 CRY",     Command = "/sob",  IsEmote = true },
            new RadialItem { Name = "😰 SWEAT",   Command = "/swt",  IsEmote = true },
            new RadialItem { Name = "😱 OMG",     Command = "/omg",  IsEmote = true },
            new RadialItem { Name = "🙏 SORRY",   Command = "/sry",  IsEmote = true },
            new RadialItem { Name = "👍 NICE",    Command = "/thx",  IsEmote = true },
        };
    }
}
