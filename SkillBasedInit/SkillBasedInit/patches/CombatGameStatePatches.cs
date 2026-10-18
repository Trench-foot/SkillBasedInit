using BattleTech.Data;
using SVGImporter;

namespace SkillBasedInit.patches
{

    [HarmonyPatch(typeof(CombatGameState), "_Init")]
    public static class CombatGameState__Init
    {
        public static void Postfix(CombatGameState __instance)
        {
            Mod.Log.Trace?.Write("CGS:_I entered.");
            Mod.Log.Debug?.Write("Caching CombatGameState");

            // Vanilla clamps BaseInitiative, ForceUnitOnePhaseDown/Up, ForceUnitToLastPhase and the AI reserve target
            //   to PhaseSpecial..PhaseAssault (1..5). Widen them to our phase range so they agree with the TurnDirector.
            PhaseConstantsDef phaseConsts = __instance.Constants.Phase;
            phaseConsts.PhaseSpecial = Mod.MinPhase;
            phaseConsts.PhaseAssault = Mod.MaxPhase;
            __instance.Constants.Phase = phaseConsts;
            Mod.Log.Info?.Write($"Set phase constants to PhaseSpecial: {__instance.Constants.Phase.PhaseSpecial}  PhaseAssault: {__instance.Constants.Phase.PhaseAssault}");

            // Pre-load our required icons, otherwise DM will unload them as they aren't necessary
            DataManager dm = UnityGameInstance.BattleTechGame.DataManager;
            LoadRequest loadRequest = dm.CreateLoadRequest();

            // Need to load each unique icon
            Mod.Log.Info?.Write("LOADING EFFECT ICONS...");
            loadRequest.AddLoadRequest<SVGAsset>(BattleTechResourceType.SVGAsset, Mod.Config.Icons.Stopwatch, null);
            loadRequest.ProcessRequests();
            Mod.Log.Info?.Write("  ICON LOADING COMPLETE!");
        }
    }

}
