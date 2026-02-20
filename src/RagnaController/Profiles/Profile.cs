using System.Collections.Generic;
using RagnaController.Core;

namespace RagnaController.Profiles
{
    /// <summary>
    /// Represents a named controller configuration for a specific playstyle.
    /// Serialized to JSON for import/export.
    /// </summary>
    public class Profile
    {
        public string Name             { get; set; } = "New Profile";
        public string Description      { get; set; } = string.Empty;
        public string Class            { get; set; } = "Melee";  // Melee | Ranged | Mage

        // ── Movement ──────────────────────────────────────────────────────────────
        public float MouseSensitivity  { get; set; } = 1.2f;
        public float Deadzone          { get; set; } = 0.12f;
        public float MovementCurve     { get; set; } = 1.5f;

        /// True = Action RPG: LS holds LMB + drifts cursor (character runs continuously).
        /// False = Classic: LS moves cursor only, manual click-to-move.
        public bool  ActionRpgMode     { get; set; } = true;
        public float ActionSpeed       { get; set; } = 5.0f;

        // ── Combat ────────────────────────────────────────────────────────────────
        public int   DefaultTurboMs    { get; set; } = 100;

        // ── Kite System (Ranged feature) ──────────────────────────────────────────

        /// <summary>Enable kite mode for ranged profiles (L3 toggle).</summary>
        public bool  KiteEnabled           { get; set; } = false;

        /// <summary>Attack key for kite engine (default Z = Double Strafe).</summary>
        public int   KiteAttackKeyVK        { get; set; } = 0x5A;  // Z

        /// <summary>Milliseconds between skill fires during kite attack phase.</summary>
        public int   KiteAttackIntervalMs   { get; set; } = 55;

        /// <summary>How many skill fires before retreating.</summary>
        public int   KiteAttacksBeforeRetreat { get; set; } = 3;

        /// <summary>How long (ms) to run backwards during retreat phase.</summary>
        public int   KiteRetreatDurationMs  { get; set; } = 600;

        /// <summary>How long (ms) for cursor to pivot back after retreat.</summary>
        public int   KitePivotDurationMs    { get; set; } = 180;

        /// <summary>Cursor pixels moved during retreat phase.</summary>
        public float KiteRetreatCursorDist  { get; set; } = 90f;

        /// <summary>Right-stick aim speed for kite engine.</summary>
        public float KiteAimSensitivity     { get; set; } = 20f;

        /// <summary>Whether auto-target and auto-attack is active for this profile.</summary>
        public bool  AutoAttackEnabled      { get; set; } = false;

        /// <summary>Automatically re-seek next target when current one dies/leaves range.</summary>
        public bool  AutoRetargetEnabled    { get; set; } = false;

        /// <summary>Virtual key code for the basic attack / skill (default Z = 0x5A).</summary>
        public int   AutoAttackKeyVK        { get; set; } = 0x5A;  // Z key

        /// <summary>Milliseconds between auto-attack key presses.</summary>
        public int   AutoAttackIntervalMs   { get; set; } = 60;

        /// <summary>Milliseconds between Tab-cycle presses during seek.</summary>
        public int   TabCycleMs             { get; set; } = 80;

        /// <summary>Right-stick cursor speed when in aim mode.</summary>
        public float AimSensitivity         { get; set; } = 22f;

        /// <summary>Right-stick deadzone for aim.</summary>
        public float AimDeadzone            { get; set; } = 0.20f;

        // ── Mage System (Wizard/Sage feature) ────────────────────────────────────

        /// <summary>Enable mage combat mode (L3 toggle).</summary>
        public bool  MageEnabled               { get; set; } = false;

        /// <summary>Ground-target AoE spell key (Z = Storm Gust default).</summary>
        public int   MageGroundSpellKeyVK      { get; set; } = 0x5A;  // Z

        /// <summary>Right-stick cursor speed for ground-target aiming.</summary>
        public float MageGroundAimSensitivity  { get; set; } = 18f;

        /// <summary>Right-stick deadzone for ground targeting.</summary>
        public float MageGroundAimDeadzone     { get; set; } = 0.15f;

        /// <summary>Bolt spell key (V = Fire Bolt default).</summary>
        public int   MageBoltKeyVK             { get; set; } = 0x56;  // V

        /// <summary>Milliseconds between bolt casts (includes cast time + delay).</summary>
        public int   MageBoltCastDelayMs       { get; set; } = 1200;  // Fire Bolt Lv10

        /// <summary>Right-stick aim speed when bolt-spamming.</summary>
        public float MageBoltAimSensitivity    { get; set; } = 20f;

        /// <summary>Defensive skill key (C = Safety Wall / Ice Wall).</summary>
        public int   MageDefensiveKeyVK        { get; set; } = 0x43;  // C

        /// <summary>Cooldown for defensive quick-cast (ms).</summary>
        public int   MageDefensiveCooldownMs   { get; set; } = 800;

        /// <summary>Show SP warning after N casts (no memory read, just counter).</summary>
        public int   MageCastsBeforeSPWarning  { get; set; } = 15;

        // ── Support System (Priest/Monk feature) ──────────────────────────────────

        /// <summary>Enable support mode (L3 toggle).</summary>
        public bool  SupportEnabled             { get; set; } = false;

        /// <summary>Heal skill key (Z = Heal default).</summary>
        public int   SupportHealKeyVK           { get; set; } = 0x5A;  // Z

        /// <summary>Milliseconds between heal casts during spam.</summary>
        public int   SupportHealIntervalMs      { get; set; } = 80;

        /// <summary>Self-heal key (usually same as Heal).</summary>
        public int   SupportSelfHealKeyVK       { get; set; } = 0x5A;  // Z

        /// <summary>Resurrection skill key (X = Resurrection).</summary>
        public int   SupportRezzKeyVK           { get; set; } = 0x58;  // X

        /// <summary>Sanctuary ground-target key (C = Sanctuary).</summary>
        public int   SupportSanctuaryKeyVK      { get; set; } = 0x43;  // C

        /// <summary>Right-stick cursor speed for ground-target (Sanctuary).</summary>
        public float SupportGroundAimSensitivity { get; set; } = 18f;

        /// <summary>Right-stick deadzone for ground targeting.</summary>
        public float SupportGroundAimDeadzone   { get; set; } = 0.15f;

        /// <summary>Right-stick aim speed when targeting party members.</summary>
        public float SupportTargetAimSensitivity { get; set; } = 22f;

        /// <summary>Milliseconds between Tab presses during party cycling.</summary>
        public int   SupportTabCycleMs          { get; set; } = 100;

        /// <summary>Enable auto-cycle through party for heal-check sweeps.</summary>
        public bool  SupportAutoCycleEnabled    { get; set; } = false;

        /// <summary>Auto-cycle interval (ms) — sweep party every N seconds.</summary>
        public int   SupportAutoCycleIntervalMs { get; set; } = 3000;

        // ── Button → Action mappings ──────────────────────────────────────────────
        // key = "ButtonName" or "L2+ButtonName" / "R2+ButtonName"
        public Dictionary<string, ButtonAction> ButtonMappings { get; set; } = new();

        // ── Metadata ──────────────────────────────────────────────────────────────
        public string Version          { get; set; } = "1.0";
        public bool   IsBuiltIn        { get; set; } = false;
    }
}
