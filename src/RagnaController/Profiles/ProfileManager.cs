using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagnaController.Core;

namespace RagnaController.Profiles
{
    /// <summary>
    /// Manages loading, saving and listing profiles.
    /// Profiles are stored as JSON files in %AppData%\RagnaController\Profiles\.
    /// </summary>
    public class ProfileManager
    {
        private static readonly string ProfileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RagnaController", "Profiles");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public List<Profile> Profiles { get; } = new();

        public ProfileManager()
        {
            Directory.CreateDirectory(ProfileDir);
            Load();
        }

        // ── IO ────────────────────────────────────────────────────────────────────

        public void Load()
        {
            Profiles.Clear();

            // Add built-in presets first
            Profiles.AddRange(CreateBuiltInProfiles());

            // Load user profiles from disk
            foreach (var file in Directory.GetFiles(ProfileDir, "*.json"))
            {
                try
                {
                    var json    = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
                    if (profile != null && !profile.IsBuiltIn)
                        Profiles.Add(profile);
                }
                catch { /* skip corrupt files */ }
            }
        }

        public void Save(Profile profile)
        {
            if (profile.IsBuiltIn) return;
            var fileName = SanitizeFileName(profile.Name) + ".json";
            var path     = Path.Combine(ProfileDir, fileName);
            var json     = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(path, json);
        }

        public void Delete(Profile profile)
        {
            if (profile.IsBuiltIn) return;
            var fileName = SanitizeFileName(profile.Name) + ".json";
            var path     = Path.Combine(ProfileDir, fileName);
            if (File.Exists(path)) File.Delete(path);
            Profiles.Remove(profile);
        }

        public void Export(Profile profile, string targetPath)
        {
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(targetPath, json);
        }

        public Profile? Import(string sourcePath)
        {
            var json    = File.ReadAllText(sourcePath);
            var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
            if (profile == null) return null;
            profile.IsBuiltIn = false;
            Profiles.Add(profile);
            Save(profile);
            return profile;
        }

        // ── Built-in presets ──────────────────────────────────────────────────────

        private static List<Profile> CreateBuiltInProfiles()
        {
            return new List<Profile>
            {
                // Swordsman Branch
                CreateLordKnightProfile(),
                CreatePaladinProfile(),
                
                // Mage Branch
                CreateHighWizardProfile(),
                CreateProfessorProfile(),
                
                // Archer Branch
                CreateSniperProfile(),
                CreateClownGypsyProfile(),
                
                // Acolyte Branch
                CreateHighPriestProfile(),
                CreateChampionProfile(),
                
                // Thief Branch
                CreateAssassinCrossProfile(),
                CreateStalkerProfile(),
                
                // Merchant Branch
                CreateWhitesmithProfile(),
                CreateCreatorProfile(),
                
                // Extended/Alternative Classes
                CreateTaekwonProfile(),
                CreateStarGladiatorProfile(),
                CreateSoulLinkerProfile(),
                CreateGunslingerProfile(),
                CreateNinjaProfile(),
                CreateSuperNoviceProfile()
            };
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  SWORDSMAN BRANCH
        // ══════════════════════════════════════════════════════════════════════════

        private static Profile CreateLordKnightProfile() => new()
        {
            Name        = "Lord Knight",
            Description = "Two-Handed Sword specialist — Bowling Bash spam with Auto-Target",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.3f,
            Deadzone    = 0.12f,
            MovementCurve = 1.4f,
            ActionRpgMode = true,
            ActionSpeed   = 5.0f,
            DefaultTurboMs = 80,

            // Auto-Target for melee
            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,   // Z = Bowling Bash
            AutoAttackIntervalMs = 80,
            TabCycleMs           = 80,
            AimSensitivity       = 22f,
            AimDeadzone          = 0.20f,
            
            ButtonMappings = new()
            {
                // Face Buttons - Main Combat
                ["A"]         = Skill(VirtualKey.Z,  "Bowling Bash",     turbo: true, turboMs: 80),
                ["B"]         = Skill(VirtualKey.X,  "Magnum Break"),
                ["X"]         = Skill(VirtualKey.C,  "Pierce"),
                ["Y"]         = Skill(VirtualKey.V,  "Two-Hand Quicken"),
                
                // Shoulders
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Click(ActionType.RightClick, "Lock Target"),
                
                // D-Pad - Buffs & Utility
                ["DPadUp"]    = Skill(VirtualKey.F5, "Concentration"),
                ["DPadDown"]  = Skill(VirtualKey.F6, "Endure"),
                ["DPadLeft"]  = Skill(VirtualKey.F7, "Provoke"),
                ["DPadRight"] = Skill(VirtualKey.F8, "Auto Berserk"),
                ["Start"]     = Skill(VirtualKey.Escape, "Menu"),

                // L2 Layer - Extended Skills
                ["L2+A"]      = Skill(VirtualKey.B, "Bash"),
                ["L2+B"]      = Skill(VirtualKey.N, "Spear Boomerang"),
                ["L2+X"]      = Skill(VirtualKey.M, "Brandish Spear"),
                ["L2+Y"]      = Skill(VirtualKey.F4, "Parrying"),
                ["L2+DPadUp"]    = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"]  = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"]  = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"] = Skill(VirtualKey.Num4, "Butterfly Wing"),

                // R2 Layer - Utility
                ["R2+A"]      = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]      = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]      = Skill(VirtualKey.AltLeft, "Sit/Stand"),
                ["R2+Y"]      = Skill(VirtualKey.Insert, "Teleport"),
            }
        };

        private static Profile CreatePaladinProfile() => new()
        {
            Name        = "Paladin",
            Description = "Tank/Support hybrid — Shield skills with Devotion support",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.2f,
            Deadzone    = 0.12f,
            MovementCurve = 1.3f,
            ActionRpgMode = true,
            ActionSpeed   = 4.5f,
            DefaultTurboMs = 100,

            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,
            AutoAttackIntervalMs = 100,
            TabCycleMs           = 80,
            AimSensitivity       = 20f,
            AimDeadzone          = 0.20f,
            
            ButtonMappings = new()
            {
                ["A"]         = Skill(VirtualKey.Z,  "Grand Cross",      turbo: true, turboMs: 100),
                ["B"]         = Skill(VirtualKey.X,  "Shield Chain"),
                ["X"]         = Skill(VirtualKey.C,  "Sacrifice"),
                ["Y"]         = Skill(VirtualKey.V,  "Devotion"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Heal"),
                
                ["DPadUp"]    = Skill(VirtualKey.F5, "Faith"),
                ["DPadDown"]  = Skill(VirtualKey.F6, "Gospel"),
                ["DPadLeft"]  = Skill(VirtualKey.F7, "Defender"),
                ["DPadRight"] = Skill(VirtualKey.F8, "Auto Guard"),
                ["Start"]     = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]      = Skill(VirtualKey.B, "Shield Boomerang"),
                ["L2+B"]      = Skill(VirtualKey.N, "Bash"),
                ["L2+X"]      = Skill(VirtualKey.M, "Pressure"),
                ["L2+Y"]      = Skill(VirtualKey.F4, "Providence"),
                ["L2+DPadUp"]    = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"]  = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"]  = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"] = Skill(VirtualKey.Num4, "Holy Water"),

                ["R2+A"]      = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]      = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]      = Skill(VirtualKey.AltLeft, "Sit/Stand"),
                ["R2+Y"]      = Skill(VirtualKey.Insert, "Teleport"),
            }
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  MAGE BRANCH
        // ══════════════════════════════════════════════════════════════════════════

        private static Profile CreateHighWizardProfile() => new()
        {
            Name        = "High Wizard",
            Description = "AoE magic specialist — Storm Gust grinding with Safety Wall defense",
            Class       = "Mage",
            IsBuiltIn   = true,
            MouseSensitivity = 1.0f,
            Deadzone    = 0.15f,
            MovementCurve = 1.2f,
            ActionRpgMode = true,
            ActionSpeed   = 4.0f,
            DefaultTurboMs = 120,

            MageEnabled                 = true,
            MageGroundSpellKeyVK        = 0x5A,
            MageGroundAimSensitivity    = 18f,
            MageGroundAimDeadzone       = 0.15f,
            MageBoltKeyVK               = 0x56,
            MageBoltCastDelayMs         = 1200,
            MageBoltAimSensitivity      = 20f,
            MageDefensiveKeyVK          = 0x43,
            MageDefensiveCooldownMs     = 800,
            MageCastsBeforeSPWarning    = 15,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Storm Gust"),
                ["B"]        = Skill(VirtualKey.X, "Meteor Storm"),
                ["X"]        = Skill(VirtualKey.C, "Lord of Vermillion"),
                ["Y"]        = Skill(VirtualKey.V, "Fire Bolt",         turbo: true, turboMs: 120),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "Blue Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Safety Wall"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Sight"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Quagmire"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Ice Wall"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Stone Curse"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Cold Bolt"),
                ["L2+B"]     = Skill(VirtualKey.N, "Lightning Bolt"),
                ["L2+X"]     = Skill(VirtualKey.M, "Frost Diver"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Jupitel Thunder"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "SP Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Teleport Clip"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Ground Target"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateProfessorProfile() => new()
        {
            Name        = "Professor",
            Description = "Utility caster — Land Protector, Dispell, elemental fields",
            Class       = "Mage",
            IsBuiltIn   = true,
            MouseSensitivity = 1.0f,
            Deadzone    = 0.15f,
            MovementCurve = 1.2f,
            ActionRpgMode = true,
            ActionSpeed   = 4.0f,
            DefaultTurboMs = 150,

            MageEnabled                 = true,
            MageGroundSpellKeyVK        = 0x5A,
            MageGroundAimSensitivity    = 18f,
            MageGroundAimDeadzone       = 0.15f,
            MageBoltKeyVK               = 0x56,
            MageBoltCastDelayMs         = 1000,
            MageBoltAimSensitivity      = 20f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Soul Burn"),
                ["B"]        = Skill(VirtualKey.X, "Land Protector"),
                ["X"]        = Skill(VirtualKey.C, "Dispell"),
                ["Y"]        = Skill(VirtualKey.V, "Fire Bolt",         turbo: true, turboMs: 150),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "Blue Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Spider Web"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Volcano"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Deluge"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Violent Gale"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Magnetic Earth"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Heaven's Drive"),
                ["L2+B"]     = Skill(VirtualKey.N, "Earth Spike"),
                ["L2+X"]     = Skill(VirtualKey.M, "Double Casting"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Auto Spell"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "SP Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Teleport"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Ground Target"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  ARCHER BRANCH
        // ══════════════════════════════════════════════════════════════════════════

        private static Profile CreateSniperProfile() => new()
        {
            Name        = "Sniper",
            Description = "Long-range DPS — Double Strafe spam with trap combos and Kite Engine",
            Class       = "Ranged",
            IsBuiltIn   = true,
            MouseSensitivity  = 1.5f,
            Deadzone          = 0.10f,
            MovementCurve     = 1.6f,
            ActionRpgMode     = true,
            ActionSpeed       = 6.0f,
            DefaultTurboMs    = 55,

            KiteEnabled              = true,
            KiteAttackKeyVK          = 0x5A,
            KiteAttackIntervalMs     = 55,
            KiteAttacksBeforeRetreat = 3,
            KiteRetreatDurationMs    = 600,
            KitePivotDurationMs      = 180,
            KiteRetreatCursorDist    = 90f,
            KiteAimSensitivity       = 20f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Double Strafe",    turbo: true, turboMs: 55),
                ["B"]        = Skill(VirtualKey.X, "Arrow Shower"),
                ["X"]        = Skill(VirtualKey.C, "Falcon Assault"),
                ["Y"]        = Skill(VirtualKey.V, "Sharp Shooting"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Click(ActionType.RightClick, "Lock Target"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "True Sight"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Wind Walk"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Falcon Eyes"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Detect"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Ankle Snare"),
                ["L2+B"]     = Skill(VirtualKey.N, "Sandman"),
                ["L2+X"]     = Skill(VirtualKey.M, "Claymore Trap"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Remove Trap"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Arrows"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Blue Potion"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Trap Materials"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateClownGypsyProfile() => new()
        {
            Name        = "Clown/Gypsy",
            Description = "Support archer — Song/Dance buffs with ranged attacks",
            Class       = "Ranged",
            IsBuiltIn   = true,
            MouseSensitivity  = 1.4f,
            Deadzone          = 0.12f,
            MovementCurve     = 1.5f,
            ActionRpgMode     = true,
            ActionSpeed       = 5.5f,
            DefaultTurboMs    = 60,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Musical Strike",   turbo: true, turboMs: 60),
                ["B"]        = Skill(VirtualKey.X, "Arrow Vulcan"),
                ["X"]        = Skill(VirtualKey.C, "Arrow Shower"),
                ["Y"]        = Skill(VirtualKey.V, "Tarot Card"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Service for You"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Bragi's Poem"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Assassin Cross of Sunset"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Apple of Idun"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Encore"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Lullaby"),
                ["L2+B"]     = Skill(VirtualKey.N, "Scream"),
                ["L2+X"]     = Skill(VirtualKey.M, "Frost Joker"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Please Don't Forget Me"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Arrows"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Blue Potion"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Gemstone"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  ACOLYTE BRANCH
        // ══════════════════════════════════════════════════════════════════════════

        private static Profile CreateHighPriestProfile() => new()
        {
            Name        = "High Priest",
            Description = "Primary healer/support — Party heal, buffs, resurrection with Assumptio",
            Class       = "Support",
            IsBuiltIn   = true,
            MouseSensitivity = 1.0f,
            Deadzone    = 0.15f,
            MovementCurve = 1.2f,
            ActionRpgMode = true,
            ActionSpeed   = 4.0f,
            DefaultTurboMs = 100,

            SupportEnabled           = true,
            SupportHealKeyVK         = 0x46,
            SupportHealIntervalMs    = 500,
            SupportBuffs             = new[] { 0x5A, 0x58, 0x43 },
            SupportBuffIntervalMs    = 800,
            SupportResurrectKeyVK    = 0x52,
            SupportTargetCycleKeyVK  = 0x09,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.F, "Heal"),
                ["B"]        = Skill(VirtualKey.Z, "Blessing"),
                ["X"]        = Skill(VirtualKey.X, "Increase Agi"),
                ["Y"]        = Skill(VirtualKey.C, "Kyrie Eleison"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "Blue Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Assumptio"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Gloria"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Magnificat"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Impositio Manus"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Suffragium"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.V, "Sanctuary"),
                ["L2+B"]     = Skill(VirtualKey.B, "Pneuma"),
                ["L2+X"]     = Skill(VirtualKey.N, "Safety Wall"),
                ["L2+Y"]     = Skill(VirtualKey.R, "Resurrection"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "SP Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Blue Gemstone"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Party Member"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateChampionProfile() => new()
        {
            Name        = "Champion",
            Description = "Monk striker — Combo specialist with Asura Strike finisher",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.4f,
            Deadzone    = 0.10f,
            MovementCurve = 1.5f,
            ActionRpgMode = true,
            ActionSpeed   = 6.0f,
            DefaultTurboMs = 40,

            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,
            AutoAttackIntervalMs = 40,
            TabCycleMs           = 60,
            AimSensitivity       = 25f,
            AimDeadzone          = 0.15f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Triple Attack",    turbo: true, turboMs: 40),
                ["B"]        = Skill(VirtualKey.X, "Occult Impaction"),
                ["X"]        = Skill(VirtualKey.C, "Throw Spirit Sphere"),
                ["Y"]        = Skill(VirtualKey.V, "Asura Strike"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Summon Spirit Sphere"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Fury"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Snap"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Dodge"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Root"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Raging Thrust"),
                ["L2+B"]     = Skill(VirtualKey.N, "Raging Palm Strike"),
                ["L2+X"]     = Skill(VirtualKey.M, "Tiger Knuckle Fist"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Chain Combo"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "SP Potion"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  THIEF BRANCH
        // ══════════════════════════════════════════════════════════════════════════

        private static Profile CreateAssassinCrossProfile() => new()
        {
            Name        = "Assassin Cross",
            Description = "Critical assassin — EDP Sonic Blow burst with Grimtooth",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.5f,
            Deadzone    = 0.08f,
            MovementCurve = 1.6f,
            ActionRpgMode = true,
            ActionSpeed   = 7.0f,
            DefaultTurboMs = 50,

            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,
            AutoAttackIntervalMs = 50,
            TabCycleMs           = 60,
            AimSensitivity       = 28f,
            AimDeadzone          = 0.12f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Sonic Blow",       turbo: true, turboMs: 50),
                ["B"]        = Skill(VirtualKey.X, "Soul Destroyer"),
                ["X"]        = Skill(VirtualKey.C, "Meteor Assault"),
                ["Y"]        = Skill(VirtualKey.V, "Grimtooth"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Cloaking"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "EDP"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Enchant Poison"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Venom Dust"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Create Deadly Poison"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Katar Mastery"),
                ["L2+B"]     = Skill(VirtualKey.N, "Advanced Katar"),
                ["L2+X"]     = Skill(VirtualKey.M, "Soul Breaker"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Venom Splasher"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Poison Bottle"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Blue Potion"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateStalkerProfile() => new()
        {
            Name        = "Stalker",
            Description = "Utility rogue — Strip, Intimidate, Preserve with Bow/Dagger flexibility",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.3f,
            Deadzone    = 0.10f,
            MovementCurve = 1.4f,
            ActionRpgMode = true,
            ActionSpeed   = 6.0f,
            DefaultTurboMs = 60,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Back Stab",        turbo: true, turboMs: 60),
                ["B"]        = Skill(VirtualKey.X, "Triangle Shot"),
                ["X"]        = Skill(VirtualKey.C, "Full Strip"),
                ["Y"]        = Skill(VirtualKey.V, "Intimidate"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Cloaking"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Preserve"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Plagiarism"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Reproduce"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Tunnel Drive"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Strip Weapon"),
                ["L2+B"]     = Skill(VirtualKey.N, "Strip Shield"),
                ["L2+X"]     = Skill(VirtualKey.M, "Strip Armor"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Strip Helm"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Arrows"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  MERCHANT BRANCH
        // ══════════════════════════════════════════════════════════════════════════

        private static Profile CreateWhitesmithProfile() => new()
        {
            Name        = "Whitesmith",
            Description = "Cart attacker — Cart Termination burst with Meltdown utility",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.2f,
            Deadzone    = 0.12f,
            MovementCurve = 1.3f,
            ActionRpgMode = true,
            ActionSpeed   = 5.0f,
            DefaultTurboMs = 90,

            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,
            AutoAttackIntervalMs = 90,
            TabCycleMs           = 80,
            AimSensitivity       = 20f,
            AimDeadzone          = 0.18f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Cart Termination", turbo: true, turboMs: 90),
                ["B"]        = Skill(VirtualKey.X, "Cart Revolution"),
                ["X"]        = Skill(VirtualKey.C, "Cart Boost"),
                ["Y"]        = Skill(VirtualKey.V, "Meltdown"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Adrenaline Rush"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Power Maximize"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Weapon Repair"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Greed"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Over Thrust"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Hammerfall"),
                ["L2+B"]     = Skill(VirtualKey.N, "Unfair Trick"),
                ["L2+X"]     = Skill(VirtualKey.M, "Axe Boomerang"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Skin Tempering"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Ore"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateCreatorProfile() => new()
        {
            Name        = "Creator (Biochemist)",
            Description = "Potion pitcher/Homunculus master — Acid Demo spam with plant support",
            Class       = "Ranged",
            IsBuiltIn   = true,
            MouseSensitivity = 1.1f,
            Deadzone    = 0.14f,
            MovementCurve = 1.3f,
            ActionRpgMode = true,
            ActionSpeed   = 5.0f,
            DefaultTurboMs = 100,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Acid Demonstration",turbo: true, turboMs: 100),
                ["B"]        = Skill(VirtualKey.X, "Bomb"),
                ["X"]        = Skill(VirtualKey.C, "Aid Potion"),
                ["Y"]        = Skill(VirtualKey.V, "Summon Flora"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "Blue Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Call Homunculus"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Plant Cultivation"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Bioethics"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Homunculus Resurrection"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Vaporize"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Sphere Parasitism"),
                ["L2+B"]     = Skill(VirtualKey.N, "Pharmacy"),
                ["L2+X"]     = Skill(VirtualKey.M, "Slim Pitcher"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Full Chemical Protection"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Bottle Grenade"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Acid Bottle"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  EXTENDED / ALTERNATIVE CLASSES
        // ══════════════════════════════════════════════════════════════════════════

        private static Profile CreateTaekwonProfile() => new()
        {
            Name        = "Taekwon Boy/Girl",
            Description = "Kick specialist — Combo system with Running and Sprint",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.5f,
            Deadzone    = 0.08f,
            MovementCurve = 1.7f,
            ActionRpgMode = true,
            ActionSpeed   = 7.0f,
            DefaultTurboMs = 35,

            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,
            AutoAttackIntervalMs = 35,
            TabCycleMs           = 60,
            AimSensitivity       = 30f,
            AimDeadzone          = 0.10f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Kick",              turbo: true, turboMs: 35),
                ["B"]        = Skill(VirtualKey.X, "Running"),
                ["X"]        = Skill(VirtualKey.C, "Tornado Kick"),
                ["Y"]        = Skill(VirtualKey.V, "Flying Kick"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Sprint"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Leap"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Happy Break"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Kihop"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Peaceful Break"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Counter Kick"),
                ["L2+B"]     = Skill(VirtualKey.N, "Roundhouse"),
                ["L2+X"]     = Skill(VirtualKey.M, "Tumbling"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Warm Wind"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "SP Potion"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateStarGladiatorProfile() => new()
        {
            Name        = "Star Gladiator",
            Description = "Solar/Lunar/Stellar fighter — Stance-based combos with Union",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.6f,
            Deadzone    = 0.08f,
            MovementCurve = 1.7f,
            ActionRpgMode = true,
            ActionSpeed   = 7.5f,
            DefaultTurboMs = 40,

            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,
            AutoAttackIntervalMs = 40,
            TabCycleMs           = 60,
            AimSensitivity       = 32f,
            AimDeadzone          = 0.10f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Kick",              turbo: true, turboMs: 40),
                ["B"]        = Skill(VirtualKey.X, "Solar Burst"),
                ["X"]        = Skill(VirtualKey.C, "Lunar Stance"),
                ["Y"]        = Skill(VirtualKey.V, "Stellar Stance"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Union"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Solar Stance"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Solar Blessings"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Lunar Blessings"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Stellar Blessings"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Flying Kick"),
                ["L2+B"]     = Skill(VirtualKey.N, "Star Buster"),
                ["L2+X"]     = Skill(VirtualKey.M, "Solar Protection"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Stellar Protection"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "SP Potion"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateSoulLinkerProfile() => new()
        {
            Name        = "Soul Linker",
            Description = "Spirit support — Class-specific buffs with Kaahi and Kaupe",
            Class       = "Support",
            IsBuiltIn   = true,
            MouseSensitivity = 1.2f,
            Deadzone    = 0.12f,
            MovementCurve = 1.3f,
            ActionRpgMode = true,
            ActionSpeed   = 5.0f,
            DefaultTurboMs = 80,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Esma",              turbo: true, turboMs: 80),
                ["B"]        = Skill(VirtualKey.X, "Estun"),
                ["X"]        = Skill(VirtualKey.C, "Estin"),
                ["Y"]        = Skill(VirtualKey.V, "Eswoo"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "Blue Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Kaahi"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Kaupe"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Kaite"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Kaina"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Kaizel"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Alchemist Spirit"),
                ["L2+B"]     = Skill(VirtualKey.N, "Monk Spirit"),
                ["L2+X"]     = Skill(VirtualKey.M, "Star Spirit"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Sage Spirit"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Crusader Spirit"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Wizard Spirit"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Rogue Spirit"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Assassin Spirit"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Party Member"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateGunslingerProfile() => new()
        {
            Name        = "Gunslinger",
            Description = "Gun specialist — Rapid Shot with Coin buffs and Gatling",
            Class       = "Ranged",
            IsBuiltIn   = true,
            MouseSensitivity = 1.6f,
            Deadzone    = 0.10f,
            MovementCurve = 1.6f,
            ActionRpgMode = true,
            ActionSpeed   = 6.5f,
            DefaultTurboMs = 45,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Rapid Shower",     turbo: true, turboMs: 45),
                ["B"]        = Skill(VirtualKey.X, "Desperado"),
                ["X"]        = Skill(VirtualKey.C, "Gatling Fever"),
                ["Y"]        = Skill(VirtualKey.V, "Full Buster"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Flip Coin"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Increase Accuracy"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Adjust"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Snake Eyes"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Chain Action"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Tracking"),
                ["L2+B"]     = Skill(VirtualKey.N, "Piercing Shot"),
                ["L2+X"]     = Skill(VirtualKey.M, "Disarm"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Dust"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Bullets"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Blue Potion"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Coins"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateNinjaProfile() => new()
        {
            Name        = "Ninja",
            Description = "Ninjutsu master — Throw + Magic skills with Shadow Jump mobility",
            Class       = "Ranged",
            IsBuiltIn   = true,
            MouseSensitivity = 1.7f,
            Deadzone    = 0.08f,
            MovementCurve = 1.8f,
            ActionRpgMode = true,
            ActionSpeed   = 8.0f,
            DefaultTurboMs = 50,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Throw Kunai",      turbo: true, turboMs: 50),
                ["B"]        = Skill(VirtualKey.X, "Throw Huuma Shuriken"),
                ["X"]        = Skill(VirtualKey.C, "Ninja Aura"),
                ["Y"]        = Skill(VirtualKey.V, "Illusion Shadow"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Shadow Jump"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Cicada Skin Shed"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Mirror Image"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Smoke"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Shadow Slash"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Crimson Fire Petal"),
                ["L2+B"]     = Skill(VirtualKey.N, "Lightning Jolt"),
                ["L2+X"]     = Skill(VirtualKey.M, "Wind Blade"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "North Wind"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Kunai"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Blue Potion"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "Ninja Stones"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        private static Profile CreateSuperNoviceProfile() => new()
        {
            Name        = "Super Novice",
            Description = "Jack of all trades — Mixed 1st job skills with Angel's Protection",
            Class       = "Melee",
            IsBuiltIn   = true,
            MouseSensitivity = 1.2f,
            Deadzone    = 0.12f,
            MovementCurve = 1.3f,
            ActionRpgMode = true,
            ActionSpeed   = 5.0f,
            DefaultTurboMs = 70,

            AutoAttackEnabled    = true,
            AutoRetargetEnabled  = true,
            AutoAttackKeyVK      = 0x5A,
            AutoAttackIntervalMs = 70,
            TabCycleMs           = 70,
            AimSensitivity       = 18f,
            AimDeadzone          = 0.15f,
            
            ButtonMappings = new()
            {
                ["A"]        = Skill(VirtualKey.Z, "Bash",             turbo: true, turboMs: 70),
                ["B"]        = Skill(VirtualKey.X, "Mammonite"),
                ["X"]        = Skill(VirtualKey.C, "Magnum Break"),
                ["Y"]        = Skill(VirtualKey.V, "Heal"),
                
                ["RightShoulder"] = Skill(VirtualKey.F1, "White Potion"),
                ["LeftShoulder"]  = Skill(VirtualKey.F2, "Blessing"),
                
                ["DPadUp"]   = Skill(VirtualKey.F5, "Increase Agi"),
                ["DPadDown"] = Skill(VirtualKey.F6, "Kyrie Eleison"),
                ["DPadLeft"] = Skill(VirtualKey.F7, "Fire Bolt"),
                ["DPadRight"]= Skill(VirtualKey.F8, "Cold Bolt"),
                ["Start"]    = Skill(VirtualKey.Escape, "Menu"),

                ["L2+A"]     = Skill(VirtualKey.B, "Provoke"),
                ["L2+B"]     = Skill(VirtualKey.N, "Endure"),
                ["L2+X"]     = Skill(VirtualKey.M, "Pneuma"),
                ["L2+Y"]     = Skill(VirtualKey.F4, "Safety Wall"),
                ["L2+DPadUp"]   = Skill(VirtualKey.Num1, "Blue Potion"),
                ["L2+DPadDown"] = Skill(VirtualKey.Num2, "Yggdrasil Berry"),
                ["L2+DPadLeft"] = Skill(VirtualKey.Num3, "Fly Wing"),
                ["L2+DPadRight"]= Skill(VirtualKey.Num4, "SP Potion"),

                ["R2+A"]     = Click(ActionType.LeftClick, "Click Move"),
                ["R2+B"]     = Skill(VirtualKey.Tab, "Target Next"),
                ["R2+X"]     = Skill(VirtualKey.AltLeft, "Sit/Stand"),
            }
        };

        // ── Utilities ─────────────────────────────────────────────────────────────


        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
