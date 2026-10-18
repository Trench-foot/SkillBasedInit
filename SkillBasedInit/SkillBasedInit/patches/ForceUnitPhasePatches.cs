using IRBTModUtils.Extension;
using Localize;
using SkillBasedInit.Helper;
using System;

namespace SkillBasedInit.Patches
{

    // --- FORCE UNIT PHASE DOWN EFECTS ---
    // Handle knockdowns. Vanilla calls ForceUnitOnePhaseDown from inside CompleteKnockdown; that call is suppressed
    //   (see AbstractActor_ForceUnitOnePhaseDown) so the SBI prone penalty is the only one applied.
    [HarmonyPatch(typeof(Mech), "CompleteKnockdown")]
    [HarmonyPatch(new Type[] { typeof(string), typeof(int) })]
    public static class Mech_CompleteKnockdown
    {
        public static bool InKnockdown = false;

        public static void Prefix(ref bool __runOriginal, Mech __instance, ref bool __state)
        {
            if (!__runOriginal) return;

            Mod.Log.Trace?.Write("M:CK:pre - entered.");
            InKnockdown = true;

            // Vanilla does nothing if the mech is already prone or dead; remember that so we don't add a second penalty
            __state = !__instance.IsDead && !__instance.IsProne;
        }

        public static void Postfix(Mech __instance, string sourceID, int stackItemUID, bool __state)
        {
            Mod.Log.Trace?.Write("M:CK:post - entered.");
            InKnockdown = false;

            if (!__state)
            {
                Mod.Log.Debug?.Write($"Actor: {__instance.DistinctId()} was already prone or dead, no knockdown penalty.");
                return;
            }

            Mod.Log.Debug?.Write($"Completing knockdown for actor: {__instance.DistinctId()} from sourceID:{sourceID} stackItemUID:{stackItemUID}.");

            if (InitiativeHelper.DelayMustWaitForNextRound(__instance))
            {
                // ProneInitMod is applied every round while prone, so there's nothing to store
                Mod.Log.Debug?.Write($"Actor has acted or is acting (activated: {__instance.HasActivatedThisRound} begun: {__instance.HasBegunActivation} " +
                    $"init: {__instance.Initiative}), applying prone modifier next turn.");
                return;
            }

            int penalty = __instance.ProneInitMod();
            if (penalty == 0) return; // Nothing to do

            Mod.Log.Info?.Write($"Applying penalty: {penalty} to {__instance.DistinctId()} with init: {__instance.Initiative} immediately.");

            __instance.Initiative += penalty;
            if (__instance.Initiative > Mod.MaxPhase)
            {
                __instance.Initiative = Mod.MaxPhase;
            }
            __instance.Combat.MessageCenter.PublishMessage(new ActorPhaseInfoChanged(__instance.GUID));

            string floatieMsg = new Text(Mod.LocalizedText.Floaties[ModText.LT_FT_KNOCKDOWN], new object[] { penalty }).ToString();
            __instance.Combat.MessageCenter.PublishMessage(
                    new FloatieMessage(__instance.GUID, __instance.GUID, floatieMsg, FloatieMessage.MessageNature.Debuff)
                    );
        }
    }

    // Handle morale attacks (aka called shots)
    [HarmonyPatch(typeof(AttackStackSequence), "OnAdded")]
    [HarmonyPatch(new Type[] { })]
    public static class AttackStackSequence_OnAdded
    {
        public static bool IsMoraleAttack = false;
        public static AbstractActor Attacker = null;

        public static void Prefix(ref bool __runOriginal, AttackStackSequence __instance)
        {
            if (!__runOriginal) return;

            Mod.Log.Trace?.Write("ASS:OA:pre - entered.");
            IsMoraleAttack = __instance.isMoraleAttack;
            Attacker = __instance.owningActor;
        }

        public static void Postfix(AttackStackSequence __instance)
        {
            Mod.Log.Trace?.Write("ASS:OA:post - entered.");
            IsMoraleAttack = false;
            Attacker = null;
            Mod.Log.Debug?.Write($"AttackStackSequence:OnAdded:prefix - Resetting IsMoraleAttack");
        }
    }

    // Vanilla ForceUnitOnePhaseDown moves the unit one phase and creates InitiativeDelayEffect (BaseInitiative +1),
    //   which the vanilla delta would also count. Replace it entirely while interleaved. Callers are:
    //   - morale attacks (called shot): hybrid called shot penalty
    //   - Mech.CompleteKnockdown: nothing, Mech_CompleteKnockdown applies the SBI prone penalty
    //   - melee push-back (MeleeHitPushBackPhases, vanilla or CBTBE): MeleePushBack.PhasesPerPoint per call
    [HarmonyPatch(typeof(AbstractActor), "ForceUnitOnePhaseDown")]
    [HarmonyPatch(new Type[] { typeof(string), typeof(int), typeof(bool) })]
    public static class AbstractActor_ForceUnitOnePhaseDown
    {
        public static void Prefix(ref bool __runOriginal, AbstractActor __instance, string sourceID, int stackItemUID, bool addedBySelf)
        {
            if (!__runOriginal) return;

            Mod.Log.Trace?.Write("AA:FUOPD:pre - entered.");

            // Vanilla does nothing outside of interleaved mode either
            if (!__instance.Combat.TurnDirector.IsInterleaved) return;
            __runOriginal = false;

            if (Mech_CompleteKnockdown.InKnockdown)
            {
                Mod.Log.Debug?.Write($"Suppressing vanilla knockdown delay for actor: {__instance.DistinctId()}");
                return;
            }

            if (AttackStackSequence_OnAdded.IsMoraleAttack)
            {
                int penalty = __instance.CalledShotInitMod(AttackStackSequence_OnAdded.Attacker);
                ApplyDelay(__instance, penalty, ModStats.STATE_CALLED_SHOT, ModText.LT_FT_CALLED_SHOT_NOW, ModText.LT_FT_CALLED_SHOT_LATER);
            }
            else
            {
                int penalty = Mod.Config.MeleePushBack.PhasesPerPoint;
                ApplyDelay(__instance, penalty, ModStats.STATE_MELEE_PUSHBACK, ModText.LT_FT_PUSHBACK_NOW, ModText.LT_FT_PUSHBACK_LATER);
            }
        }

        // Delays the unit now if it hasn't acted yet, otherwise stores the penalty for next round's UpdateInitiative
        public static void ApplyDelay(AbstractActor actor, int penalty, string stateStat, string floatieNowKey, string floatieLaterKey)
        {
            if (penalty <= 0) return; // nothing to do

            bool applyNextTurn = InitiativeHelper.DelayMustWaitForNextRound(actor);
            if (applyNextTurn)
            {
                Mod.Log.Info?.Write($"Applying penalty: {penalty} to {actor.DistinctId()} with init: {actor.Initiative} next turn via {stateStat}.");
                // Actors loaded from saves made before a stat existed won't have it
                if (!actor.StatCollection.ContainsStatistic(stateStat)) actor.StatCollection.AddStatistic<int>(stateStat, 0);
                actor.StatCollection.ModifyStat<int>("self", -1, stateStat, StatCollection.StatOperation.Int_Add, penalty);
            }
            else
            {
                Mod.Log.Info?.Write($"Applying penalty: {penalty} to {actor.DistinctId()} with init: {actor.Initiative} immediately.");
                actor.Initiative = Math.Min(actor.Initiative + penalty, Mod.MaxPhase);
                actor.Combat.MessageCenter.PublishMessage(new ActorPhaseInfoChanged(actor.GUID));
            }

            string floatieKey = applyNextTurn ? floatieLaterKey : floatieNowKey;
            string floatieMsg = new Text(Mod.LocalizedText.Floaties[floatieKey], new object[] { penalty }).ToString();
            actor.Combat.MessageCenter.PublishMessage(
                new FloatieMessage(actor.GUID, actor.GUID, floatieMsg, FloatieMessage.MessageNature.Debuff)
                );
        }
    }

    // Any melee hit that does damage staggers the target (MeleeHit). Vanilla and CBTBE only delay a melee target through
    //   the attacker's MeleeHitPushBackPhases, which they route through ForceUnitOnePhaseDown above, so gear stacks on top.
    //   Harmony runs postfixes even when a prefix skips the original (CBTBE does with PermanentEvasion).
    [HarmonyPatch(typeof(AbstractActor), "ResolveAttackSequence")]
    [HarmonyPatch(new Type[] { typeof(string), typeof(int), typeof(int), typeof(AttackDirection) })]
    public static class AbstractActor_ResolveAttackSequence
    {
        public static void Postfix(AbstractActor __instance, int sequenceID)
        {
            Mod.Log.Trace?.Write("AA:RAS:post - entered.");

            if (__instance?.Combat == null || !__instance.Combat.TurnDirector.IsInterleaved) return;
            if (__instance.IsDead || __instance.IsFlaggedForDeath) return;

            AttackDirector.AttackSequence attackSequence = __instance.Combat.AttackDirector.GetAttackSequence(sequenceID);
            if (attackSequence == null || !attackSequence.isMelee || attackSequence.attacker == null) return;
            // DFA self damage and CBTBE's self knockdown sequences aren't hits on someone else
            if (attackSequence.attacker.GUID == __instance.GUID) return;
            if (!attackSequence.GetAttackDidDamage(__instance.GUID)) return;

            int penalty = __instance.MeleeHitInitMod(attackSequence.attacker);
            Mod.Log.Debug?.Write($"Melee hit on: {__instance.DistinctId()} by: {attackSequence.attacker.DistinctId()} => penalty: {penalty}");
            AbstractActor_ForceUnitOnePhaseDown.ApplyDelay(__instance, penalty, ModStats.STATE_MELEE_PUSHBACK, ModText.LT_FT_MELEE_NOW, ModText.LT_FT_MELEE_LATER);
        }
    }

    // --- FORCE UNIT PHASE UP EFFECTS ---
    // Record the morale defend (Vigilance) source so ForceUnitOnePhaseUp can tell it apart from other callers
    [HarmonyPatch(typeof(Mech), "ApplyMoraleDefendEffects")]
    [HarmonyPatch(new Type[] { typeof(string), typeof(int) })]
    public static class Mech_ApplyMoraleDefendEffects
    {

        public static string MoraleDefendSourceId;
        public static int MoraleDefendStackItemId;

        public static void Prefix(ref bool __runOriginal, Mech __instance, string sourceID, int stackItemID)
        {
            if (!__runOriginal) return;

            Mod.Log.Trace?.Write("M:AMDE:post - entered.");

            MoraleDefendSourceId = sourceID;
            MoraleDefendStackItemId = stackItemID;
            Mod.Log.Debug?.Write($"Mech:ApplyMoraleDefendEffects:prefix - Recording sourceID:{MoraleDefendSourceId} stackItemUID:{MoraleDefendStackItemId}");
        }

        public static void Postfix(Mech __instance)
        {
            Mod.Log.Trace?.Write("M:AMDE:post - entered.");
            Mod.Log.Debug?.Write($"Mech:ApplyMoraleDefendEffects:postfix - Removing sourceID:{MoraleDefendSourceId} stackItemUID:{MoraleDefendStackItemId}.");
            MoraleDefendSourceId = null;
            MoraleDefendStackItemId = -1;
        }
    }

    // Replace vanilla's Vigilance phase change (-1 now, PhaseModifierSelf -1 next round) with the hybrid bonus,
    //   stored for next round. Braced and stability effects in ApplyMoraleDefendEffects are untouched.
    //   Other callers fall through to vanilla.
    [HarmonyPatch(typeof(AbstractActor), "ForceUnitOnePhaseUp")]
    [HarmonyPatch(new Type[] { typeof(string), typeof(int), typeof(bool) })]
    public static class AbstractActor_ForceUnitOnePhaseUp
    {
        static void Prefix(ref bool __runOriginal, AbstractActor __instance, string sourceID, int stackItemUID)
        {
            if (!__runOriginal) return;

            Mod.Log.Trace?.Write("AA:FUOPU:pre - entered.");
            Mod.Log.Info?.Write($"AbstractActor:ForceUnitOnePhaseUp:prefix - sourceID:{sourceID} vs actor: {__instance.GUID}");

            bool isVigilance = sourceID == Mech_ApplyMoraleDefendEffects.MoraleDefendSourceId && stackItemUID == Mech_ApplyMoraleDefendEffects.MoraleDefendStackItemId;
            if (!isVigilance) return;

            __runOriginal = false;

            int bonus = __instance.VigilanceInitMod();
            if (bonus == 0) return;

            __instance.StatCollection.ModifyStat<int>("self", -1, ModStats.STATE_VIGILIANCE, StatCollection.StatOperation.Int_Add, bonus);

            // Bonus is negative (earlier); the floatie reads '+{0} INIT'
            string floatieMsg = new Text(Mod.LocalizedText.Floaties[ModText.LT_FT_VIGILANCE], new object[] { bonus * -1 }).ToString();
            __instance.Combat.MessageCenter.PublishMessage(new FloatieMessage(__instance.GUID, __instance.GUID, floatieMsg, FloatieMessage.MessageNature.Buff));
        }
    }

}
