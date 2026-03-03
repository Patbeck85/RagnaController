using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagnaController.Core;

namespace RagnaController.Profiles
{
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

        public void Load()
        {
            Profiles.Clear();
            Profiles.AddRange(CreateBuiltInProfiles());

            if (!Directory.Exists(ProfileDir)) return;

            foreach (var file in Directory.GetFiles(ProfileDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions);

                    // Guard: skip null, skip files with empty names (corrupted schema)
                    if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProfileManager] Skipped invalid profile: {file}");
                        continue;
                    }

                    // Always treat disk files as user profiles (SaveProfile forces IsBuiltIn=false,
                    // but guard here too in case a file was hand-edited)
                    profile.IsBuiltIn = false;

                    // Override: if a built-in with the same name exists, replace it
                    // so user customisations survive app updates without creating duplicates.
                    int existingIdx = Profiles.FindIndex(p => p.Name == profile.Name);
                    if (existingIdx >= 0)
                        Profiles[existingIdx] = profile; // replace built-in with saved override
                    else
                        Profiles.Add(profile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileManager] Load error ({file}): {ex.Message}");
                }
            }
        }

        public void Save(Profile profile)
        {
            if (profile.IsBuiltIn) return;
            var fileName = SanitizeFileName(profile.Name) + ".json";
            var path = Path.Combine(ProfileDir, fileName);
            Directory.CreateDirectory(ProfileDir); // Guard against directory deletion at runtime
            File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
        }

        /// <summary>
        /// Saves a profile – built-in profiles are saved as user override files.
        /// Always writes IsBuiltIn=false so Load() will pick up the file on next start.
        /// </summary>
        public void SaveProfile(Profile profile)
        {
            var fileName = SanitizeFileName(profile.Name) + ".json";
            var path = Path.Combine(ProfileDir, fileName);
            Directory.CreateDirectory(ProfileDir);

            // Force IsBuiltIn=false so Load() doesn't silently discard the file.
            // We restore the original value afterwards to avoid mutating the live object.
            bool wasBuiltIn = profile.IsBuiltIn;
            profile.IsBuiltIn = false;
            File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
            profile.IsBuiltIn = wasBuiltIn;
        }

        public void Delete(Profile profile)
        {
            if (profile.IsBuiltIn) return;

            // Primary: try the canonical filename from the current profile name
            var primaryPath = Path.Combine(ProfileDir, SanitizeFileName(profile.Name) + ".json");
            if (File.Exists(primaryPath))
            {
                File.Delete(primaryPath);
            }
            else
            {
                // Fallback: scan all JSON files for one whose Name matches.
                // This handles the case where the profile was renamed after last save.
                foreach (var file in Directory.GetFiles(ProfileDir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var p    = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
                        if (p?.Name == profile.Name)
                        {
                            File.Delete(file);
                            break;
                        }
                    }
                    catch { /* ignore corrupt files during scan */ }
                }
            }

            Profiles.Remove(profile);
        }

        public void Export(Profile profile, string targetPath) =>
            File.WriteAllText(targetPath, JsonSerializer.Serialize(profile, JsonOptions));

        /// <summary>
        /// Reads a profile from disk for preview WITHOUT adding it to the list or saving.
        /// Call AddAndSave() after user confirmation.
        /// </summary>
        public Profile? ImportPreview(string sourcePath)
        {
            var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(sourcePath), JsonOptions);
            if (profile == null) return null;
            profile.IsBuiltIn = false;
            return profile;
        }

        /// <summary>Adds a previewed profile to the list and saves it to disk.</summary>
        public void AddAndSave(Profile profile)
        {
            Profiles.Add(profile);
            Save(profile);
        }

        private static List<Profile> CreateBuiltInProfiles()
        {
            return new List<Profile>
            {
                // ── Standard ─────────────────────────────────────────────────
                CreateStandardProfile(),

                // ── 1st Job ──────────────────────────────────────────────────
                CreateNoviceProfile(),
                CreateSwordmanProfile(),  CreateMageProfile(),
                CreateArcherProfile(),    CreateThiefProfile(),
                CreateMerchantProfile(),  CreateAcolyteProfile(),

                // ── 2nd Job ──────────────────────────────────────────────────
                CreateKnightProfile(),    CreateCrusaderProfile(),
                CreateWizardProfile(),    CreateSageProfile(),
                CreateHunterProfile(),    CreateBardProfile(),    CreateDancerProfile(),
                CreateAssassinProfile(),  CreateRogueProfile(),
                CreateBlacksmithProfile(), CreateAlchemistProfile(),
                CreatePriestProfile(),    CreateMonkProfile(),

                // ── Transcended / High Jobs ───────────────────────────────────
                CreateLordKnightProfile(),    CreatePaladinProfile(),
                CreateHighWizardProfile(),    CreateProfessorProfile(),
                CreateSniperProfile(),        CreateClownGypsyProfile(),
                CreateHighPriestProfile(),    CreateChampionProfile(),
                CreateAssassinCrossProfile(), CreateStalkerProfile(),
                CreateWhitesmithProfile(),    CreateCreatorProfile(),

                // ── Special Classes ───────────────────────────────────────────
                CreateTaekwonProfile(),       CreateStarGladiatorProfile(),
                CreateSoulLinkerProfile(),    CreateGunslingerProfile(),
                CreateNinjaProfile(),         CreateSuperNoviceProfile()
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Standard layout (applies to ALL classes)
        // ─────────────────────────────────────────────────────────────────────
        private static void ApplyBaseLayout(Profile p)
        {
            // ── Base buttons ────────────────────────────────────────────────
            p.ButtonMappings["A"]           = Click(ActionType.LeftClick,  "LMB / Attack / Select");
            p.ButtonMappings["B"]           = Key(VirtualKey.Enter,        "Enter / Confirm dialog");
            // X = Hold Alt → handled directly in HybridEngine, do NOT map here
            p.ButtonMappings["Y"]           = Key(VirtualKey.Insert,       "Sit / Stand");

            // ── Thumbsticks ─────────────────────────────────────────────────
            p.ButtonMappings["LeftThumb"]   = Click(ActionType.RightClick, "Right click (context menu)");
            // RightThumb = DoubleClick → handled directly in HybridEngine, do NOT map here

            // ── D-Pad ───────────────────────────────────────────────────────
            p.ButtonMappings["DPadUp"]      = Key(VirtualKey.Tab,          "Next target (Tab)");
            p.ButtonMappings["DPadDown"]    = Key(VirtualKey.F1,           "Quick-Heal (F1 Standard)");
            // DPadLeft/Right: class-specific, no global default

            // ── Start/Back ──────────────────────────────────────────────────
            p.ButtonMappings["Start"]       = Key(VirtualKey.Escape,       "Escape / Close window");

            // ── L1 + A/B/X/Y → F1–F4 (Skill bar slots 1-4) ────────────────
            p.ButtonMappings["L1+A"] = Key(VirtualKey.F1, "Skill F1");
            p.ButtonMappings["L1+B"] = Key(VirtualKey.F2, "Skill F2");
            p.ButtonMappings["L1+X"] = Key(VirtualKey.F3, "Skill F3");
            p.ButtonMappings["L1+Y"] = Key(VirtualKey.F4, "Skill F4");

            // ── R1 + A/B/X/Y → F5–F8 (Skill bar slots 5-8) ────────────────
            p.ButtonMappings["R1+A"] = Key(VirtualKey.F5, "Skill F5");
            p.ButtonMappings["R1+B"] = Key(VirtualKey.F6, "Skill F6");
            p.ButtonMappings["R1+X"] = Key(VirtualKey.F7, "Skill F7");
            p.ButtonMappings["R1+Y"] = Key(VirtualKey.F8, "Skill F8");

            // ── L2 + A/B/X/Y → F9–F12 (Skill bar slots 9-12) ──────────────
            p.ButtonMappings["L2+A"] = Key(VirtualKey.F9,  "Skill F9");
            p.ButtonMappings["L2+B"] = Key(VirtualKey.F10, "Skill F10");
            p.ButtonMappings["L2+X"] = Key(VirtualKey.F11, "Skill F11");
            p.ButtonMappings["L2+Y"] = Key(VirtualKey.F12, "Skill F12");

            // ── R2 + A/B/X/Y → Ctrl+F1–F4 (Second skill bar slots 1-4) ────
            p.ButtonMappings["R2+A"] = KeyWithMod(VirtualKey.ControlLeft, VirtualKey.F1, "2. Skill Ctrl+F1");
            p.ButtonMappings["R2+B"] = KeyWithMod(VirtualKey.ControlLeft, VirtualKey.F2, "2. Skill Ctrl+F2");
            p.ButtonMappings["R2+X"] = KeyWithMod(VirtualKey.ControlLeft, VirtualKey.F3, "2. Skill Ctrl+F3");
            p.ButtonMappings["R2+Y"] = KeyWithMod(VirtualKey.ControlLeft, VirtualKey.F4, "2. Skill Ctrl+F4");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Class Profiles
        // ─────────────────────────────────────────────────────────────────────

        private static Profile CreateStandardProfile()
        {
            var p = new Profile
            {
                Name        = "Standard",
                Description = "Universal standard layout for all classes.",
                Class       = "Melee",
                IsBuiltIn   = true,
                ActionRpgMode       = true,
                ActionSpeed         = 5.0f,
                MovementCoastFrames = 3,
                CursorMaxSpeed      = 1200f,
                CursorDeadzone      = 0.12f,
                CursorCurve         = 1.5f,
            };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Your most important skill",
                    "F2  → Second main skill",
                    "F3  → Utility / Buff",
                    "F4  → Heal potion",
                    "F5–F8  → Additional skills",
                    "F9–F12 → Rarely used skills",
                    "Ctrl+F1–F4 → 2nd skill bar"
                },
                "Standard template. Assign your F-keys in game according to your class."
            );
            return p;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1st Job Profile
        // ─────────────────────────────────────────────────────────────────────

        private static Profile CreateNoviceProfile()
        {
            var p = new Profile { Name = "Novice", Class = "Melee", IsBuiltIn = true,
                ActionSpeed = 3.5f, MovementCoastFrames = 4, CursorMaxSpeed = 900f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → First Aid (Heal)",
                    "F2  → Use potion",
                },
                "As Novice you have very few skills.\nLevel to Job Level 10 quickly and change to your 1st Job Class!"
            );
            return p;
        }

        private static Profile CreateSwordmanProfile()
        {
            var p = new Profile { Name = "Swordman", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 5.0f, MovementCoastFrames = 3, CursorMaxSpeed = 1200f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Bash ⭐",
                    "F2  → Magnum Break",
                    "F3  → Provoke",
                    "F4  → Endure (Toggle)",
                    "F5  → Potion",
                },
                "Bash (F1) ist dein Haupt-Angriff.\nEndure (F4) aktivieren wenn viele Gegner dich umzingeln."
            );
            return p;
        }

        private static Profile CreateMageProfile()
        {
            var p = new Profile { Name = "Mage", Class = "Mage", IsBuiltIn = true,
                MageEnabled = true, ActionSpeed = 3.5f, MovementCoastFrames = 5,
                CursorMaxSpeed = 900f, CursorCurve = 2.0f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Fire Bolt ⭐",
                    "F2  → Cold Bolt",
                    "F3  → Lightning Bolt",
                    "F4  → Fire Ball",
                    "F5  → Stone Curse",
                    "F6  → Safety Wall",
                },
                "Precision mode (SELECT) for Safety Wall placement.\nMove right stick slowly for precise bolt targeting."
            );
            return p;
        }

        private static Profile CreateArcherProfile()
        {
            var p = new Profile { Name = "Archer", Class = "Ranged", IsBuiltIn = true,
                KiteEnabled = true, ActionSpeed = 4.5f, MovementCoastFrames = 3, CursorMaxSpeed = 1100f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Double Strafe ⭐",
                    "F2  → Arrow Shower",
                    "F3  → Improve Concentration",
                },
                "Double Strafe (F1) is your main damage.\nActivate Improve Concentration (F3) before every fight!"
            );
            return p;
        }

        private static Profile CreateThiefProfile()
        {
            var p = new Profile { Name = "Thief", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 6.0f, MovementCoastFrames = 2, CursorMaxSpeed = 1400f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Double Attack (auto)",
                    "F2  → Steal ⭐",
                    "F3  → Hiding (Toggle)",
                    "F4  → Detoxify",
                    "F5  → Envenom",
                },
                "Steal (F2) immer zuerst → dann angreifen.\nHiding (F3) bei Gefahr aktivieren und weglaufen!"
            );
            return p;
        }

        private static Profile CreateMerchantProfile()
        {
            var p = new Profile { Name = "Merchant", Class = "Melee", IsBuiltIn = true,
                ActionSpeed = 4.0f, MovementCoastFrames = 4, CursorMaxSpeed = 1000f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Mammonite ⭐",
                    "F2  → Cart Revolution",
                    "F3  → Vending (open shop)",
                    "F4  → Discount (passiv)",
                },
                "Precision mode (SELECT) recommended for shop & trading!\nCart Revolution (F2) as AoE emergency attack."
            );
            return p;
        }

        private static Profile CreateAcolyteProfile()
        {
            var p = new Profile { Name = "Acolyte", Class = "Support", IsBuiltIn = true,
                SupportEnabled = true, ActionSpeed = 3.5f, MovementCoastFrames = 4, CursorMaxSpeed = 950f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Heal ⭐",
                    "F2  → Blessing",
                    "F3  → Increase AGI",
                    "F4  → Aqua Benedicta",
                },
                "Point cursor at ally → F1 for Heal.\nBlessing (F2) + Increase AGI (F3) before every fight!"
            );
            return p;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2nd Job Profile
        // ─────────────────────────────────────────────────────────────────────

        private static Profile CreateKnightProfile()
        {
            var p = new Profile { Name = "Knight", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 5.5f, MovementCoastFrames = 2, CursorMaxSpeed = 1200f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Bash / Bowling Bash ⭐",
                    "F2  → Spear Boomerang",
                    "F3  → Two-Hand Quicken (Toggle)",
                    "F4  → Potion",
                },
                "Activate Two-Hand Quicken (F3) → spam Bash.\nSpear Boomerang (F2) for ranged attack."
            );
            return p;
        }

        private static Profile CreateCrusaderProfile()
        {
            var p = new Profile { Name = "Crusader", Class = "Melee", IsBuiltIn = true,
                ActionSpeed = 4.5f, MovementCoastFrames = 3, CursorMaxSpeed = 1000f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Grand Cross ⭐",
                    "F2  → Shield Chain",
                    "F3  → Holy Cross",
                    "F4  → Autoguard (Toggle)",
                    "F5  → Heal",
                    "F6  → Devotion (on ally)",
                },
                "Grand Cross (F1) centers on your character – stand still!\nAlways keep Autoguard (F4) active."
            );
            return p;
        }

        private static Profile CreateWizardProfile()
        {
            var p = new Profile { Name = "Wizard", Class = "Mage", IsBuiltIn = true,
                MageEnabled = true, ActionSpeed = 3.5f, MovementCoastFrames = 5,
                CursorMaxSpeed = 800f, CursorCurve = 2.0f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Storm Gust ⭐",
                    "F2  → Lord of Vermilion",
                    "F3  → Meteor Storm",
                    "F4  → Water Ball",
                    "F5  → Fire Pillar",
                    "F6  → Frost Nova",
                    "F7  → Safety Wall",
                },
                "Precision mode (SELECT) for AoE placement!\nMove right stick slowly → precise Storm Gust position."
            );
            return p;
        }

        private static Profile CreateSageProfile()
        {
            var p = new Profile { Name = "Sage", Class = "Mage", IsBuiltIn = true,
                MageEnabled = true, ActionSpeed = 4.0f, MovementCoastFrames = 4,
                CursorMaxSpeed = 900f, CursorCurve = 1.8f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Earth Spike ⭐",
                    "F2  → Dispell",
                    "F3  → Free Cast (Toggle)",
                    "F4  → Spider Web",
                    "F5  → Auto Spell (Toggle)",
                    "F6  → Safety Wall",
                },
                "Free Cast (F3) allows moving while casting!\nSpider Web (F4) → Earth Spike (F1) combo."
            );
            return p;
        }

        private static Profile CreateHunterProfile()
        {
            var p = new Profile { Name = "Hunter", Class = "Ranged", IsBuiltIn = true,
                KiteEnabled = true, ActionSpeed = 5.0f, MovementCoastFrames = 3, CursorMaxSpeed = 1100f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Double Strafe ⭐ (Turbo!)",
                    "F2  → Ankle Snare (Trap)",
                    "F3  → Blastmine",
                    "F4  → Sandman",
                    "F5  → Falcon Assault",
                    "F6  → Improve Concentration",
                },
                "Place Ankle Snare (F2) → enemy walks in → Double Strafe spam!\nSet trap before running."
            );
            return p;
        }

        private static Profile CreateBardProfile()
        {
            var p = new Profile { Name = "Bard", Class = "Ranged", IsBuiltIn = true,
                SupportEnabled = true, ActionSpeed = 4.0f, MovementCoastFrames = 4, CursorMaxSpeed = 1000f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Impressive Riff ⭐ (ASPD Buff)",
                    "F2  → Apple of Idun (HP Regen)",
                    "F3  → Whistle (Flee Buff)",
                    "F4  → Musical Strike",
                    "F5  → Frost Joke",
                    "F6  → Dissonance",
                },
                "Activate song → stand still until party is buffed!\nFrost Joke (F5) randomly freezes nearby enemies."
            );
            return p;
        }

        private static Profile CreateDancerProfile()
        {
            var p = new Profile { Name = "Dancer", Class = "Ranged", IsBuiltIn = true,
                SupportEnabled = true, ActionSpeed = 4.0f, MovementCoastFrames = 4, CursorMaxSpeed = 1000f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Slow Grace ⭐ (ASPD Debuff on enemies)",
                    "F2  → Hip Shaker (SP Drain)",
                    "F3  → Wink of Charm",
                    "F4  → Throw Arrow",
                    "F5  → Dazzler",
                    "F6  → Service for You (SP Regen Party)",
                },
                "Activate dance → stand still!\nSlow Grace (F1) slows enemies – very strong in PvP."
            );
            return p;
        }

        private static Profile CreateAssassinProfile()
        {
            var p = new Profile { Name = "Assassin", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 6.5f, MovementCoastFrames = 1, CursorMaxSpeed = 1500f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Sonic Blow ⭐",
                    "F2  → Enchant Poison",
                    "F3  → Venom Dust",
                    "F4  → Grimtooth",
                    "F5  → Cloaking (Toggle)",
                },
                "Cloaking (F5) → heranschleichen → Sonic Blow (F1)!\nEnchant Poison (F2) vor dem Kampf aktivieren."
            );
            return p;
        }

        private static Profile CreateRogueProfile()
        {
            var p = new Profile { Name = "Rogue", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 5.5f, MovementCoastFrames = 2, CursorMaxSpeed = 1300f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Backstab ⭐",
                    "F2  → Snatcher",
                    "F3  → Intimidate",
                    "F4  → Raid",
                    "F5  → Steal",
                },
                "Backstab (F1) only works from behind – circle enemy first!\nSnatcher (F2) = Auto-Steal on attack."
            );
            return p;
        }

        private static Profile CreateBlacksmithProfile()
        {
            var p = new Profile { Name = "Blacksmith", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 5.0f, MovementCoastFrames = 3, CursorMaxSpeed = 1100f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Cart Revolution ⭐",
                    "F2  → Hammerfall",
                    "F3  → Adrenaline Rush (Toggle)",
                    "F4  → Weapon Perfection (Toggle)",
                    "F5  → Power Thrust (Toggle)",
                    "F6  → Mammonite",
                },
                "Before fighting: activate F3 + F4 + F5 (buff stack)!\nHammerfall (F2) Stun → Cart Revolution (F1) Spam."
            );
            return p;
        }

        private static Profile CreateAlchemistProfile()
        {
            var p = new Profile { Name = "Alchemist", Class = "Melee", IsBuiltIn = true,
                ActionSpeed = 4.5f, MovementCoastFrames = 3, CursorMaxSpeed = 1100f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Acid Terror ⭐",
                    "F2  → Potion Pitcher (auf Ally)",
                    "F3  → Summon Homunculus",
                    "F4  → Homunculus Skill",
                    "F5  → Demonstration",
                },
                "Potion Pitcher (F2): point cursor at ally → press L1+B.\nSummon Homunculus (F3) and let it fight alongside you!"
            );
            return p;
        }

        private static Profile CreatePriestProfile()
        {
            var p = new Profile { Name = "Priest", Class = "Support", IsBuiltIn = true,
                SupportEnabled = true, ActionSpeed = 3.5f, MovementCoastFrames = 4, CursorMaxSpeed = 900f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Heal ⭐",
                    "F2  → Sanctuary",
                    "F3  → Resurrection",
                    "F4  → Blessing",
                    "F5  → Increase AGI",
                    "F6  → Lex Aeterna",
                    "F7  → Turn Undead",
                    "Ctrl+F1 → Gloria",
                    "Ctrl+F2 → Magnificat",
                },
                "Point cursor at ally → F1 Heal.\nPlace Sanctuary (F2) on ground = AoE heal for the party."
            );
            return p;
        }

        private static Profile CreateMonkProfile()
        {
            var p = new Profile { Name = "Monk", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 6.0f, MovementCoastFrames = 2, CursorMaxSpeed = 1300f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Asura Strike ⭐ (Combo Finisher!)",
                    "F2  → Triple Attack (Combo Start)",
                    "F3  → Investigate",
                    "F4  → Occult Impaction",
                    "F5  → Charge Spirits",
                    "F6  → Mental Strength (Toggle)",
                },
                "Combo: F2 (Triple Attack) → F3 → F4 → F1 (Asura)!\nPress F5 multiple times to charge Spirits before Asura."
            );
            return p;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Transcended / High Job Profile
        // ─────────────────────────────────────────────────────────────────────

        private static Profile CreateLordKnightProfile()
        {
            var p = new Profile { Name = "Lord Knight", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 6.0f, MovementCoastFrames = 2, CursorMaxSpeed = 1400f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Bowling Bash ⭐ (Turbo recommended!)",
                    "F2  → Brandish Spear",
                    "F3  → Berserk",
                    "F4  → Concentration",
                    "F5  → Two-Hand Quicken",
                    "F6  → Potion",
                    "Ctrl+F1 → Aura Blade",
                    "Ctrl+F2 → Parry"
                },
                "Enable Bowling Bash on F1 with Turbo → hold L1+A = spam!\nBerserk only when HP is high enough!"
            );
            return p;
        }

        private static Profile CreatePaladinProfile()
        {
            var p = new Profile { Name = "Paladin", Class = "Melee", IsBuiltIn = true,
                ActionSpeed = 4.5f, MovementCoastFrames = 3, CursorMaxSpeed = 1000f, SupportEnabled = true };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Devotion (auf Party-Mitglied)",
                    "F2  → Shield Chain ⭐",
                    "F3  → Heal",
                    "F4  → Gloria Domini",
                    "F5  → Sacrifice",
                    "F6  → Autoguard (Toggle)",
                    "Ctrl+F1 → Grand Cross",
                    "Ctrl+F2 → Shield Reflect"
                },
                "Devotion: point cursor at ally, then press L1+A.\nSacrifice only with high VIT build!"
            );
            return p;
        }

        private static Profile CreateHighWizardProfile()
        {
            var p = new Profile { Name = "High Wizard", Class = "Mage", IsBuiltIn = true,
                MageEnabled = true, ActionSpeed = 3.5f, MovementCoastFrames = 5,
                CursorMaxSpeed = 750f, CursorCurve = 2.2f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Storm Gust ⭐ (AoE Ice)",
                    "F2  → Meteor Storm ⭐ (AoE Fire)",
                    "F3  → Frostdiver (Freeze single target)",
                    "F4  → Lord of Vermilion (AoE Lightning)",
                    "F5  → Napalm Vulcan",
                    "F6  → Magic Crasher",
                    "F7  → Safety Wall",
                    "Ctrl+F1 → Quagmire",
                    "Ctrl+F2 → Energy Coat (Toggle)"
                },
                "Activate precision mode (SELECT) for AoE placement!\nMove right stick slowly for precise Storm Gust position."
            );
            return p;
        }

        private static Profile CreateProfessorProfile()
        {
            var p = new Profile { Name = "Professor", Class = "Mage", IsBuiltIn = true,
                MageEnabled = true, ActionSpeed = 3.5f, MovementCoastFrames = 5,
                CursorMaxSpeed = 800f, CursorCurve = 2.0f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Soul Burn ⭐ (SP Drain / Silence)",
                    "F2  → Fiber Lock (Spider Web)",
                    "F3  → Dispell",
                    "F4  → Heaven's Drive",
                    "F5  → Memorize",
                    "F6  → Double Bolt",
                    "Ctrl+F1 → Energy Coat (Toggle)",
                    "Ctrl+F2 → Free Cast (Toggle)"
                },
                "Fiber Lock → Soul Burn Combo: F2 then quickly F1.\nDispell (F3) against buffed enemies in PvP."
            );
            return p;
        }

        private static Profile CreateSniperProfile()
        {
            var p = new Profile { Name = "Sniper", Class = "Ranged", IsBuiltIn = true,
                KiteEnabled = true, ActionSpeed = 5.5f, MovementCoastFrames = 2, CursorMaxSpeed = 1300f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Double Strafe ⭐ (Turbo!)",
                    "F2  → Aimed Bolt ⭐",
                    "F3  → Ankle Snare (Trap)",
                    "F4  → Sharpshooter",
                    "F5  → Falcon Assault",
                    "F6  → Blastmine",
                    "F7  → Improve Concentration",
                    "Ctrl+F1 → True Sight (Toggle)",
                    "Ctrl+F2 → Wind Walk (Toggle)"
                },
                "Double Strafe (F1) with Turbo: hold L1+A.\nPlace Ankle Snare, then Aimed Bolt = maximum damage!"
            );
            return p;
        }

        private static Profile CreateClownGypsyProfile()
        {
            var p = new Profile { Name = "Clown/Gypsy", Class = "Ranged", IsBuiltIn = true,
                ActionSpeed = 4.0f, MovementCoastFrames = 4, CursorMaxSpeed = 1000f, SupportEnabled = true };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Poem of Bragi / Slow Grace ⭐",
                    "F2  → Apple of Idun / Hip Shaker",
                    "F3  → Musical Strike / Throw Arrow",
                    "F4  → Tarot Card of Fate",
                    "F5  → Arrow Vulcan",
                    "F6  → Frost Joke / Dazzler",
                    "Ctrl+F1 → Dissonance",
                    "Ctrl+F2 → Invulnerable Siegfried"
                },
                "Activate songs/dances then stand still!\nPoem of Bragi reduces cast time for the whole party."
            );
            return p;
        }

        private static Profile CreateHighPriestProfile()
        {
            var p = new Profile { Name = "High Priest", Class = "Support", IsBuiltIn = true,
                SupportEnabled = true, ActionSpeed = 3.5f, MovementCoastFrames = 5,
                CursorMaxSpeed = 850f, CursorCurve = 1.8f };
            ApplyBaseLayout(p);
            p.SupportBuffs = new List<int> { 0x5A, 0x58, 0x43 };
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Heal ⭐ (Turbo for auto-heal)",
                    "F2  → Sanctuary (AoE Heal)",
                    "F3  → Assumptio ⭐",
                    "F4  → Pneuma (arrow protection)",
                    "F5  → Resurrection",
                    "F6  → Gloria",
                    "F7  → Magnificat (SP Regen)",
                    "F8  → Lex Aeterna",
                    "Ctrl+F1 → Blessing",
                    "Ctrl+F2 → Increase AGI",
                    "Ctrl+F3 → Kyrie Eleison"
                },
                "Precision mode (SELECT) for targeted healing on allies.\nResurrection: point cursor at dead ally, then press F5."
            );
            return p;
        }

        private static Profile CreateChampionProfile()
        {
            var p = new Profile { Name = "Champion", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 6.5f, MovementCoastFrames = 1, CursorMaxSpeed = 1400f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Asura Strike ⭐ (Combo Finisher!)",
                    "F2  → Chain Combo",
                    "F3  → Combo Finish",
                    "F4  → Tiger Knuckle Fist",
                    "F5  → Mental Strength (Toggle)",
                    "F6  → Charge Spirits",
                    "Ctrl+F1 → Occult Impaction"
                },
                "Combo: F2 → F3 → F4 → F1 (Asura Strike)!\nBefore Asura: charge Spirits to maximum (press F6 multiple times)."
            );
            return p;
        }

        private static Profile CreateAssassinCrossProfile()
        {
            var p = new Profile { Name = "Assassin Cross", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 7.0f, MovementCoastFrames = 1, CursorMaxSpeed = 1600f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Sonic Blow ⭐ (Turbo!)",
                    "F2  → Soul Destroyer",
                    "F3  → Cloaking (Toggle)",
                    "F4  → Meteor Assault",
                    "F5  → Enchant Poison",
                    "F6  → Create Deadly Poison",
                    "Ctrl+F1 → EDP (Enchant Deadly Poison)",
                    "Ctrl+F2 → Venom Dust"
                },
                "SBK: EDP (Ctrl+F1) → Sonic Blow (F1) Turbo!\nUse Cloaking (F3) to approach enemies unseen."
            );
            return p;
        }

        private static Profile CreateStalkerProfile()
        {
            var p = new Profile { Name = "Stalker", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 5.5f, MovementCoastFrames = 2, CursorMaxSpeed = 1300f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Copied Skill ⭐ (varies per build!)",
                    "F2  → Raid",
                    "F3  → Stealth (Toggle)",
                    "F4  → Backstab",
                    "F5  → Plagiarism (copy again)",
                    "Ctrl+F1 → Intimidate"
                },
                "Apply Plagiarism (F5) to a monster → copy its skill!\nAssign the copied skill to F1 after copying."
            );
            return p;
        }

        private static Profile CreateWhitesmithProfile()
        {
            var p = new Profile { Name = "Whitesmith", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 5.5f, MovementCoastFrames = 2, CursorMaxSpeed = 1200f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Cart Termination ⭐ (Turbo!)",
                    "F2  → Hammerfall (AoE Stun)",
                    "F3  → Adrenaline Rush (Toggle)",
                    "F4  → Weapon Perfection (Toggle)",
                    "F5  → Maximum Over Thrust (Toggle)",
                    "F6  → Cart Revolution",
                    "Ctrl+F1 → Skin Tempering (Toggle)"
                },
                "Before fighting: activate F3 + F4 + F5 (stack buffs)!\nHammerfall (F2) → Cart Termination (F1) Turbo Combo."
            );
            return p;
        }

        private static Profile CreateCreatorProfile()
        {
            var p = new Profile { Name = "Creator", Class = "Melee", IsBuiltIn = true,
                MageEnabled = true, ActionSpeed = 5.0f, MovementCoastFrames = 3,
                CursorMaxSpeed = 1000f, CursorCurve = 1.8f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Acid Demonstration ⭐ (Turbo!)",
                    "F2  → Potion Pitcher (auf Ally)",
                    "F3  → Summon Homunculus",
                    "F4  → Homunculus Skill",
                    "F5  → Chemical Protection Weapon",
                    "F6  → Chemical Protection Armor",
                    "Ctrl+F1 → Prepare Potion"
                },
                "Acid Demo (F1) Turbo on target for maximum damage.\nPotion Pitcher (F2): point cursor at ally, then L1+B."
            );
            return p;
        }

        private static Profile CreateTaekwonProfile()
        {
            var p = new Profile { Name = "Taekwon", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 5.5f, MovementCoastFrames = 2, CursorMaxSpeed = 1200f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Roundhouse Kick ⭐",
                    "F2  → Counter Kick",
                    "F3  → Tornado Kick",
                    "F4  → Heel Drop",
                    "F5  → Flying Kick (gap close)",
                    "Ctrl+F1 → Tumbling (dodge)"
                },
                "Kick flow combo: F1 → F2 → F3 → F4 alternating!\nFlying Kick (F5) for fast gap closing."
            );
            return p;
        }

        private static Profile CreateStarGladiatorProfile()
        {
            var p = new Profile { Name = "Star Gladiator", Class = "Melee", IsBuiltIn = true,
                AutoAttackEnabled = true, ActionSpeed = 6.5f, MovementCoastFrames = 1, CursorMaxSpeed = 1500f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Warmth of the Sun/Moon/Stars ⭐",
                    "F2  → Solar/Lunar/Star Wrath",
                    "F3  → Union (mit Soul Linker)",
                    "F4  → Feeling of the Sun (Map-Mark)",
                    "F5  → Miracle (situativ)",
                    "Ctrl+F1 → Leap (fast movement)"
                },
                "Activate Warmth (F1) → let enemies run around you!\nLeap (Ctrl+F1) for very fast map traversal."
            );
            return p;
        }

        private static Profile CreateSoulLinkerProfile()
        {
            var p = new Profile { Name = "Soul Linker", Class = "Support", IsBuiltIn = true,
                SupportEnabled = true, MageEnabled = true, ActionSpeed = 3.0f,
                MovementCoastFrames = 5, CursorMaxSpeed = 800f, CursorCurve = 2.0f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Esma ⭐ (damage)",
                    "F2  → Kaahi (Self-Heal Toggle)",
                    "F3  → Kaina (party SP buff)",
                    "F4  → Eske (STR/DEX buff on ally)",
                    "F5  → Eska (DEF buff on ally)",
                    "F6  → Estin / Estun",
                    "Ctrl+F1 → Ka-buff on Star Gladiator/Taekwon"
                },
                "Keep Kaahi (F2) active = automatic self-heal!\nFor buffs: point cursor at ally → press F4 or F5."
            );
            return p;
        }

        private static Profile CreateGunslingerProfile()
        {
            var p = new Profile { Name = "Gunslinger", Class = "Ranged", IsBuiltIn = true,
                KiteEnabled = true, ActionSpeed = 5.0f, MovementCoastFrames = 3, CursorMaxSpeed = 1200f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Desperado ⭐ (AoE Pistol)",
                    "F2  → Gatling Fever (Toggle)",
                    "F3  → Tracking (single target)",
                    "F4  → Coin Flip",
                    "F5  → Full Buster (Shotgun)",
                    "F6  → Rapid Shower",
                    "Ctrl+F1 → Fire Dance",
                    "Ctrl+F2 → Spread Attack"
                },
                "Build Coins (F4) → Gatling Fever (F2) → spam Desperado (F1)!\nTracking (F3) for high single-target damage on bosses."
            );
            return p;
        }

        private static Profile CreateNinjaProfile()
        {
            var p = new Profile { Name = "Ninja", Class = "Melee", IsBuiltIn = true,
                MageEnabled = true, ActionSpeed = 6.0f, MovementCoastFrames = 2,
                CursorMaxSpeed = 1300f, CursorCurve = 1.8f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Throw Huuma Shuriken ⭐",
                    "F2  → Ninja Aura (Toggle)",
                    "F3  → Cicada Skin Shed (evade!)",
                    "F4  → Blaze Shield / Freezing Spear",
                    "F5  → Shadow Leap",
                    "F6  → Throw Shuriken",
                    "Ctrl+F1 → Exploding Dragon",
                    "Ctrl+F2 → Lightning Jolt"
                },
                "Press Cicada (F3) IMMEDIATELY when in danger – grants brief invincibility!\nShadow Leap (F5) for fast repositioning."
            );
            return p;
        }

        private static Profile CreateSuperNoviceProfile()
        {
            var p = new Profile { Name = "Super Novice", Class = "Melee", IsBuiltIn = true,
                ActionSpeed = 4.0f, MovementCoastFrames = 3, CursorMaxSpeed = 1000f };
            ApplyBaseLayout(p);
            ApplySkillInfo(p,
                new List<string> {
                    "F1  → Heal",
                    "F2  → Fire Bolt / Cold Bolt",
                    "F3  → Bash / Double Attack",
                    "F4  → Blessing",
                    "F5  → Increase AGI",
                    "F6  → Mammonite",
                    "F7  → Safety Wall",
                    "Ctrl+F1 → Kyrie Eleison"
                },
                "Super Novice has skills from all classes!\nAdjust F-keys in-game to match your build."
            );
            return p;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Skill Recommendations & Class Tips
        // ─────────────────────────────────────────────────────────────────────

        private static void ApplySkillInfo(Profile p, List<string> skills, string tips)
        {
            p.SkillRecommendations = skills;
            p.ClassTips = tips;
        }

        private static ButtonAction Key(VirtualKey key, string label, bool turbo = false, int turboMs = 100)
            => new() { Type = ActionType.Key, Key = key, Label = label, TurboEnabled = turbo, TurboIntervalMs = turboMs };

        private static ButtonAction KeyWithMod(VirtualKey modifier, VirtualKey key, string label)
            => new() { Type = ActionType.Key, ModifierKey = modifier, Key = key, Label = label };

        private static ButtonAction Click(ActionType type, string label)
            => new() { Type = type, Label = label };

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "unnamed_profile";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            // Trim trailing dots/spaces (Windows disallows them in filenames)
            name = name.Trim('.', ' ');
            if (string.IsNullOrWhiteSpace(name)) name = "unnamed_profile";
            return name;
        }
    }
}
