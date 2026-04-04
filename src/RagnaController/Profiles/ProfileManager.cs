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
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RagnaController", "Profiles");
        private static readonly JsonSerializerOptions Opt = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        public List<Profile> Profiles { get; } = new();

        public ProfileManager() { Directory.CreateDirectory(Dir); Load(); }

        public void Load()
        {
            Profiles.Clear();
            Profiles.AddRange(CreateBuiltInProfiles());
            if (!Directory.Exists(Dir)) return;
            foreach (var f in Directory.GetFiles(Dir, "*.json"))
            {
                try
                {
                    var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(f), Opt);
                    if (p != null)
                    {
                        p.IsBuiltIn = false;
                        int i = Profiles.FindIndex(x => x.Name == p.Name);
                        if (i >= 0) Profiles[i] = p; else Profiles.Add(p);
                    }
                }
                catch { }
            }
            // Safety net: profile list must never be empty
            if (Profiles.Count == 0)
                Profiles.Add(new Profile { Name = "Novice", Class = "Melee", IsBuiltIn = true });
        }

        private List<Profile> CreateBuiltInProfiles()
        {
            var l = new List<Profile>();
            string[] jobs = {
                "Novice","Swordman","Mage","Archer","Thief","Merchant","Acolyte",
                "Knight","Crusader","Wizard","Sage","Hunter","Blacksmith","Alchemist",
                "Priest","Monk","Assassin","Rogue","Bard","Dancer",
                "Lord Knight","Paladin","High Wizard","Professor","Sniper","Whitesmith",
                "Creator","High Priest","Champion","Assassin Cross","Stalker","Clown",
                "Gypsy","Super Novice","Taekwon","Star Gladiator","Soul Linker",
                "Ninja","Gunslinger"
            };

            foreach (var j in jobs)
            {
                var p = new Profile
                {
                    Name = j, Class = "Job", IsBuiltIn = true,
                    PreRenewalAttackIntervalMs = 120,
                    RenewalAttackIntervalMs    = 80,
                    SkillRecommendations = new List<string> { "F1: Main Skill", "F2: Support", "F3: Potion" },
                    ClassTips = "Default settings for " + j
                };

                // Default mappings for all classes
                p.ButtonMappings["A"]    = new ButtonAction { Type = ActionType.LeftClick, Label = "Attack / Select" };
                p.ButtonMappings["B"]    = new ButtonAction { Type = ActionType.Key, Key = VirtualKey.Enter, Label = "Confirm / Enter" };
                p.ButtonMappings["X"]    = new ButtonAction { Type = ActionType.Key, Key = VirtualKey.AltLeft, Label = "Show Items" };
                p.ButtonMappings["L1+A"] = new ButtonAction { Type = ActionType.Key, Key = VirtualKey.F1, Label = "Skill 1" };
                p.ButtonMappings["L1+B"] = new ButtonAction { Type = ActionType.Key, Key = VirtualKey.F2, Label = "Skill 2" };
                p.ButtonMappings["L1+X"] = new ButtonAction { Type = ActionType.Key, Key = VirtualKey.F3, Label = "Skill 3" };
                p.ButtonMappings["L1+Y"] = new ButtonAction { Type = ActionType.Key, Key = VirtualKey.F4, Label = "Skill 4" };

                // Default emote radial items for all classes
                p.RadialMenuItems = new System.Collections.Generic.List<RadialItem>
                {
                    new RadialItem { Name = "❤ LOVE",  Command = "/lv",  IsEmote = true },
                    new RadialItem { Name = "💋 KISS",  Command = "/kis", IsEmote = true },
                    new RadialItem { Name = "😂 HAHA",  Command = "/heh", IsEmote = true },
                    new RadialItem { Name = "😢 CRY",   Command = "/sob", IsEmote = true },
                    new RadialItem { Name = "😰 SWEAT", Command = "/swt", IsEmote = true },
                    new RadialItem { Name = "😱 OMG",   Command = "/omg", IsEmote = true },
                    new RadialItem { Name = "🙏 SORRY", Command = "/sry", IsEmote = true },
                    new RadialItem { Name = "👍 NICE",  Command = "/thx", IsEmote = true },
                };

                // Class-specific combo configuration

                // Monk — Triple Attack triggers on auto-attack; hold combo: Chain Combo → Combo Finish → Asura Strike
                if (j == "Monk")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A after Triple Attack.";
                    p.ComboSkillNames        = new List<string> { "Chain Combo", "Combo Finish", "Asura Strike" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 };
                    p.PreRenewalComboDelays  = new List<int> { 350, 400, 1000 };
                    p.RenewalComboDelays     = new List<int> { 250, 300,  800 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Monk Combo" };
                    p.SkillRecommendations   = new List<string> { "F1: Chain Combo", "F2: Combo Finish", "F3: Asura Strike", "R2+A: Auto-Combo (halten)" };
                }

                // Champion — same chain as Monk with shorter delays due to higher base stats
                else if (j == "Champion")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A after Triple Attack.";
                    p.ComboSkillNames        = new List<string> { "Chain Combo", "Combo Finish", "Asura Strike" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 };
                    p.PreRenewalComboDelays  = new List<int> { 300, 350,  900 };
                    p.RenewalComboDelays     = new List<int> { 220, 280,  750 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Champion Combo" };
                    p.SkillRecommendations   = new List<string> { "F1: Chain Combo", "F2: Combo Finish", "F3: Asura Strike", "R2+A: Auto-Combo (halten)" };
                }

                // Taekwon — activate Kick Stance, then auto-fire Combo Kick
                else if (j == "Taekwon")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A for Kick Sequence.";
                    p.ComboSkillNames        = new List<string> { "Kick Stance", "Combo Kick" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2 };
                    p.PreRenewalComboDelays  = new List<int> { 300, 400 };
                    p.RenewalComboDelays     = new List<int> { 200, 300 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Kick Combo" };
                    p.SkillRecommendations   = new List<string> { "F1: Kick Stance", "F2: Combo Kick", "R2+A: Kick Combo (halten)" };
                }

                // Star Gladiator — Solar Stance → Mild Wind → Stellar Kick
                else if (j == "Star Gladiator")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A for Stellar Combo.";
                    p.ComboSkillNames        = new List<string> { "Solar Stance", "Mild Wind", "Stellar Kick" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 };
                    p.PreRenewalComboDelays  = new List<int> { 280, 350, 450 };
                    p.RenewalComboDelays     = new List<int> { 200, 270, 350 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Stellar Combo" };
                    p.SkillRecommendations   = new List<string> { "F1: Solar Stance", "F2: Mild Wind", "F3: Stellar Kick", "R2+A: Stellar Combo (halten)" };
                }

                // Soul Linker — two Ka spells then Esma for maximum damage
                else if (j == "Soul Linker")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A for Esma Chain.";
                    p.ComboSkillNames        = new List<string> { "Ka Spell 1", "Ka Spell 2", "Esma" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 };
                    p.PreRenewalComboDelays  = new List<int> { 400, 400, 600 };
                    p.RenewalComboDelays     = new List<int> { 300, 300, 500 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Esma Combo" };
                    p.SkillRecommendations   = new List<string> { "F1: Ka Spell 1", "F2: Ka Spell 2", "F3: Esma", "R2+A: Esma Combo (halten)" };
                }

                // Ninja — Shuriken → Shadow Leap → Final Strike
                else if (j == "Ninja")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A for Ninja Chain.";
                    p.ComboSkillNames        = new List<string> { "Throw Shuriken", "Shadow Leap", "Final Strike" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2, VirtualKey.F3 };
                    p.PreRenewalComboDelays  = new List<int> { 200, 300, 400 };
                    p.RenewalComboDelays     = new List<int> { 150, 220, 320 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Ninja Combo" };
                    p.SkillRecommendations   = new List<string> { "F1: Throw Shuriken", "F2: Shadow Leap", "F3: Final Strike", "R2+A: Ninja Combo (halten)" };
                }

                // Gunslinger — Desperado → Chain Action
                else if (j == "Gunslinger")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A for Rapidfire.";
                    p.ComboSkillNames        = new List<string> { "Desperado", "Chain Action" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2 };
                    p.PreRenewalComboDelays  = new List<int> { 200, 300 };
                    p.RenewalComboDelays     = new List<int> { 150, 220 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Rapidfire" };
                    p.SkillRecommendations   = new List<string> { "F1: Desperado", "F2: Chain Action", "R2+A: Rapidfire (halten)" };
                }

                // Sniper — Arrow Shower → Focused Arrow Strike
                else if (j == "Sniper")
                {
                    p.ComboEnabled = true;
                    p.ClassTips   += "\nCombo Engine active! Hold R2+A for Arrow Combo.";
                    p.ComboSkillNames        = new List<string> { "Arrow Shower", "Focused Arrow Strike" };
                    p.ComboSequenceVK        = new List<VirtualKey> { VirtualKey.F1, VirtualKey.F2 };
                    p.PreRenewalComboDelays  = new List<int> { 220, 320 };
                    p.RenewalComboDelays     = new List<int> { 160, 240 };
                    p.ButtonMappings["R2+A"] = new ButtonAction { Type = ActionType.Combo, Label = "Arrow Combo" };
                    p.SkillRecommendations   = new List<string> { "F1: Arrow Shower", "F2: Focused Arrow Strike", "R2+A: Arrow Combo (halten)" };
                }

                l.Add(p);
            }
            return l;
        }

        public void SaveProfile(Profile p)
        {
            string path = Path.Combine(Dir, p.Name + ".json");
            // Auto-backup previous version before overwrite
            if (File.Exists(path))
            {
                string bak = Path.Combine(Dir, p.Name + ".bak.json");
                try { File.Copy(path, bak, overwrite: true); } catch { }
            }
            bool was = p.IsBuiltIn;
            p.IsBuiltIn = false;
            File.WriteAllText(path, JsonSerializer.Serialize(p, Opt));
            p.IsBuiltIn = was;
        }

        public void AddAndSave(Profile p) { Profiles.Add(p); SaveProfile(p); }
        public void Export(Profile p, string path) => File.WriteAllText(path, JsonSerializer.Serialize(p, Opt));
        public Profile? ImportPreview(string path)
        {
            var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Opt);
            if (p != null) p.IsBuiltIn = false;
            return p;
        }
        public void Delete(Profile p)
        {
            if (!p.IsBuiltIn)
            {
                string path = Path.Combine(Dir, p.Name + ".json");
                if (File.Exists(path)) File.Delete(path);
                Profiles.Remove(p);
            }
        }
    }
}
