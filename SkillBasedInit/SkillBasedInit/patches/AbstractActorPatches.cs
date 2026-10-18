using BattleTech.Save.Test;
using IRBTModUtils.Extension;
using SkillBasedInit.Helper;
using System;

namespace SkillBasedInit
{

    [HarmonyPatch(typeof(AbstractActor), "OnNewRound")]
    public static class AbstractActor_OnNewRound
    {
        public static void Postfix(AbstractActor __instance, int round)
        {
            Mod.Log.Trace?.Write("AA:ONR - entered.");
            InitiativeHelper.UpdateInitiative(__instance);
        }
    }

    [HarmonyPatch(typeof(AbstractActor), "DeferUnit")]
    public static class AbstractActor_DeferUnit
    {
        public static void Postfix(AbstractActor __instance)
        {
            Mod.Log.Trace?.Write($"AA:DU - entered.");

            int penalty = __instance.GetHesitationPenalty();
            int newInit = __instance.Initiative + penalty;
            if (newInit > Mod.MaxPhase) newInit = Mod.MaxPhase;

            Mod.Log.Debug?.Write($"  Deferring Actor:({__instance.DistinctId()}) initiative:{__instance.Initiative} by:{penalty} to:{newInit}");
            __instance.Initiative = newInit;

            // Save the penalty to the actor for next round
            try
            {
                __instance.StatCollection.ModifyStat<int>(__instance.GUID, -1, ModStats.STATE_HESITATION, StatCollection.StatOperation.Int_Add, penalty);
                Mod.Log.Info?.Write($"Hesitation increased to {__instance.StatCollection.GetValue<int>(ModStats.STATE_HESITATION)} for actor: {__instance.DistinctId()}");
            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, $"Failed to update init for actor: {__instance.DistinctId()} to {newInit}");
            }

        }

    }

    // InitiativeToString is static, so there's no instance to correct here; the display value is clamped instead.
    [HarmonyPatch(typeof(AbstractActor), "InitiativeToString")]
    public static class AbstractActor_InitiativeToString
    {
        public static void Postfix(ref string __result, int initiative)
        {
            Mod.Log.Trace?.Write($"AA:ITS - entered for init:{initiative}.");
            __result = InitiativeHelper.InitiativeToString(initiative);
            Mod.Log.Trace?.Write($" returning {__result} for initiative {initiative}");
        }
    }

    // The getter is small enough that callers may inline it, so AbstractActor_Hydrate also guards the load path.
    [HarmonyPatch(typeof(AbstractActor))]
    [HarmonyPatch("HasValidInitiative", MethodType.Getter)]
    public static class AbstractActor_HasValidInitiative
    {
        public static void Postfix(AbstractActor __instance, ref bool __result)
        {
            Mod.Log.Debug?.Write("AA:HVI - entered.");
            bool isValid = InitiativeHelper.IsValidInitiative(__instance.Initiative);
            if (!isValid)
            {
                Mod.Log.Info?.Write($"Actor:{__instance.DistinctId()} has invalid initiative {__instance.Initiative}!");
            }
            __result = isValid;
            Mod.Log.Debug?.Write($"AbstractActor:HasValidInitiative returning {__result} for {__instance.Initiative}");
        }
    }

    // Vanilla Hydrate resets any initiative outside 1-5 to BaseInitiative on load. Restore the saved value if it's valid for 30 phases.
    [HarmonyPatch(typeof(AbstractActor), "Hydrate")]
    [HarmonyPatch(new Type[] { typeof(SerializableReferenceContainer), typeof(CombatGameState) })]
    public static class AbstractActor_Hydrate
    {
        public static void Prefix(AbstractActor __instance, ref int __state)
        {
            __state = __instance.Initiative;
        }

        public static void Postfix(AbstractActor __instance, int __state)
        {
            if (__instance.Initiative != __state && InitiativeHelper.IsValidInitiative(__state))
            {
                Mod.Log.Info?.Write($"Restoring saved initiative: {__state} for actor: {__instance.DistinctId()}, vanilla reset it to: {__instance.Initiative}");
                __instance.Initiative = __state;
            }
        }
    }

    [HarmonyPatch(typeof(AbstractActor))]
    [HarmonyPatch("BaseInitiative", MethodType.Getter)]
    public static class AbstractActor_BaseInitiative
    {
        public static void Postfix(AbstractActor __instance, ref int __result, StatCollection ___statCollection)
        {
            Mod.Log.Trace?.Write("AA:BI - entered.");

            if (__instance.Combat.TurnDirector.IsInterleaved)
            {
                int baseInit = ___statCollection.GetValue<int>("BaseInitiative");
                int phaseMod = ___statCollection.GetValue<int>("PhaseModifier");
                int modifiedInit = baseInit + phaseMod;

                if (modifiedInit < Mod.MinPhase)
                {
                    Mod.Log.Info?.Write($"Actor:({__instance.DistinctId()}) being set to {Mod.MinPhase} due to BaseInit:{baseInit} + PhaseMod:{phaseMod}");
                    __result = Mod.MinPhase;
                }
                else if (modifiedInit > Mod.MaxPhase)
                {
                    Mod.Log.Info?.Write($"Actor:({__instance.DistinctId()}) being set to {Mod.MaxPhase} due to BaseInit:{baseInit} + PhaseMod:{phaseMod}");
                    __result = Mod.MaxPhase;
                }
                else
                {
                    __result = modifiedInit;
                    Mod.Log.Info?.Write($"Actor:({__instance.DistinctId()}) has stats BaseInit:{baseInit} + PhaseMod:{phaseMod} = modifiedInit:{modifiedInit}.");
                }
            }
            else
            {
                Mod.Log.Debug?.Write($"Actor:({__instance.DistinctId()}) is non-interleaved, returning phase: {Mod.MaxPhase}.");
                __result = Mod.MaxPhase;
            }

        }
    }

    // Initializes any custom status effects. Without this, they won't be read.
    [HarmonyPatch(typeof(AbstractActor), "InitEffectStats")]
    static class AbstractActor_InitEffectStats
    {
        static void Postfix(AbstractActor __instance)
        {
            Mod.Log.Info?.Write($"Initializing statistics for actor: {__instance.DistinctId()}");

            // Static init values that default to a specific value
            int tonnageMod = __instance.GetBaseInitByTonnage();
            Mod.Log.Info?.Write($"  -- tonnageMod: {tonnageMod}");
            __instance.StatCollection.AddStatistic<int>(ModStats.STATE_TONNAGE_INIT_MOD, tonnageMod);

            int typeMod = __instance.GetTypeModifier();
            Mod.Log.Info?.Write($"  -- typeMod: {typeMod}");
            __instance.StatCollection.AddStatistic<int>(ModStats.STATE_UNIT_TYPE_INIT_MOD, typeMod);

            // Modifiers to various effects
            __instance.StatCollection.AddStatistic<int>(ModStats.MOD_INJURY, 0);
            __instance.StatCollection.AddStatistic<int>(ModStats.MOD_MISC, 0);
            __instance.StatCollection.AddStatistic<int>(ModStats.MOD_CALLED_SHOT_ATTACKER, 0);
            __instance.StatCollection.AddStatistic<int>(ModStats.MOD_CALLED_SHOT_TARGET, 0);
            __instance.StatCollection.AddStatistic<int>(ModStats.MOD_VIGILANCE, 0);
            __instance.StatCollection.AddStatistic<int>(ModStats.MOD_HESITATION, 0);

            // Stats that track future state
            __instance.StatCollection.AddStatistic<int>(ModStats.STATE_CALLED_SHOT, 0);
            __instance.StatCollection.AddStatistic<int>(ModStats.STATE_VIGILIANCE, 0);
            __instance.StatCollection.AddStatistic<int>(ModStats.STATE_HESITATION, 0);

            // Pilot related values
            Pilot pilot = __instance.GetPilot();
            if (pilot != null)
            {
                int pilotTagsMod = PilotHelper.GetTagsModifier(pilot);
                Mod.Log.Info?.Write($"  -- pilotTagsMod: {pilotTagsMod}");
                __instance.StatCollection.AddStatistic<int>(ModStats.STATE_PILOT_TAGS_INIT_MOD, pilotTagsMod);

                pilot.StatCollection.AddStatistic<int>(ModStats.MOD_SKILL_GUNNERY, 0);
                pilot.StatCollection.AddStatistic<int>(ModStats.MOD_SKILL_GUTS, 0);
                pilot.StatCollection.AddStatistic<int>(ModStats.MOD_SKILL_PILOT, 0);
                pilot.StatCollection.AddStatistic<int>(ModStats.MOD_SKILL_TACTICS, 0);
            }
        }
    }
}
