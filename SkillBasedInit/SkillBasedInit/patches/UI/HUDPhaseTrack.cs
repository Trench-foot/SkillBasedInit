using IRBTModUtils;
using SkillBasedInit.Helper;
using System;
using System.Collections.Generic;

namespace SkillBasedInit
{

    [HarmonyPatch(typeof(CombatHUDPhaseTrack), "RefreshPhaseColors")]
    public static class CombatHUDPhaseTrack_RefreshPhaseColors
    {
        public static void Prefix(ref bool __runOriginal, CombatHUDPhaseTrack __instance, bool isPlayer, Hostility hostility, int ___currentPhase, CombatHUDPhaseBar[] ___phaseBars)
        {
            if (!__runOriginal) return;

            Mod.Log.Debug?.Write("CHUDPT::RPC - entered.");

            if (__instance == null || ___phaseBars == null || !SharedState.Combat.TurnDirector.IsInterleaved) { return; }

            // TODO: FIX HARDCODED VALUE
            // Reconcile phase (from 1 - X) with display (X to 1)
            int initNum = (Mod.MaxPhase + 1) - ___currentPhase;
            int[] phaseBounds = InitiativeHelper.CalcPhaseIconBounds(___currentPhase);
            Mod.Log.Debug?.Write($" For currentPhase: {___currentPhase}  phaseBounds are: [ {phaseBounds[0]} {phaseBounds[1]} {phaseBounds[2]} {phaseBounds[3]} {phaseBounds[4]} ]");

            for (int i = 0; i < 5; i++)
            {
                if (phaseBounds[i] > initNum)
                {
                    Mod.Log.Debug?.Write($" Setting phase: {phaseBounds[i]} as past phase.");
                    ___phaseBars[i].IndicatePastPhase();
                }
                else if (phaseBounds[i] == initNum)
                {
                    Mod.Log.Debug?.Write($" Setting phase: {phaseBounds[i]} as current phase.");
                    ___phaseBars[i].IndicateCurrentPhase(isPlayer, hostility);
                }
                else
                {
                    Mod.Log.Debug?.Write($" Setting phase: {phaseBounds[i]} as future phase.");
                    ___phaseBars[i].IndicateFuturePhase(isPlayer, hostility);
                }
                ___phaseBars[i].Text.SetText($"{phaseBounds[i]}");

            }

            if (phaseBounds[0] != Mod.MaxPhase)
            {
                ___phaseBars[0].Text.SetText("P");
            }
            if (phaseBounds[4] != Mod.MinPhase)
            {
                ___phaseBars[4].Text.SetText("F");
            }

            __runOriginal = false;
        }
    }

    // Vanilla SetTrackerPhase does PhaseIcons[phase - 1] on a 5 slot track, so any phase above 5 throws. That happens
    //   before combat is interleaved too: CustomUnits spawns assault mechs at Phase.PhaseAssault, which is 30 here.
    //   An exception in CombatHUD.Init leaves the HUD half built and CombatHUDPhaseTrack.Update throwing every frame,
    //   so never fall through to vanilla - always place the tracker ourselves.
    [HarmonyPatch(typeof(CombatHUDPhaseTrack), "SetTrackerPhase")]
    [HarmonyPatch(new Type[] { typeof(CombatHUDIconTracker), typeof(int) })]
    public static class CombatHUDPhaseTrack_SetTrackerPhase
    {
        public static void Prefix(ref bool __runOriginal, CombatHUDPhaseTrack __instance, CombatHUDIconTracker tracker, int phase, int ___currentPhase, List<CombatHUDPhaseIcons> ___PhaseIcons)
        {
            if (!__runOriginal) return;
            __runOriginal = false;

            Mod.Log.Trace?.Write($"CHUDPT:STP - entered at phase: {phase}.");

            if (tracker == null || ___PhaseIcons == null || ___PhaseIcons.Count == 0)
            {
                Mod.Log.Warn?.Write("Invalid state detected, tracker or ___PhaseIcons were null or empty. This should not happen!");
                return;
            }

            bool isInterleaved = SharedState.Combat?.TurnDirector?.IsInterleaved ?? false;
            int slot = InitiativeHelper.PhaseTrackSlot(phase, ___currentPhase, isInterleaved, ___PhaseIcons.Count);
            Mod.Log.Trace?.Write($"  -- phase: {phase} currentPhase: {___currentPhase} interleaved: {isInterleaved} => slot: {slot}");

            ___PhaseIcons[slot].AddIconTrackerToPhase(tracker);
            // Don't record the phase. Vanilla's RefreshAllIconTrackers only re-places a tracker whose phase changed, but our
            //   slots slide with the current phase, so a unit whose phase didn't change still has to move. Leaving it at -1
            //   re-places every tracker on every refresh, as upstream did.
            tracker.CurrentlyDisplayedPhase = -1;
        }
    }

    // Same unchecked turnEventIcons[phase - 1] indexing. Turn events shown on the phase track use the current phase.
    [HarmonyPatch(typeof(CombatHUDPhaseTrack), "SetTrackerTurnEventPhase")]
    [HarmonyPatch(new Type[] { typeof(CombatHUDIconTracker), typeof(int) })]
    public static class CombatHUDPhaseTrack_SetTrackerTurnEventPhase
    {
        public static void Prefix(ref bool __runOriginal, CombatHUDIconTracker tracker, int phase, int ___currentPhase, CombatHUDPhaseIcons[] ___turnEventIcons)
        {
            if (!__runOriginal) return;
            __runOriginal = false;

            if (tracker == null || ___turnEventIcons == null || ___turnEventIcons.Length == 0)
            {
                Mod.Log.Warn?.Write("Invalid state detected, tracker or ___turnEventIcons were null or empty. This should not happen!");
                return;
            }

            bool isInterleaved = SharedState.Combat?.TurnDirector?.IsInterleaved ?? false;
            int slot = InitiativeHelper.PhaseTrackSlot(phase, ___currentPhase, isInterleaved, ___turnEventIcons.Length);
            ___turnEventIcons[slot].AddTurnEventTrackerToPhase(tracker);
            tracker.CurrentlyDisplayedPhase = phase;
        }
    }

}