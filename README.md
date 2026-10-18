# BTX Skill Based Initiative

**BTX_SkillBasedInit** is a fork of IceRaptor's [SkillBasedInit](https://github.com/BattletechModders/SkillBasedInit) 3.1.0, made for the BattleTech Extended (BEX/BTX) modpack. It uses its own mod name, DLL, Harmony ID and log, so it can't be confused with the SkillBasedInit that RogueTech ships.

The main change: **vanilla and modpack initiative bonuses and penalties work.** Upstream SBI ignores the game's `BaseInitiative` stat in combat, so command consoles, comms equipment, DNI cockpits, pilot abilities, quirks and TisButAScratch injuries did nothing. The fork converts each vanilla point into SBI phases (3 by default).

See [DESIGN.md](DESIGN.md) for the full design, a three-way comparison of vanilla / upstream SBI / this fork for every initiative event, and the list of upstream bugs fixed.

### What's different from upstream

* **Vanilla/BEX initiative modifiers apply.** Each point of `BaseInitiative` change is worth `VanillaModifiers.PhasesPerPoint` phases (default 3), with lines for them in the combat tooltip, Mech Bay and lance screen.
* **Called shot and Vigilance are skill-based but not random.**
  * Called shot: a 3-phase delay, plus the amount by which the attacker's Gunnery/Tactics average beats the target's Guts/Tactics average.
  * Vigilance: 3 phases earlier next round, plus the unit's Guts/Tactics average.
* **Knockdowns** apply only SBI's prone penalty. The vanilla knockdown delay is suppressed, so the penalty isn't counted twice.
* **Melee hits** stagger the target: 3 phases later, plus how much the attacker's Piloting/Tactics average beats the target's Guts/Piloting average. Vanilla melee doesn't change initiative at all.
* **Melee push-back** gear (`MeleeHitPushBackPhases`, from vanilla or CBTBE) adds 3 more phases per point.
* **Reinforcements** that arrive after round 1 get a normal roll plus 5 phases, instead of acting almost first.
* **Hesitation stacks** when a unit reserves in consecutive rounds, up to the last phase. Refused reserves cost nothing.
* **Random rolls are reproducible.** Each roll is derived from the mission, unit, round and roll type, so reloading a save gives the same rolls and the tooltip shows the values combat actually used. Default per-round randomness is 3 to 9 phases (upstream 2 to 10).
* **Saves made mid-round load correctly.** Upstream reset any unit in phases 6–30 to vanilla initiative on load.

This mod uses assets from [https://game-icons.net/], which are licensed through a CC BY 3.0 license. The icons are modified only to make them transparent or change their color.

## Summary

The skill level of each Pilot becomes very important in this mod. Novice MechWarriors with low tactics or piloting skill will typically act later in the round, even in light or medium mechs. Veteran MechWarriors can push the limits of their equipment and have heavy or assault mechs going at the same time as unskilled lights. Equipment that grants initiative in the base game or modpack (command consoles, comms gear, advanced cockpits) boosts the weak or helps the elite become even more fearsome.

Instead of four weight classes (light / medium / heavy / assault), this mod divides units into 10-ton groups. 20-25 ton mechs are slightly faster than 30-35 ton mechs. 40-45 ton mechs are faster than 50-55 ton mechs, while 60-65 ton mechs are faster than 70-75 ton mechs. 80-85 tons mechs are faster than 90-95, which are faster than 100. This offers a small bonus to pilots of lighter units.

In addition, this mod emphasizes a MechWarrior's Tactics, Guts, and Piloting skills.

* *Tactics* - contributes directly to the initiative value. If you want your Mechwarriors to consistently achieve a high phase number, increase their Tactics skill.
* *Piloting* reduces the randomness of many calculations, providing a more consistent result round over round.
* *Gunnery* is used during called shots by the attacker; it increases the initiative penalty the target suffers.
* *Guts* is used during called shots by the target to reduce the initiative loss, and increases the Vigilance bonus.

Almost all values are available through tooltips in the mech bay, lance drop and combat UI screens. Hover over the initiative badges (the hexagons) and many details of the system will be defined. Every signed modifier in the tooltips reads like initiative: **+ means acts earlier** (green), and unsigned red numbers are delays.

### Dependencies

This mod requires the following mods to function properly:

  * [https://github.com/battletechmodders/IRBTModUtils/]
  * [https://github.com/BattletechModders/CustomComponents/]
  * [https://github.com/BattletechModders/CustomBundle/tree/master/CustomUnits] (CustomUnits and Custom Ammo Categories)

[Tis But a Scratch (TBAS)](https://github.com/ajkroeg/TisButAScratch) works well with this mod: its injuries that change `BaseInitiative` now apply, scaled like any other vanilla modifier.

### Conflicts

`mod.json` declares `ConflictsWith` for RogueTech's `SkillBasedInit`, `ExpandedInitiative`, `Better Initiative` and `The Engine Initiative`. Disable whichever of those your pack uses before enabling this mod. ExpandedInitiative's own `ConflictsWith` only names `SkillBasedInit`, so it won't catch this mod on its own.

LowVisibility orders itself after SBI through `OptionallyDependsOn`. Change that entry to `BTX_SkillBasedInit` in your LowVisibility `mod.json`.

### Installation

Build the solution in **Release**. The build assembles an installable folder at `dist\BTX_SkillBasedInit` (DLL, `mod.json`, `mod_localized_text.json`, icons). Copy that folder into your `Mods` directory, disable any conflicting initiative mod, and delete `Mods\.modtek` to force a cache rebuild. The build never writes into the game folder.

Building needs a `Directory.Build.props` in the repo root (it's gitignored) that points at your game:

```xml
<Project>
    <PropertyGroup>
        <BattleTechGameDir>D:\Program Files\Steam\steamapps\common\BATTLETECH</BattleTechGameDir>
    </PropertyGroup>
</Project>
```

### Disabling The Mod

To disable the mod, edit `BTX_SkillBasedInit/mod.json` and change `Enabled: true` to `Enabled: false`, or remove the folder. Vanilla initiative (5 phases) returns, and vanilla/modpack initiative equipment keeps working as it does in the base game.

## Technical Details

At the start of each round (`AbstractActor.OnNewRound`) the mod calculates a new initiative value using the following calculation. `actor.Initiative` holds the phase the unit acts in (phase 1 acts first); the UI shows `31 - phase`.

`round_init = tonnage_mod + unit_type_mod + sum(pilot_tag_mods) + injury_mod + misc_mod + vanilla_mod + called_shot_state + melee_pushback_state + vigilance_state + prone_mod + crippled_mod + shutdown_mod - tactics_mod + inspired_mod + randomness_mod + hesitation_state`

Each of these modifiers are described in further detail below. Note that some values are referred to as *mod*ifiers and some are *state*. Modifiers are statistics that you can freely adjust with status effects, without interfering with the internals of SBI. State variables are also statistics, but will be consumed (reset to 0) when the actor's initiative is calculated at the start of the turn. Because of this property you should *NOT* apply status effects to them. When status effects are removed (due to damage or expiration) they reverse their changes. This can lead to very odd situations where SBI changed a value from 2 to 0, which then becomes -2 because the status effect was removed. You've been warned.

### Settings

New in the fork (all in `mod.json` under `Settings`):

| Setting | Default | Description |
| -- | -- | -- |
| `VanillaModifiers.PhasesPerPoint` | 3 | Phases per point of vanilla `BaseInitiative` change |
| `CalledShot.BasePhases` | 3 | Base called shot delay before the skill difference |
| `Vigilance.BasePhases` | 3 | Base Vigilance bonus before the Guts/Tactics average |
| `MeleeHit.BasePhases` | 3 | Flat delay on the target of a melee hit that does damage, before the skill term. 0 turns melee hits off. |
| `MeleePushBack.PhasesPerPoint` | 3 | Delay per point of `MeleeHitPushBackPhases` |
| `Arrival.DelayPhases` | 5 | Extra delay for units arriving after round 1, in the round they arrive |

`CalledShotRandMin/Max` and `VigilanceRandMin/Max` were removed. They're ignored if an old `mod.json` still has them.

### List of Statistics

| Statistic | Source | Type | Description | Default Value |
| -- | -- | -- | -- | -- |
| SBI_MOD_INJURY | Actor | Integer | Direct modifier to initiative from injuries. TBAS injuries that change `BaseInitiative` are picked up through the vanilla modifier instead. | 0 |
| SBI_MOD_MISC | Actor | Integer | Direct modifier to initiative for miscellaneous effects. | 0 |
| SBI_MOD_CALLED_SHOT_ATTACKER | Actor | Integer | Modifies the calculation of the called shot penalty (see below). Read from the attacking actor. | 0 |
| SBI_MOD_CALLED_SHOT_TARGET | Actor | Integer | Modifies the called shot penalty (see below). Read from the defending actor. | 0 |
| SBI_MOD_VIGILANCE | Actor | Integer | Modifies the vigilance bonus that will be applied to SBI_STATE_VIGILANCE. | 0 |
| SBI_MOD_HESITATION | Actor | Integer | A modifier to the *generated* hesitation, that will be added to the SBI_STATE_HESITATION statistic. | 0 |
| SBI_MOD_GUNNERY | Pilot | Integer | Indirect modifier that alters the calculated modifier from the gunnery skill (see below) | 0 |
| SBI_MOD_GUTS | Pilot | Integer | Indirect modifier that alters the calculated modifier from the guts skill (see below) | 0 |
| SBI_MOD_PILOTING | Pilot | Integer | Indirect modifier that alters the calculated modifier from the piloting skill (see below) | 0 |
| SBI_MOD_TACTICS | Pilot | Integer | Indirect modifier that alters the calculated modifier from the tactics skill (see below) | 0 |
| SBI_STATE_TONNAGE | Actor | Integer | Maps the actor's tonnage to a starting init value. Mapping is defined in mod.json, as InitBaseByTonnage dictionary. Calculated at the start of combat and never changed. | 0 |
| SBI_STATE_UNIT_TYPE | Actor | Integer | Direct modifier to initiative based upon the actor's type - mech, trooper squad, naval, vehicle, etc. Calculated at the start of combat and never changed. | 0 |
| SBI_STATE_PILOT_TAGS | Actor | Integer | Direct modifiers to initiative based on a pilot tag. Mapping defined in mod.json in Pilot.PilotTagModifiers. Calculated at the start of combat and never changed. | 0 |
| SBI_STATE_CALLED_SHOT | Actor | Integer | The current total called shot penalty to apply on the following turn. | 0 |
| SBI_STATE_MELEE_PUSHBACK | Actor | Integer | *(fork)* Melee push-back penalty to apply on the following turn. | 0 |
| SBI_STATE_VIGILANCE | Actor | Integer | The current total vigilance bonus to apply on the following turn. | 0 |
| SBI_STATE_HESITATION | Actor | Integer | The current total hesitation penalty to apply on the following turn. | 0 |
| SBI_STATE_LAST_RESERVE_ROUND | Actor | Integer | *(fork)* The round the unit last reserved in, used for hesitation stacking. 0 = never. | 0 |

### Skill Normalization
This mod uses a normalized skill value in many places. Because skill values can range from 1-20 in some packs, but initiative phases only go from 30-1, we normalize skill values to smaller modifier values. The table below shows the correspondence between skill value and modifier:

| Skill |  1  |  2  |  3  |  4  |  5  |  6  |  7  |  8  |  9  |  10  | 11 | 12 | 13 |
| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | --  |
| Modifier  | +0 | +1 | +1 | +2 | +2 | +3 | +3 | +4 | +4 | +5 | +6 | +7 | +8 |

In addition, SBI offers statistics that allow mod authors to modify these normalized modifiers. The statistics `SBI_MOD_GUNNERY`, `SBI_MOD_GUTS`, `SBI_MOD_PILOTING`, and `SBI_MOD_TACTICS` are added to the calculated modifier above for their respective skill calculation.

(!) The skill statistics are read from the PILOT stat collection, NOT the actor stat collection. Make sure your effects target the proper collection!

> Example: A Pilot with tactics of 7 has a SBI_MOD_TACTICS value of 2. Their normalized skill modifier is +3, +2 for the SBI_MOD_TACTICS value, for a total of 5.

### Vanilla Initiative Modifiers

Each round, the fork reads the actor's `BaseInitiative` statistic and takes its current value minus its default (the weight-class value set when the unit was created). That difference is multiplied by `VanillaModifiers.PhasesPerPoint` and added to `round_init`. Vanilla content uses `Int_Add -1` for "+1 initiative", which is one phase earlier, so a command console moves its unit 3 phases earlier by default. Everything that changes `BaseInitiative` is included without listing it: gear, lance-wide gear, pilot abilities, MechAffinity quirks, TBAS injuries.

Out of combat, the Mech Bay and lance screen mirror vanilla's own calculation. They count self-targeted effects from components and pilot abilities, and lance-wide effects come in through the lance modifier, so nothing is counted twice.

`PhaseModifier` and `PhaseModifierSelf` are not read. In vanilla they only carry the prone carry-over and Vigilance, and both are replaced by SBI's own versions.

### Tonnage Modifier

The `tonnage_mod` is calculated by comparing the unit's tonnage against the relevant UnitConfig.InitBaseByTonnage dictionary. The keys are sorted, and the value of the smallest key that is greater than or equal to the unit's tonnage is used. Tonnage above every key uses the largest key. Combat and the Mech Bay use the same lookup. The calculation of tonnage varies by unit type, as shown below:

* Mech.InitBaseByTonnage - The tonnage in the mechDef is used.
* Trooper.InitBaseByTonnage - As this is a CustomUnits type, tonnage is pulled from the mechDef. This tonnage is divided by the number of troopers configured for the squad.
* Naval.InitBaseByTonnage - As this is a CustomUnits type, tonnage is pulled from the mechDef.
* Vehicle.InitBaseByTonnage - If the unit is a true vehicle, tonnage is pulled from the vehicleDef. If it's a CustomUnits FakeVehicle, tonnage is pulled from the mechDef.
* Turret.InitBaseByTonnage - The unit's tags are scanned for `unit_light`, `unit_medium`, or `unit_heavy`. These are matched against `Turret.LightTonnage`, `Turret.MediumTonnage`, and `Turret.HeavyTonnage` respectively. If one of these tags are not found, Turret.DefaultTonnage is used instead

> Example: For a configuration of Unit.InitBaseByTonnage = { "35" : 16, "75" : 12, "100" : 9, "999" : 6 } units between 0-35 tons would have a base init of 16, units between 36-75 tons would have a base init of 12, and units between 76-100 would have a base init of 9. Units between 101-999 tons would use a base init of 6.

### Unit Type Modifier

The `unit_type_mod` is a direct modifier assigned from the `Unit.TypeMod` value for the relative unit type. The unit type is determined via the following algorithm:

* Turrets are defined as HBS Turret types
* Vehicles are either HBS Vehicle types, or CustomUnit FakeVehicles with isVehicle=true
* Naval units are CustomUnits with Naval=true
* Trooper Squads are CustomUnits with SquadInfo.Troopers > 1
* Everything else is a Mech

### Called Shot Modifier

When a unit is targeted by the Called Shot ability, it suffers an initiative penalty. In the fork there is no random element:

`penalty = CalledShot.BasePhases + max(0, attacker_skill - target_skill)`

* `attacker_skill` is the average of the attacker's normalized gunnery and tactics modifiers, plus `SBI_MOD_CALLED_SHOT_ATTACKER` (inverted, as it's a phase modifier).
* `target_skill` is the average of the target's normalized guts and tactics modifiers, plus `SBI_MOD_CALLED_SHOT_TARGET` (inverted).

If the target hasn't acted this round, the penalty applies immediately; otherwise it's stored and applied next round. Vanilla's own called shot delay is not applied.

> Example: The attacker has gunnery 10 and tactics 10 (+5 average). The target has guts 6 and tactics 6 (+3 average). The penalty is 3 + (5 - 3) = 5 phases.

### Vigilance Modifier

When the Vigilance ability is used, the unit gains an initiative bonus for next round. In the fork there is no random element:

`bonus = Vigilance.BasePhases + average(guts, tactics) + SBI_MOD_VIGILANCE`

The bonus never becomes a penalty. Braced and stability effects from Vigilance are unchanged.

> Example: The pilot has guts 6 (+3) and tactics 10 (+5), for a +4 average. The unit has SBI_MOD_VIGILANCE = -2, from a poor cockpit design. The bonus is 3 + 4 - 2 = 5 phases earlier next round.

### Melee Hit

Any melee attack (including DFA) that does damage delays the target by `MeleeHit.BasePhases` + max(0, attacker's average Piloting and Tactics modifier − target's average Guts and Piloting modifier). There's no randomness. If the target has already acted, or is in the middle of its own activation, the delay is stored in `SBI_STATE_MELEE_PUSHBACK` and applied next round.

> Example: The attacker has piloting 10 (+5) and tactics 6 (+3), for a +4 average. The target has guts 4 (+2) and piloting 4 (+2), for +2. The target is delayed 3 + (4 - 2) = 5 phases.

### Melee Push-back

Equipment with `MeleeHitPushBackPhases` (vanilla, or CBTBE's melee) delays the target by `MeleePushBack.PhasesPerPoint` phases per point on a melee hit. If the target has already acted, the delay is stored in `SBI_STATE_MELEE_PUSHBACK` and applied next round.

### Randomness Modifier

Each turn a unit has a random amount of initiative subtracted from its base score. The bounds are configured by unit type, as `Unit.RandomnessMin` and `Unit.RandomnessMax` (defaults -3 and -9). A value within this range (inclusive) is used for `randomness_mod`. Before rolling, the normalized Piloting modifier is added to `Unit.RandomnessMax`, which represents a more skilled pilot being able to capitalize on their machine. The upper bound never drops below the lower one.

Rolls are deterministic: each one is a hash of the contract, the unit, the round and the kind of roll. Reloading a save reproduces the same rolls, and the tooltip shows the same values that combat used.

> Example: Vehicles are configured with Vehicle.RandomnessMin = -3 and Vehicle.RandomnessMax = -9. A 3 to 9 phase delay will be applied to each vehicle's base initiative. If a vehicle has a Pilot skill of 5 their normalized modifier for that skill would be +2. This makes their bounds -3 to (-9 + 2) = -7.

### Prone Modifier

When a Mech is knocked prone, it suffers penalty to initiative configured as Mech.ProneModifierMin and Mech.ProneModifierMax. The penalty will be calculated as a random modifier between these two values. The max bounds will be modified by the unit's normalized skill modifier for piloting. This modifier is applied immediately on the knockdown (if the mech hasn't acted yet), and every turn the unit begins the turn prone. A mech that is already prone gets no extra penalty from being knocked down again.

> Example: A Mech is prone, with Mech.ProneModifierMin = -2 and Mech.ProneModifierMax = -9. The Pilot has a piloting skill of 9, which gives a +4 normalized skill modifier. Their prone modifier will be between -2 and (-9 + 4) = -5.

### Hesitation Modifier

Every time a unit successfully chooses the Reserve option, it accrues a *Hesitation* penalty. A random value between `Unit.HesitationMin` and `Unit.HesitationMax` (defaults -2 and -6) is calculated, with any value from `SBI_MOD_HESITATION` applied to it. The penalty applies to the unit's current phase and is also added to its carried hesitation.

At the start of the next round, the carried hesitation is added to the initiative calculation:

* If the unit **reserved in the previous round**, there is no reduction, so consecutive reserves keep stacking.
* Otherwise the pilot's tactics modifier + 1 is first subtracted from the carried hesitation.

Carried hesitation is capped at 29, so a unit that keeps reserving ends up in the last phase.

### Crippled Modifier

When a unit is crippled it suffers a penalty reflecting the unit not performing at it's combat best. This penalty is configured as Unit.CrippledModifierMin and Unit.CrippledModifierMax, and will be a random value selected between those two bounds. The maximum value is reduced by the normalized piloting skill modifier for the unit.

Trooper Squads cannot be crippled. Other unit types are considered crippled in the following circumstances:

* Mech - Any leg is destroyed (this applies to Quad mechs as well)
* Vehicles - Either the left or right side is destroyed. For CustomUnits vehicles, the left or right torsos count as sides.
* Naval - When the left or right torso (side) is destroyed.

> Example: A Vehicle has lost its left side completely (no structure left). Vehicles are configured with Vehicle.CrippledModifierMin = -5 and Vehicle.CrippledModifierMax = -13. The Pilot has a piloting skill of 3, which gives a +1 normalized skill modifier. The vehicle's crippled modifier will be between -5 and (-13 + 1) = -12.

### Inspired Mod

When a unit reaches the *Inspired* state (by the team gaining enough morale or rage) it gains a bonus to its initiative value. This bonus is configured through the Unit.InspiredMin and Unit.InspiredMax values. The maximum bound will be improved by the normalized skill modifier for the Pilot's tactics skill. The modifier will be a random value selected from between these two values.

> Example: A trooper squad is configured with Trooper.InspiredMin = 2 and Trooper.InspiredMax = 5. They have a Tactics skill of 5, for a +2 normalized skill modifier. Their inspired bonus will range between 2 and (5+2) = 7.

### Arrival

Units that join combat after round 1 (reinforcements, MissionControl extra lances, ambushes) are given a full roll plus `Arrival.DelayPhases`, never earlier than the phase in progress, so they still act in the round they arrive. Units joining in round 1 get the roll without the delay. From the next round they roll normally.

### Changing Colors
The mod applies colors to the following combat UI elements:

* Initiative Hexagon above the Mech Paper doll
* Initiative Hexagon floating above the Mech
* Pilot Name background bar

These colors can be customized through the `mod.json`.

## Planned

Works in progress or planned effects include:

- [] Modify Reserve button to change to 'Reserve to Phase 1' when ALT key is held down. Model pays for all the phases it holds though. (See BTDebug for how to lash to ALT key)
