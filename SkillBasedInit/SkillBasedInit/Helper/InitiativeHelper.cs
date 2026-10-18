using IRBTModUtils.Extension;
using System.Collections.Generic;

namespace SkillBasedInit.Helper
{
    public static class InitiativeHelper
    {

        // Init and phases are flipped. ForceUnitOnePhaseDown is initiative++, ForceUnitOnePhaseUp is initiative--.
        // Init of 1 = phase 30, 6 = 24, 10 = 21, 30 = 1
        public static int PhaseToInitiative(int phase)
        {
            int initiative = (Mod.MaxPhase + 1) - phase;
            return initiative;
        }

        // Init and phases are flipped. ForceUnitOnePhaseDown is initiative++, ForceUnitOnePhaseUp is initiative--.
        // Init of 1 = phase 30, 6 = 24, 10 = 21, 30 = 1
        public static int InitiativeToPhase(int initiative)
        {
            int phase = (Mod.MaxPhase + 1) - initiative;
            return phase;
        }

        public static bool IsValidInitiative(int initiative)
        {
            return initiative >= Mod.MinPhase && initiative <= Mod.MaxPhase;
        }

        // Display value for an actor.Initiative, clamped to the valid range. Init of 1 displays as 30, 30 displays as 1.
        public static string InitiativeToString(int initiative)
        {
            if (!IsValidInitiative(initiative))
            {
                Mod.Log.Info?.Write($"InitiativeToString - ERROR - Bad initiative of {initiative} detected, clamping for display.");
                initiative = initiative > Mod.MaxPhase ? Mod.MaxPhase : Mod.MinPhase;
            }
            return InitiativeToPhase(initiative).ToString();
        }

        // Vanilla BaseInitiative points the actor currently carries from gear, abilities, quirks and injuries:
        //   current value minus the default set in InitStats. Positive means later, as content uses Int_Add -1 for '+1 initiative'.
        public static int VanillaModifierPoints(AbstractActor actor)
        {
            Statistic stat = actor?.StatCollection?.GetStatistic(ModStats.HBS_BASE_INITIATIVE);
            if (stat == null) return 0;

            return stat.Value<int>() - stat.DefaultValue<int>();
        }

        public static int VanillaPointsToPhases(int points)
        {
            return points * Mod.Config.VanillaModifiers.PhasesPerPoint;
        }

        // Out of combat there are no stats yet, so mirror vanilla LanceLoadoutSlot: count self-targeted BaseInitiative
        //   effects. Lance-wide effects are excluded here, they arrive via LanceConfiguratorPanel.lanceInitiativeModifier.
        public static int ComponentModifierPoints(MechDef mechDef)
        {
            if (mechDef?.Inventory == null) return 0;

            List<EffectData> effects = new List<EffectData>();
            foreach (MechComponentRef componentRef in mechDef.Inventory)
            {
                if (componentRef?.Def?.statusEffects != null) effects.AddRange(componentRef.Def.statusEffects);
            }
            return SelfEffectPoints(effects);
        }

        public static int PilotAbilityModifierPoints(Pilot pilot)
        {
            if (pilot?.pilotDef?.AbilityDefs == null) return 0;

            List<EffectData> effects = new List<EffectData>();
            foreach (AbilityDef abilityDef in pilot.pilotDef.AbilityDefs)
            {
                if (abilityDef?.EffectData != null) effects.AddRange(abilityDef.EffectData);
            }
            return SelfEffectPoints(effects);
        }

        // GetInitiativeModifierFromEffectData returns the display value (-modValue); invert back to points (+ = later)
        public static int SelfEffectPoints(IEnumerable<EffectData> effects)
        {
            int points = 0;
            foreach (EffectData effect in effects)
            {
                bool isLanceWide = MechStatisticsRules.GetInitiativeModifierFromEffectData(effect, true) != 0;
                if (isLanceWide) continue;

                points -= MechStatisticsRules.GetInitiativeModifierFromEffectData(effect, false);
            }
            return points;
        }

        // Finds the tonnage bracket for a unit: the smallest key >= tonnage. Tonnage above every key uses the largest key.
        //   Keys are sorted explicitly, as Dictionary enumeration order isn't guaranteed.
        public static int TonnageToPhase(Dictionary<int, int> initBaseByTonnage, int tonnage)
        {
            if (initBaseByTonnage == null || initBaseByTonnage.Count == 0) return 0;

            List<int> keys = new List<int>(initBaseByTonnage.Keys);
            keys.Sort();
            foreach (int key in keys)
            {
                if (tonnage <= key) return initBaseByTonnage[key];
            }
            return initBaseByTonnage[keys[keys.Count - 1]];
        }

        public static void UpdateInitiative(AbstractActor actor)
        {

            Mod.Log.Info?.Write($"Updating initiative for actor: {actor.DistinctId()}");

            if (!actor.Combat.TurnDirector.IsInterleaved)
            {
                actor.Initiative = actor.Combat.TurnDirector.NonInterleavedPhase;
                Mod.Log.Info?.Write($"  combat is not interleaved, using phase: {actor.Initiative})");
                return;
            }

            // If the actor is dead, skip them
            if (actor.IsDead || actor.IsFlaggedForDeath)
            {
                actor.Initiative = Mod.MaxPhase;
                Mod.Log.Info?.Write($"  actor is dead, setting init to MaxPhase: {Mod.MaxPhase}");
                actor.StatCollection.Set<int>(ModStats.ROUND_INIT, Mod.MaxPhase);
                return;
            }

            UnitCfg unitConfig = actor.GetUnitConfig();

            // Set the base init by tonnage
            int tonnageMod = actor.StatCollection.GetValue<int>(ModStats.STATE_TONNAGE_INIT_MOD);
            int roundInitiative = tonnageMod;

            int typeMod = actor.StatCollection.GetValue<int>(ModStats.STATE_UNIT_TYPE_INIT_MOD);
            roundInitiative += typeMod;

            int pilotTagsMod = actor.StatCollection.GetValue<int>(ModStats.STATE_PILOT_TAGS_INIT_MOD);
            roundInitiative += pilotTagsMod;
            Mod.Log.Info?.Write(
                $"roundInit: {roundInitiative} <=" +
                $"  tonnageBase: {tonnageMod}" +
                $"  unitType: {typeMod}" +
                $"  pilotTags: {pilotTagsMod}"
                );


            // Check non-consumable modifiers - they apply without change

            // Injury mod is a phase mod, so invert it
            int injuryMod = actor.StatCollection.GetValue<int>(ModStats.MOD_INJURY) * -1;
            roundInitiative += injuryMod;

            // Misc mod is a phase mod, so invert it.
            int miscMod = actor.StatCollection.GetValue<int>(ModStats.MOD_MISC) * -1;
            roundInitiative += miscMod;
            Mod.Log.Info?.Write(
                $"roundInit: {roundInitiative} <=" +
                $"  injuryMod: {injuryMod}" +
                $"  miscMod: {miscMod}"
                );

            // Vanilla and modpack BaseInitiative modifiers (gear, abilities, quirks, injuries), scaled to our phases
            int vanillaPoints = VanillaModifierPoints(actor);
            int vanillaMod = VanillaPointsToPhases(vanillaPoints);
            roundInitiative += vanillaMod;
            Mod.Log.Info?.Write($"roundInit: {roundInitiative} <=  vanillaMod: {vanillaMod} from vanillaPoints: {vanillaPoints}");

            // Check for consumable modifiers - these get reset to 0 when we recalculate
            int calledShotMod = actor.StatCollection.GetValue<int>(ModStats.STATE_CALLED_SHOT);
            if (calledShotMod != 0)
            {
                // Actor was hit by a called shot sometime after its turn, apply the penalty
                roundInitiative += calledShotMod;
                Mod.Log.Info?.Write($"roundInit: {roundInitiative} <=  calledShotState: {calledShotMod}");
                actor.StatCollection.Set<int>(ModStats.STATE_CALLED_SHOT, 0);
            }

            // Actor was pushed back by melee sometime after its turn. Saves from before this stat existed won't have it.
            int pushBackMod = actor.StatCollection.ContainsStatistic(ModStats.STATE_MELEE_PUSHBACK) ?
                actor.StatCollection.GetValue<int>(ModStats.STATE_MELEE_PUSHBACK) : 0;
            if (pushBackMod != 0)
            {
                roundInitiative += pushBackMod;
                Mod.Log.Info?.Write($"roundInit: {roundInitiative} <=  meleePushBackState: {pushBackMod}");
                actor.StatCollection.Set<int>(ModStats.STATE_MELEE_PUSHBACK, 0);
            }

            int vigilanceMod = actor.StatCollection.GetValue<int>(ModStats.STATE_VIGILIANCE);
            if (vigilanceMod != 0)
            {
                roundInitiative += vigilanceMod;
                Mod.Log.Info?.Write($"roundInit: {roundInitiative} <=  vigilanceState: {vigilanceMod}");
                actor.StatCollection.Set<int>(ModStats.STATE_VIGILIANCE, 0);
            }

            int proneMod = actor.ProneInitMod();
            roundInitiative += proneMod;
            int crippledMod = actor.CrippledInitModifier();
            roundInitiative += crippledMod;
            int shutdownMod = actor.ShutdownInitMod();
            roundInitiative += shutdownMod;
            Mod.Log.Info?.Write($"roundInit: {roundInitiative} <=  proneInitModifier: {proneMod}  " +
                $"crippledInitModifier: {crippledMod}  " +
                $"shutdownInitModifier: {shutdownMod}");

            Pilot pilot = actor.GetPilot();

            // Tactics modifier are positive, so reduce init
            int tacticsMod = pilot.SBITacticsMod() * -1;
            roundInitiative += tacticsMod;

            // Inspired mods are positive phases, so reduce init 
            int inspiredMod = pilot.InspiredModifier();
            roundInitiative += inspiredMod;

            // Generate the random element
            int randomnessMod = pilot.RandomnessModifier(unitConfig);
            roundInitiative += randomnessMod;

            Mod.Log.Info?.Write($"roundInit: {roundInitiative} <=  tacticsMod: {tacticsMod}  inspiredMod: {inspiredMod} randomnessMod: {randomnessMod}");

            if (actor.StatCollection.GetValue<int>(ModStats.STATE_HESITATION) != 0)
            {
                int carriedHesitation = actor.StatCollection.GetValue<int>(ModStats.STATE_HESITATION);
                if (ReservedLastRound(actor))
                {
                    // Consecutive reserves stack: no decay while the unit keeps reserving
                    Mod.Log.Info?.Write($"  hesitationMod: {carriedHesitation} carried without decay, unit reserved last round");
                }
                else
                {
                    // Tactics mod is raw, so reduce hestitation init by raw statics modifier
                    int hestiationReduction = pilot.SBITacticsMod() + 1;
                    int reducedHesitation = carriedHesitation - hestiationReduction;
                    Mod.Log.Info?.Write($"  hesitationMod: {carriedHesitation} - tacticsMod: {hestiationReduction} => {reducedHesitation}");
                    carriedHesitation = reducedHesitation < 0 ? 0 : reducedHesitation;
                }
                roundInitiative += carriedHesitation;
                actor.StatCollection.Set<int>(ModStats.STATE_HESITATION, carriedHesitation);
            }

            // Normalize values
            if (roundInitiative <= 0)
            {
                roundInitiative = Mod.MinPhase;
                Mod.Log.Info?.Write($"  Round init {roundInitiative} less than 0, setting to 1.");
            }
            else if (roundInitiative > 30)
            {
                roundInitiative = Mod.MaxPhase;
                Mod.Log.Info?.Write($"  Round init {roundInitiative} greater than 30, setting to 30.");
            }

            actor.Initiative = roundInitiative;
            int newPhase = InitiativeHelper.InitiativeToPhase(roundInitiative);
            Mod.Log.Info?.Write($"  actor.Initiative: {actor.Initiative} => phase: {newPhase}");
        }

        // Carried hesitation is capped so it's bounded by the phase range; the final phase is clamped separately
        public const int MaxHesitation = Mod.MaxPhase - Mod.MinPhase;

        // Adds a reserve's hesitation to the carried total and records the round, so the next round knows not to decay it
        public static void AddHesitation(AbstractActor actor, int penalty, int currentRound)
        {
            StatCollection stats = actor.StatCollection;
            // Actors loaded from saves made before these stats existed won't have them
            if (!stats.ContainsStatistic(ModStats.STATE_LAST_RESERVE_ROUND)) stats.AddStatistic<int>(ModStats.STATE_LAST_RESERVE_ROUND, 0);

            int carried = stats.GetValue<int>(ModStats.STATE_HESITATION) + penalty;
            if (carried > MaxHesitation) carried = MaxHesitation;

            stats.Set<int>(ModStats.STATE_HESITATION, carried);
            stats.Set<int>(ModStats.STATE_LAST_RESERVE_ROUND, currentRound);
        }

        // OnNewRound runs after TurnDirector.CurrentRound is advanced, so 'last round' is CurrentRound - 1
        public static bool ReservedLastRound(AbstractActor actor)
        {
            if (!actor.StatCollection.ContainsStatistic(ModStats.STATE_LAST_RESERVE_ROUND)) return false;

            int lastReserveRound = actor.StatCollection.GetValue<int>(ModStats.STATE_LAST_RESERVE_ROUND);
            int currentRound = actor.Combat.TurnDirector.CurrentRound;
            return lastReserveRound > 0 && lastReserveRound == currentRound - 1;
        }

        // Initiative for a unit that joins combat mid-mission: a full roll, plus the arrival delay when requested.
        //   Never earlier than the phase in progress, so the unit still acts in the round it arrives.
        public static int ArrivalInitiative(AbstractActor actor, bool applyDelay)
        {
            UpdateInitiative(actor);

            int delay = applyDelay ? Mod.Config.Arrival.DelayPhases : 0;
            int arrivalInit = actor.Initiative + delay;

            int currentPhase = actor.Combat.TurnDirector.CurrentPhase;
            if (arrivalInit < currentPhase) arrivalInit = currentPhase;
            if (arrivalInit > Mod.MaxPhase) arrivalInit = Mod.MaxPhase;

            Mod.Log.Info?.Write($"Arrival for actor: {actor.DistinctId()} => rolled: {actor.Initiative} + delay: {delay}, currentPhase: {currentPhase} => {arrivalInit}");
            actor.Initiative = arrivalInit;
            return delay;
        }

        // Calculate the left and right phase boundaries *as initiative*
        //   Will calculate 
        // 30, 29, 28, 27, 26 (red 30) -> phase 1
        // 30, 29, 28, 27, 26 (red 29) -> phase 2
        // 30, 29, 28, 27, 26 (red 28) -> phase 3
        // 29, 28, 27, 26, 25 (red 27) -> phase 4
        // 28, 27, 26, 25, 24 (red 26) -> phase 5
        // ...
        //  7,  6,  5,  4,  3 (red 5) -> phase 26
        //  6,  5,  4,  3,  2 (red 4) -> phase 27
        //  5,  4,  3,  2,  1 (red 3) -> phase 28
        //  5,  4,  3,  2,  1 (red 2) -> phase 29
        //  5,  4,  3,  2,  1 (red 1) -> phase 30
        public static int[] CalcPhaseIconBounds(int currentPhase)
        {

            // Normalize phase to initiative values
            int currentInit = (Mod.MaxPhase + 1) - currentPhase;

            int midPoint = currentInit;
            if (midPoint + 2 > Mod.MaxPhase || midPoint + 1 > Mod.MaxPhase)
            {
                midPoint = Mod.MaxPhase - 2;
            }
            else if (midPoint - 2 < Mod.MinPhase || midPoint - 1 < Mod.MinPhase)
            {
                midPoint = Mod.MinPhase + 2;
            }

            int[] bounds = new int[] { midPoint + 2, midPoint + 1, midPoint, midPoint - 1, midPoint - 2 };
            //Mod.Log.Info?.Write($"For phase {currentPhase}, init bounds are: {bounds[0]} to {bounds[4]}");
            return bounds;
        }
    }
}
