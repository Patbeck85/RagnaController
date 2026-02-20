# RagnaController — Enhanced Profile System

## Probleme mit aktuellen Profilen

### Current Issues:
1. **Zu generic** — "Skill 2", "Skill 3" sind nicht hilfreich
2. **Nur 4 Profile** — Zu wenig für alle RO-Klassen
3. **Keine Combos** — Keine voreingestellten Skill-Rotationen
4. **Keine Spezialisierung** — PvP vs PvM nicht unterschieden
5. **Suboptimale Layouts** — Wichtige Skills nicht auf besten Buttons

---

## Vorgeschlagene Verbesserungen

### 1. Mehr spezifische Profile (12 statt 4)

#### Melee Classes
1. **Knight / Lord Knight (PvM)**
   - Bowling Bash spam (A + Turbo)
   - Magnum Break → Bowling Bash combo
   - Two-Hand Quicken buff
   - Pierce für MVPs
   
2. **Knight / Lord Knight (PvP)**
   - Bash → Stun combo
   - Spear Boomerang kite
   - Provoke → Bash
   - Defensive: Endure

3. **Assassin / Assassin Cross**
   - Sonic Blow combo (EDP → SB)
   - Grimtooth spam
   - Cloaking toggle
   - Venom Dust → Grimtooth

4. **Crusader / Paladin**
   - Grand Cross (tank spam)
   - Shield Chain
   - Devotion quick-cast
   - Heal macro

#### Ranged Classes
5. **Hunter / Sniper (PvM)**
   - Double Strafe spam mit Kite
   - Falcon Assault
   - Blitz Beat
   - Detect → DS combo

6. **Hunter / Sniper (PvP)**
   - Ankle Snare → Land Mine
   - Sandman → DS
   - Remove Trap
   - Sharp Shooting

#### Mage Classes
7. **Wizard / High Wizard (PvM)**
   - Storm Gust grind
   - Meteor Storm → SG combo
   - Quagmire → SG
   - Stone Curse → Bolt

8. **Wizard / High Wizard (PvP)**
   - Safety Wall → Bolt spam
   - Quagmire → Jupitel Thunder
   - Lord of Vermillion
   - Sight → Bolt

9. **Sage / Professor**
   - Volcano/Deluge/Whirlwind
   - Land Protector
   - Dispell
   - Bolt spam

#### Support Classes
10. **Priest / High Priest (WoE)**
    - Pneuma quick-cast
    - Sanctuary spam
    - SW → Heal macro
    - Assumptio buff

11. **Priest / High Priest (Party)**
    - Heal macro (F1-F6 party members)
    - Blessing → Agi → Kyrie combo
    - Resurrect
    - Gloria → Magnificat

12. **Monk / Champion**
    - Asura Strike combo
    - Triple Attack spam
    - Occult Impaction
    - Snap positioning

---

### 2. Realistische Button-Layouts

#### Beispiel: Lord Knight (PvM)

**Prinzip:** Häufigste Skills auf beste Buttons

```
Face Buttons (Most Used):
  A = Bowling Bash (Turbo)       ← Main DPS
  B = Magnum Break               ← AoE
  X = Pierce                     ← Single Target
  Y = Two-Hand Quicken           ← Buff

Shoulders (Quick Access):
  RB = HP Potion (F1)
  LB = Lock Target

D-Pad (Secondary Skills):
  ↑ = Concentration (F5)
  ↓ = Endure (F6)
  ← = Provoke (F7)
  → = Auto-Berserk (F8)

L2 Layer (Utility):
  L2+A = White Potion
  L2+B = Blue Potion
  L2+X = Yggdrasil Berry
  L2+Y = Fly Wing
  L2+↑/↓/←/→ = Items 1-4

R2 Layer (Advanced):
  R2+A = Click Move
  R2+B = Tab Target
  R2+X = Sit/Stand
  R2+Y = Warp Portal
```

---

### 3. Klassen-spezifische Combos

#### Knight: "Bash Stun Combo"
```
Macro: bash_stun_combo.json
Steps:
  1. Provoke (V)       - 0ms
  2. Wait              - 100ms
  3. Bash (Z)          - 0ms
  4. Wait              - 150ms
  5. Bash (Z)          - 0ms
  6. Wait              - 150ms
  7. Bash (Z)          - 0ms

Total: 400ms
Result: Provoke → Triple Bash = Stun Lock
```

#### Assassin: "EDP Sonic Blow"
```
Macro: edp_sonic_blow.json
Steps:
  1. Enchant Deadly Poison (F1)  - 0ms
  2. Wait                        - 1500ms (EDP cast)
  3. Sonic Blow (Z)              - 0ms
  4. Wait                        - 200ms
  5. Sonic Blow (Z)              - 0ms

Total: 1700ms
Result: EDP buff → Double Sonic Blow
```

#### Priest: "Full Buff Rotation"
```
Macro: priest_buff_rotation.json
Steps:
  1. Blessing (Z)      - 0ms
  2. Wait              - 800ms
  3. Increase Agi (X)  - 0ms
  4. Wait              - 800ms
  5. Kyrie Eleison (C) - 0ms
  6. Wait              - 1000ms
  7. Assumptio (V)     - 0ms

Total: 2600ms
Result: Full party buff in one button
```

---

### 4. Klassen-spezifische Turbo Settings

```
Class-Optimized Turbo Intervals:
┌─────────────┬──────────┬─────────────────────┐
│ Class       │ Interval │ Reason              │
├─────────────┼──────────┼─────────────────────┤
│ Knight      │ 80ms     │ Bowling Bash ASPD   │
│ Assassin    │ 50ms     │ High ASPD builds    │
│ Hunter      │ 60ms     │ Double Strafe       │
│ Wizard      │ 120ms    │ Bolt cast time      │
│ Priest      │ 100ms    │ Heal spam           │
│ Monk        │ 40ms     │ Triple Attack       │
└─────────────┴──────────┴─────────────────────┘
```

---

### 5. Advanced Features per Class

#### Knight Auto-Combo System
```
When A (Bowling Bash) held:
  1. Check if enemy in range
  2. If yes: Bowling Bash
  3. If no: Magnum Break (pull) → Bowling Bash
  4. Repeat

Result: Never miss combo opportunity
```

#### Assassin Cloaking Manager
```
When Y pressed:
  Toggle Cloaking ON
  → Auto-disable when attacking
  → Auto-enable after 3 seconds idle

Result: Always invisible when not fighting
```

#### Wizard Safety Net
```
When HP < 30%:
  Auto-cast Safety Wall (C)
  → Teleport (F8)
  → Heal

Result: Auto-survive mechanism
```

---

## Implementation Plan

### Phase 1: Enhanced Built-in Profiles
```
Replace current 4 profiles with:
1. Knight (PvM)
2. Assassin (PvM)  
3. Hunter (PvM)
4. Wizard (PvM)
5. Priest (Party)
6. Monk (PvM)
```

### Phase 2: Profile Templates Library
```
Add to Profile Library:
- PvP variants for each
- WoE-specific (Crusader, Priest)
- MVP hunting (Sniper, Wizard)
- Leveling (all classes)
```

### Phase 3: Pre-installed Macros
```
Bundle common macros:
- Buff rotations
- Skill combos
- Emergency sequences
```

---

## Specific Improvements per Current Profile

### Melee Profile → Knight Profile

**Changes:**
```
Before:
  A = "Basic Attack" (generic)
  B = "Skill 2" (unclear)
  X = "Skill 3" (unclear)

After:
  A = "Bowling Bash" (clear purpose)
  B = "Magnum Break" (AoE pull)
  X = "Pierce" (single target DPS)
  Y = "Two-Hand Quicken" (buff)
```

**Add:**
- Macro: BB spam optimal timing
- Auto-Magnum when surrounded
- Quick buff access (L2+skills)

---

### Ranged Profile → Hunter Profile

**Changes:**
```
Before:
  Generic "Ranged" skills
  
After:
  A = "Double Strafe" (main DPS)
  B = "Arrow Shower" (AoE)
  X = "Falcon Assault" (burst)
  Y = "Blitz Beat" (auto-damage)
  
  L2+A = "Detect" (hidden enemies)
  L2+B = "Remove Trap"
  L2+X = "Ankle Snare"
  L2+Y = "Sandman"
```

**Add:**
- Trap combo macro (Ankle → Land Mine)
- Kite optimization for Sniper ASPD
- Arrow crafting quick-access

---

### Mage Profile → Wizard Profile

**Changes:**
```
Before:
  Generic ground spells
  
After:
  A = "Storm Gust" (main AoE)
  B = "Meteor Storm" (secondary AoE)
  X = "Lord of Vermillion" (fast AoE)
  Y = "Fire Bolt" (single target)
  
  L2+A = "Quagmire" (slow enemies)
  L2+B = "Safety Wall" (defense)
  L2+X = "Stone Curse" (CC)
  L2+Y = "Sight" (reveal)
```

**Add:**
- Quagmire → SG macro
- Safety Wall → Bolt spam
- SP management warnings

---

### Support Profile → Priest Profile

**Changes:**
```
Before:
  Generic support skills
  
After:
  A = "Heal" (F1-F6 cycle)
  B = "Blessing"
  X = "Increase Agi"
  Y = "Kyrie Eleison"
  
  L2+A = "Assumptio"
  L2+B = "Sanctuary"
  L2+X = "Pneuma"
  L2+Y = "Resurrection"
```

**Add:**
- Full buff rotation macro
- Emergency Sanctuary macro
- Party heal targeting system

---

## User Feedback Integration

### Most Requested Improvements:
1. ✅ Realistic skill names
2. ✅ More class variety
3. ✅ Pre-made combos
4. ✅ PvP/PvM variants
5. ✅ Better button layouts

---

## Benefits

### For Users:
- **Plug & Play:** Load profile, skills already mapped
- **Learn Faster:** See optimal skill placements
- **Play Better:** Combos pre-configured
- **Customize Easily:** Start from good base

### For Project:
- **Better UX:** "It just works"
- **Showcase Features:** Macros, turbos used properly
- **Community Growth:** Share custom profiles
- **Professional Polish:** Shows deep game knowledge

---

## Next Steps

**Option A: Full Implementation** (3-4 hours)
- Create 6 enhanced built-in profiles
- Add 10+ profile templates
- Bundle 15+ common macros

**Option B: Incremental** (1-2 hours)
- Enhance current 4 profiles with realistic skills
- Add 3 popular macros
- Document skill mappings

**Option C: Community-Driven** (0 hours dev)
- Keep current system
- Provide profile creation guide
- Let users share profiles

---

**Recommendation:** Option A — Shows professionalism and deep RO knowledge
