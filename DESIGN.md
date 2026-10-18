# BTX_SkillBasedInit: design

Fork of IceRaptor's SkillBasedInit 3.1.0 (github.com/BattletechModders/SkillBasedInit), released as **`BTX_SkillBasedInit`** so it is never confused with the SkillBasedInit that RogueTech ships. Goals:

- **Primary:** make vanilla and modpack initiative modifiers (the `BaseInitiative` stat) work under SBI's 30-phase system.
- **Secondary:** fix upstream bugs and retune a few mechanics.

Agreed 2026-10-17. All numbers below are config defaults.

## Phase model (unchanged)

- 30 phases. `actor.Initiative` holds the **phase**, and phase 1 acts first.
- The UI shows `31 - phase`.
- "+N phases" in this document means "acts N phases later".

## Per-round initiative

Computed in the `AbstractActor.OnNewRound` postfix, the same place as upstream:

```
phase = tonnage base + unit type + pilot tags                       (unchanged)
      + VANILLA DELTA x PhasesPerPoint                               (NEW)
      + injury + misc                                               (unchanged SBI hooks)
      + stored called-shot / vigilance                              (now hybrid values, see below)
      + prone + crippled + shutdown                                 (unchanged; null-check fixed)
      - tactics + inspired                                          (unchanged)
      + random delay 3..9, top narrowed by Piloting                  (repo default 2..10, 2.0.0 install 4..12; deterministic roll)
      + carried hesitation (stacks on consecutive reserves, see Events) (roll now 2..6)
clamp 1..30
```

**Vanilla delta** = `BaseInitiative` current value minus its default value (the weight-class value set in `InitStats`). That picks up every gear, ability, quirk and injury effect without listing them. The sign is preserved: content uses `Int_Add -1` to mean "one phase earlier".

Sources in the BEX/BTX pack that this restores:

| Source | Examples |
|---|---|
| Gear (self) | Command Console, SLDF Neurohelmet, DNI cockpits, Hover / VTOL / TG120 drive trains |
| Gear (lance-wide) | Comms Equipment 1–7 t, Command Console + B2000, Special Command Module |
| Pilot | BEX `AbilityDefT8A`, MechAffinity `pilot_madman` |
| Injury | TisButAScratch Concussed / Barotrauma / Blood loss (+1 each) |

`PhaseModifier` is **not** folded in. In vanilla it only carries the prone penalty and Vigilance, and both are replaced below.

## Events

Vanilla works on 5 phases; upstream SBI and the fork work on 30. "Upstream SBI" describes the 3.1.0 code as it runs today, bugs included.

| Event | Vanilla | Upstream SBI 3.1.0 | BTX_SkillBasedInit |
|---|---|---|---|
| **Gear / ability / injury modifiers** (`BaseInitiative` stat) | Each point = 1 phase, permanent while the source is active | **Ignored in combat.** The vanilla stat isn't read. Only SBI's own `SBI_MOD_MISC` / `SBI_MOD_INJURY` count, and no BEX content sets those. | Each point = `VanillaModifiers.PhasesPerPoint (3)` phases, applied every round. |
| **Called shot** (morale attack) | Target +1 phase now (unless already in the last phase), plus a 1-round `AbilityDefInitiativeDelay` effect (`BaseInitiative +1`) | Penalty = random 2..6 + max(0, attacker avg(Gunnery,Tactics) − target avg(Guts,Tactics)) + `SBI_MOD_CALLED_SHOT_*` stats. Applied now if the target hasn't acted, otherwise stored for next round. The vanilla effect is still created but has no effect, since SBI ignores the stat. | Replaces the original. Penalty = `CalledShot.BasePhases (3)` + max(0, attacker avg(Gunnery,Tactics) − target avg(Guts,Tactics)) + `SBI_MOD_CALLED_SHOT_*` stats. **No randomness.** Applied now if the target hasn't acted, otherwise next round. The vanilla effect isn't created, so nothing is double counted. |
| **Vigilance** (morale defend) | `PhaseModifierSelf −1`, so next round is 1 phase earlier. Can't be used again while pending. Also applies Braced + stability reset. | Bonus = random 2..6 + avg(Guts,Tactics) + `SBI_MOD_VIGILANCE`, stored for next round. SBI's prefix sets the unit to `PhaseSpecial` so vanilla returns early, which means `PhaseModifierSelf` is never set. As a side effect, the vanilla "can't re-use while pending" gate never engages. | Replaces the phase part. Bonus = `Vigilance.BasePhases (3)` + avg(Guts,Tactics) + `SBI_MOD_VIGILANCE`, stored for next round. **No randomness.** Braced and stability are untouched. The vanilla re-use gate is left alone (see note below). |
| **Knockdown** | `ForceUnitOnePhaseDown`: +1 phase now plus the delay effect. While prone at the end of its activation, +1 phase next round (`PhaseModifier`). | Prone penalty (random 2..9, reduced by Piloting) applied now if the unit hasn't acted, and re-rolled each round while prone. Vanilla's `ForceUnitOnePhaseDown` **also** runs: +1 phase, or nothing if the unit sits at phase 5 (bug). | **SBI prone penalty only.** Vanilla's knockdown delay and prone carry-over are suppressed. |
| **Melee push-back** (`MeleeHitPushBackPhases`, vanilla or CBTBE) | +1 phase per point now, plus the delay effect each time | Falls through to vanilla: +1 phase per point, except a unit sitting at phase 5 isn't moved (bug). The delay effect is ignored. | `MeleePushBack.PhasesPerPoint (3)` per point. Applied now if the target hasn't acted, otherwise next round. |
| **Reserve** | +1 phase. No lingering cost. | +1 phase + hesitation roll (2..6 in the repo's `mod.json`; the 2.0.0 install used 4..12) added now. Stored, then reduced by Tactics+1 at the start of every round, including after consecutive reserves. | +1 phase + hesitation roll (2..6) added now. **Stacks on consecutive reserves** (see below). |
| **Spawn after round 1** | Placed in the last phase (`ForceUnitToLastPhase`) | Not handled. The unit keeps the vanilla `InitStats` phase (2..5 of 30), so reinforcements act almost first. | Full SBI roll + `Arrival.DelayPhases (5)` for the arrival round, never earlier than the phase in progress (so it still acts that round). Units joining in round 1 get the roll with no delay, which avoids penalising the initial deployment (e.g. CustomDeploy). Normal rolls from the next round. |
| **Load mid-round** | Valid phases 1..5 kept | Any unit in phase 6..30 is flagged invalid and reset to vanilla `BaseInitiative` (phase 2..5). Most units jump to the front for that round. | Phases 1..30 stay valid (fixed `HasValidInitiative`). |

### Implementation notes (step 3)

- **`ForceUnitOnePhaseDown` is skipped entirely** while interleaved (`__runOriginal = false`), so vanilla never creates `AbilityDefInitiativeDelay`. That effect would otherwise be counted by the vanilla delta. Callers are told apart as follows:
  - `Mech.CompleteKnockdown` sets a flag, and that call does nothing.
  - Morale attacks get the called-shot penalty.
  - Everything else is treated as melee push-back. Today that is vanilla `MeleeHitPushBackPhases` and CBTBE's copy of it.
- **Push-back carried to next round** uses a new stat, `SBI_STATE_MELEE_PUSHBACK`. It's consumed in `UpdateInitiative` like `SBI_STATE_CALLED_SHOT`. Actors from older saves get the stat on first use.
- **Knockdown on a unit that's already prone or dead** no longer adds a second prone penalty. Upstream's postfix didn't check whether vanilla actually knocked the unit down.
- **Vigilance replaces the vanilla call outright.** Upstream instead faked the unit's phase so vanilla returned early.
  - Other `ForceUnitOnePhaseUp` callers still fall through to vanilla.
  - The Vigilance floatie now shows a positive bonus. Upstream printed "+-5 INIT".
- **Vigilance re-use gate.** Vanilla blocks Vigilance while `PhaseModifierSelf != 0`, but that stat is moved to `PhaseModifier` at the end of the same activation, and Vigilance ends the activation. So the gate never matters in practice, and the fork doesn't replicate it. (Upstream never set the stat either.)
- **`PhaseModifier`**, including vanilla's prone carry-over, is still ignored by `UpdateInitiative`. Prone is handled only by the SBI prone penalty.
- **Reserve** only adds hesitation when `DeferUnit` actually succeeded (`__result`). Upstream also penalised refused reserves, such as a reserve attempted in the last phase.
- **Hesitation stacking** stores the round of the last reserve in `SBI_STATE_LAST_RESERVE_ROUND`. `UpdateInitiative` skips the decay when that equals `CurrentRound − 1`. `CurrentRound` is advanced before units' `OnNewRound` runs.

### Hesitation stacking

- Each Reserve rolls hesitation (2..6). The roll is added to the unit's current phase, and also to its **carried hesitation**.
- At the start of a round, carried hesitation is added to the new initiative.
  - If the unit **did not** reserve the previous round, carried hesitation is first reduced by Tactics+1. This is upstream's decay.
  - If the unit **did** reserve the previous round, there is no decay, so consecutive reserves keep stacking.
- Bounded by max initiative. Carried hesitation is capped at `MaxPhase − MinPhase` (29), and the final phase is clamped to 30, so a unit that keeps reserving ends up acting in the last phase.
- Upstream's decay rate (Tactics+1 per round) is kept once the unit stops reserving.

## Randomness

- Rolls are deterministic (`Helper/DeterministicRoll.cs`).
  - Each roll is a hash of `(contract GUID, actor GUID, round, roll kind)`: FNV-1a over UTF-8, then the MurmurHash3 finalizer, then mapped into the configured range.
  - `string.GetHashCode` isn't used, as it isn't guaranteed stable between runtimes.
- Roll kinds are Randomness, Inspired, Prone, Crippled, Shutdown and Hesitation. Each is hashed separately, so they don't move together.
- What this gives you:
  - Reloading a save reproduces the same rolls.
  - Rolls don't depend on the order in which anything else rolled.
  - The status tooltip shows the same prone/crippled/shutdown values combat applied. Upstream re-rolled them on every hover.
- A given unit gets one value per kind per round. For example, a knockdown's immediate prone penalty and the tooltip agree within that round.
- `System.Random` (`Mod.Random`) is removed. `Combat.NetworkRandom` isn't used either: it restarts from seed 0 each combat and isn't saved.
- Default `RandomnessMin/Max` is now −3/−9 in code and in all five unit classes in `mod.json`. Upstream was −2/−10. Hesitation stays −2/−6.
- The old range-based unit tests still pass, but each now sees a single deterministic value per unit and round. `DeterministicRollTests` covers range, spread across 1,000 units, reload reproduction, and per-round/per-kind variation.

## Config changes (`mod.json`)

New top-level keys:

```json
"VanillaModifiers": { "PhasesPerPoint": 3 },
"CalledShot":       { "BasePhases": 3 },
"Vigilance":        { "BasePhases": 3 },
"MeleePushBack":    { "PhasesPerPoint": 3 },
"Arrival":          { "DelayPhases": 5 }
```

Per-unit-class changes:
- `RandomnessMin` / `RandomnessMax` default to −3 / −9.
- `HesitationMin` / `HesitationMax` default to −2 / −6.
- `CalledShotRandMin/Max` and `VigilanceRandMin/Max` are removed. They're ignored if present.

## Bug fixes

1. `HasValidInitiative` postfix takes `ref bool __result`. Upstream's had no effect.
   - The getter may be inlined into its caller, so an `AbstractActor.Hydrate` prefix/postfix also restores a saved initiative of 1..30 if vanilla reset it on load.
2. `Combat.Constants.Phase.PhaseSpecial` / `PhaseAssault` are set to 1 / 30 per combat, in the `CombatGameState._Init` postfix (covers new combats and loads).
   - Vanilla clamps, `ForceUnit*` and `CanDeferUnit` now agree with 30 phases.
   - Upstream left `PhaseAssault` at 5, so late spawns went to phase 5.
   - This also fixes the AI: `AITeam` caps its reserve target phase at `PhaseAssault`, so upstream's AI could never reserve past phase 5.
3. Tonnage brackets: combat (`AbstractActor`) and UI (`MechDef`) use the same lookup, sorted explicitly instead of relying on `Dictionary` enumeration order.
4. `RandomnessBounds` compared against the wrong sign. With a high Piloting modifier this could make `Random.Next` throw.
5. `ShutdownInitMod` null-checks `customInfo`.
6. Status tooltip: the prone and crippled labels were swapped. The randomness text says "Piloting", not "Tactics".
7. `InitiativeToString` uses arithmetic instead of the 30-case switch. It no longer touches `__instance`, which is always null on this static method.
8. Tests: shared `Mod.Config` is reset per test, so the suite isn't order-dependent. The logger gets a real directory.

## UI

- Each screen gains lines for the vanilla modifiers, × `PhasesPerPoint`:
  - Status-panel tooltip (combat): "gear/abilities", read from the actor's `BaseInitiative` delta.
  - Mech Bay: "gear".
  - Lance screen: "gear" and "pilot abilities". These are self-targeted effects only, using vanilla's own effect-target check. Lance-wide effects come from `lanceInitiativeModifier`, so they aren't counted twice.
- `LoadoutHelper.StaticInitiative` computes the Mech Bay and lance values, so the two screens can't disagree.
- **Display convention.** Every signed modifier line reads like initiative (+ = acts earlier, green). Unsigned red numbers are delays. Badges and totals use the same number the combat HUD shows (`31 − phase`).
- **Upstream UI bugs fixed in step 2:**
  - The Mech Bay badge and its "expected phase" showed the raw phase index, so a 50 t mech read "17" there but "14" in combat.
  - The lance screen added `lanceInitiativeModifier` with the wrong sign, so lance-wide initiative gear showed as a delay. It also wasn't scaled.
  - The unit-type line was coloured and signed as a bonus when it's a delay (vehicles showed a green "+2").
  - The lance "TOTAL" line showed the raw index.

## Naming

| Item | Upstream | Fork |
|---|---|---|
| ModTek mod `Name` | `SkillBasedInit` | `BTX_SkillBasedInit` |
| Mod folder | `Mods\SkillBasedInit` | `Mods\BTX_SkillBasedInit` |
| DLL | `SkillBasedInit.dll` | `BTX_SkillBasedInit.dll` |
| Harmony ID | `us.frostraptor.SkillBasedInit` | `btx.SkillBasedInit` |
| Log file | `skill_based_init.log` | `btx_skill_based_init.log` |

- C# namespaces and the `SBI_*` stat names are unchanged, to keep the diff with upstream small.
- Content that sets `SBI_MOD_*` stats keeps working.

## Compatibility

- `ConflictsWith`: `SkillBasedInit` (RogueTech's), ExpandedInitiative, BetterInitiative, The Engine Initiative.
- **LowVisibility (user's fork):** change `OptionallyDependsOn` from `SkillBasedInit` to `BTX_SkillBasedInit`.
  - That entry only affects load order. The LowVisibility DLL has no code references to SBI (checked 2026-10-17).
- **ExpandedInitiative** also names `SkillBasedInit` in its `ConflictsWith`. That won't catch the renamed mod, so our `ConflictsWith` has to cover the pairing. Disable ExpandedInitiative when installing.
- Dependencies: IRBTModUtils, CustomComponents. CustomUnits types are used at runtime.
- To verify in-game: MissionControl, CustomDeploy and CustomUnits write `Initiative` directly (spawns and deploy). The arrival patch must not fight them.

## Implementation order

Each step builds, keeps tests green, and adds tests where logic changes.

1. **Bug fixes 1–8** (no behaviour change except the fixes themselves).
2. **Vanilla delta** in the per-round formula and the UI.
3. **Events:** called shot / vigilance hybrid, knockdown / prone suppression, push-back, arrival delay.
4. **Randomness:** deterministic rolls, new default ranges.
5. **Rename to `BTX_SkillBasedInit`** (assembly, mod.json, Harmony ID, log). **Config / mod.json defaults, README, version bump.** LowVisibility fork `mod.json` update.
6. **In-game test pass** in the BEX pack, with ExpandedInitiative disabled.
