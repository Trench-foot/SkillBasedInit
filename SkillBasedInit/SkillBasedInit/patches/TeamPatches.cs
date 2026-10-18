using IRBTModUtils.Extension;
using Localize;
using SkillBasedInit.Helper;

namespace SkillBasedInit.patches
{
    // Units that join after combat starts (reinforcements, MissionControl extra lances, ambushes) keep the vanilla
    //   InitStats phase (2-5 of 30) until the next round, so they would act almost first. Roll them in properly instead.
    //   Loading a save doesn't go through AddUnit, so restored units keep their saved initiative.
    [HarmonyPatch(typeof(Team), "AddUnit")]
    public static class Team_AddUnit
    {
        public static void Prefix(ref bool __runOriginal, Team __instance, AbstractActor unit, ref bool __state)
        {
            if (!__runOriginal) return;

            // AddUnit returns early for units already on the team
            __state = unit != null && (__instance.units == null || !__instance.units.Contains(unit));
        }

        public static void Postfix(Team __instance, AbstractActor unit, bool __state)
        {
            if (!__state) return;

            CombatGameState combat = __instance.Combat;
            TurnDirector turnDirector = combat?.TurnDirector;
            if (turnDirector == null || turnDirector.CurrentRound < 1 || !turnDirector.IsInterleaved) return;

            // Only units SBI has initialized stats for
            if (!unit.StatCollection.ContainsStatistic(ModStats.STATE_TONNAGE_INIT_MOD)) return;

            // Round 1 spawns may be the initial deployment (e.g. CustomDeploy), so they only get a normal roll
            bool applyDelay = turnDirector.CurrentRound > 1;
            int delay = InitiativeHelper.ArrivalInitiative(unit, applyDelay);

            combat.MessageCenter.PublishMessage(new ActorPhaseInfoChanged(unit.GUID));
            if (delay > 0)
            {
                string floatieMsg = new Text(Mod.LocalizedText.Floaties[ModText.LT_FT_ARRIVAL], new object[] { delay }).ToString();
                combat.MessageCenter.PublishMessage(new FloatieMessage(unit.GUID, unit.GUID, floatieMsg, FloatieMessage.MessageNature.Debuff));
            }
            Mod.Log.Info?.Write($"Unit: {unit.DistinctId()} joined in round: {turnDirector.CurrentRound} phase: {turnDirector.CurrentPhase}, initiative set to: {unit.Initiative}");
        }
    }
}
