using System.Collections.Generic;
using RagnaController.Core;

namespace RagnaController.Profiles
{
    /// <summary>
    /// Represents a named controller configuration for a specific playstyle.
    /// </summary>
    public class Profile
    {
        public string Name             { get; set; } = "New Profile";
        public string Description      { get; set; } = string.Empty;
        public string Class            { get; set; } = "Melee";  // Melee | Ranged | Mage | Support

        // ── Movement ──────────────────────────────────────────────────────────────
        public float MouseSensitivity  { get; set; } = 1.2f;
        public float Deadzone          { get; set; } = 0.12f;
        public float MovementCurve     { get; set; } = 1.5f;

        // ── Cursor (Rechter Stick) ────────────────────────────────────────────────
        /// Max cursor speed px/s. Recommended for Mage: 800, default: 1200.
        public float CursorMaxSpeed    { get; set; } = 1200f;
        /// Deadzone for cursor stick. Default: 0.12
        public float CursorDeadzone    { get; set; } = 0.12f;
        /// Speed curve. 1.5 = standard, 2.0 = more precise for Mage.
        public float CursorCurve       { get; set; } = 1.5f;

        // ── Movement Feel ─────────────────────────────────────────────────────────
        /// Coast frames after stick release. 0 = instant stop, 2 = mini coast.
        public int   MovementCoastFrames  { get; set; } = 2;

        /// True = Action RPG: LS holds LMB + moves cursor.
        public bool  ActionRpgMode        { get; set; } = true;
        public float ActionSpeed          { get; set; } = 5.0f;

        /// 0 = Classic (Potenz-Kurve), 1 = DualZone (Fein+Snap, empfohlen für Mobben).
        public int   MovementCurveMode    { get; set; } = 1;

        /// Click-Cooldown bei voller Auslenkung (ms). Default 50 = 20 Klicks/s.
        public int   ClickCooldownMs      { get; set; } = 50;

        /// Maximaler Click-Cooldown auch bei sehr geringem Stick (ms). Default 120.
        public int   ClickCooldownMaxMs   { get; set; } = 120;

        /// Forward-Bias: Cursor-Vorsprung bei hoher Geschwindigkeit (0..1).
        /// 0.35 = bei Vollgas 35% weiter voraus → Charakter läuft weiter durch Mobs.
        public float MovementForwardBias  { get; set; } = 0.35f;

        // ── Mob-Sweep Mode ────────────────────────────────────────────────────────
        /// R2 + LS = während des Laufens automatisch TAB-Cycle + Angriff.
        public bool  MobSweepEnabled        { get; set; } = true;
        /// Intervall zwischen automatischen TAB-Presses im Sweep-Modus (ms).
        public int   MobSweepTabIntervalMs  { get; set; } = 350;
        /// Wartezeit nach TAB bevor der Angriff feuert (ms). Lässt RO das Target setzen.
        public int   MobSweepAttackDelayMs  { get; set; } = 60;
        /// Angriffstaste im Sweep-Modus (VirtualKey). Default Z.
        public int   MobSweepAttackKeyVK    { get; set; } = 0x5A; // Z

        // ── Combat General ────────────────────────────────────────────────────────
        public int   DefaultTurboMs    { get; set; } = 100;

        // ── Kite System (Ranged / Hunter) ─────────────────────────────────────────
        public bool  KiteEnabled           { get; set; } = false;
        public int   KiteAttackKeyVK        { get; set; } = 0x5A;  // Z
        public int   KiteAttackIntervalMs   { get; set; } = 55;
        public int   KiteAttacksBeforeRetreat { get; set; } = 3;
        public int   KiteRetreatDurationMs  { get; set; } = 600;
        public int   KitePivotDurationMs    { get; set; } = 180;
        public float KiteRetreatCursorDist  { get; set; } = 90f;
        public float KiteAimSensitivity     { get; set; } = 20f;

        // ── Auto-Target / Auto-Attack (Melee) ─────────────────────────────────────
        public bool  AutoAttackEnabled      { get; set; } = false;
        public bool  AutoRetargetEnabled    { get; set; } = false;
        public int   AutoAttackKeyVK        { get; set; } = 0x5A;  // Z
        public int   AutoAttackIntervalMs   { get; set; } = 60;
        public int   TabCycleMs             { get; set; } = 80;
        public float AimSensitivity         { get; set; } = 22f;
        public float AimDeadzone            { get; set; } = 0.20f;

        // ── Mage System (Wizard / Sage) ──────────────────────────────────────────
        public bool  MageEnabled               { get; set; } = false;
        public int   MageGroundSpellKeyVK      { get; set; } = 0x5A;  // Z
        public float MageGroundAimSensitivity  { get; set; } = 18f;
        public float MageGroundAimDeadzone     { get; set; } = 0.15f;
        public int   MageBoltKeyVK             { get; set; } = 0x56;  // V
        public int   MageBoltCastDelayMs       { get; set; } = 1200;
        public float MageBoltAimSensitivity    { get; set; } = 20f;
        public int   MageDefensiveKeyVK        { get; set; } = 0x43;  // C
        public int   MageDefensiveCooldownMs   { get; set; } = 800;
        public int   MageCastsBeforeSPWarning  { get; set; } = 15;

        // ── Support System (Priest / Monk) ────────────────────────────────────────
        public bool  SupportEnabled             { get; set; } = false;
        public int   SupportHealKeyVK           { get; set; } = 0x5A;  // Z
        public int   SupportHealIntervalMs      { get; set; } = 80;
        public int   SupportSelfHealKeyVK       { get; set; } = 0x5A;  // Z
        
        // Resurrection mapping (supports both names from engine & manager)
        public int   SupportRezzKeyVK           { get; set; } = 0x58;  // X
        public int   SupportResurrectKeyVK      { get => SupportRezzKeyVK; set => SupportRezzKeyVK = value; }

        // Party Targeting
        public int   SupportTargetCycleKeyVK    { get; set; } = 0x09;  // Tab
        public int   SupportTabCycleMs          { get; set; } = 100;
        
        public int   SupportSanctuaryKeyVK      { get; set; } = 0x43;  // C
        public float SupportGroundAimSensitivity { get; set; } = 18f;
        public float SupportGroundAimDeadzone   { get; set; } = 0.15f;
        public float SupportTargetAimSensitivity { get; set; } = 22f;

        // Buff Management & Auto-Cycle
        public bool  SupportAutoCycleEnabled    { get; set; } = false;
        public int   SupportAutoCycleIntervalMs { get; set; } = 3000;
        public List<int> SupportBuffs           { get; set; } = new List<int>();
        public int   SupportBuffIntervalMs      { get; set; } = 30000;

        // ── Button Mappings ───────────────────────────────────────────────────────
        public Dictionary<string, ButtonAction> ButtonMappings { get; set; } = new Dictionary<string, ButtonAction>();

        // ── Info / Skill Recommendations ───────────────────────────────────────────
        /// Recommended F-key layout for this class (shown in the INFO tab)
        public List<string> SkillRecommendations { get; set; } = new();
        /// Class tips and hints for the controller
        public string ClassTips { get; set; } = string.Empty;

        // ── Metadata ──────────────────────────────────────────────────────────────
        public string Version          { get; set; } = "1.0";
        public bool   IsBuiltIn        { get; set; } = false;
    }
}