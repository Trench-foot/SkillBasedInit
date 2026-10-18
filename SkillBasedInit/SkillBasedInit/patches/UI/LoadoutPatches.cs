using BattleTech.UI.Tooltips;
using Localize;
using SkillBasedInit.Helper;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SkillBasedInit.Patches
{

    // Sets the initiative value in the mech-bay
    [HarmonyPatch(typeof(MechBayMechInfoWidget), "SetInitiative")]
    [HarmonyPatch(new Type[] { })]
    public static class MechBayMechInfoWidget_SetInitiative
    {
        public static void Postfix(MechBayMechInfoWidget __instance, MechDef ___selectedMech,
            GameObject ___initiativeObj, TextMeshProUGUI ___initiativeText, HBSTooltip ___initiativeTooltip)
        {
            Mod.Log.Trace?.Write("MBMIW:SI:post - entered.");

            if (___initiativeObj == null || ___initiativeText == null)
            {
                return;
            }

            //SkillBasedInit.Logger.Log($"MechBayMechInfoWidget::SetInitiative::post - disabling text");
            if (___selectedMech == null)
            {
                ___initiativeObj.SetActive(true);
                ___initiativeText.SetText("-");
            }
            else
            {
                List<string> details = new List<string>();

                // No pilot or lance here, so only the unit's own modifiers apply
                int staticInit = LoadoutHelper.ClampInitiative(LoadoutHelper.StaticInitiative(___selectedMech, null, 0, details));
                int staticPhase = InitiativeHelper.InitiativeToPhase(staticInit);

                // --- Badge ---
                // Upstream showed the raw init value here, which didn't match the number the combat HUD shows
                ___initiativeObj.SetActive(true);
                ___initiativeText.SetText($"{staticPhase}");

                // --- Tooltip ---
                details.Add(new Text(Mod.LocalizedText.MechBay[ModText.LT_MB_EXPECTED_NO_PILOT], new object[] { staticPhase }).ToString());

                string tooltipTitle = $"{___selectedMech.Name}";
                string tooltipText = String.Join("\n", details.ToArray());
                BaseDescriptionDef initiativeData = new BaseDescriptionDef("MB_MIW_MECH_TT", tooltipTitle, tooltipText, null);
                ___initiativeTooltip.enabled = true;
                ___initiativeTooltip.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(initiativeData));
            }
        }
    }

    // Sets the initiative value in the lance loading screen. Because we want it to be understandable to players, 
    //  invert the values so they reflect the phase IDs, not the actual value
    [HarmonyPatch(typeof(LanceLoadoutSlot), "RefreshInitiativeData")]
    [HarmonyPatch(new Type[] { })]
    public static class LanceLoadoutSlot_RefreshInitiativeData
    {
        public static void Postfix(LanceLoadoutSlot __instance, GameObject ___initiativeObj, TextMeshProUGUI ___initiativeText,
            UIColorRefTracker ___initiativeColor, HBSTooltip ___initiativeTooltip, LanceConfiguratorPanel ___LC)
        {
            Mod.Log.Trace?.Write("LLS:RID:post - entered.");

            if (___initiativeObj == null || ___initiativeText == null || ___initiativeColor == null || ___initiativeTooltip == null)
            {
                return;
            }

            //SkillBasedInit.Logger.Log($"LanceLoadoutSlot::RefreshInitiativeData::post - disabling text");
            bool bothSelected = __instance.SelectedMech != null && __instance.SelectedPilot != null;
            if (!bothSelected)
            {
                ___initiativeText.SetText("-");
                ___initiativeColor.SetUIColor(UIColor.MedGray);
            }
            else
            {
                Mod.Log.Debug?.Write($"Building init data for mechdef: {__instance.SelectedMech.name} with pilot: {__instance.SelectedPilot.name}");

                MechDef selectedMechDef = __instance.SelectedMech.MechDef;
                Pilot selectedPilot = __instance.SelectedPilot.Pilot;
                List<string> details = new List<string>();

                // Upstream added lanceInitiativeModifier with the wrong sign; LoadoutHelper inverts and scales it
                int lanceInitiativeModifier = ___LC != null ? ___LC.lanceInitiativeModifier : 0;
                int initValue = LoadoutHelper.StaticInitiative(selectedMechDef, selectedPilot, lanceInitiativeModifier, details);

                UnitCfg unitCfg = selectedMechDef.GetUnitConfig();
                int[] randomnessBounds = selectedPilot.RandomnessBounds(unitCfg.RandomnessMin, unitCfg.RandomnessMax);

                // --- Tooltip ---
                int maxInit = LoadoutHelper.ClampInitiative(initValue + randomnessBounds[0]);
                int minInit = LoadoutHelper.ClampInitiative(initValue + randomnessBounds[1]);
                int avgMod = (int)Math.Ceiling((maxInit - minInit) / 2f);
                int avgInit = maxInit - avgMod;

                // Convert to phases for humans 
                int maxPhase = InitiativeHelper.InitiativeToPhase(maxInit);
                int minPhase = InitiativeHelper.InitiativeToPhase(minInit);
                int avgPhase = InitiativeHelper.InitiativeToPhase(avgInit);

                // --- Badge ---
                ___initiativeText.SetText($"{avgPhase}");
                ___initiativeText.color = Color.black;
                ___initiativeColor.SetUIColor(UIColor.White);

                // Total before randomness, shown as the HUD would show it
                int totalPhase = InitiativeHelper.InitiativeToPhase(LoadoutHelper.ClampInitiative(initValue));
                details.Add(new Text(Mod.LocalizedText.MechBay[ModText.LT_MB_TOTAL], new object[] { totalPhase }).ToString());
                details.Add(new Text(Mod.LocalizedText.MechBay[ModText.LT_MB_RANDOM], new object[] { randomnessBounds[0], randomnessBounds[1] }).ToString());
                details.Add(new Text(Mod.LocalizedText.MechBay[ModText.LT_MB_EXPECTED], new object[] { minPhase, maxPhase }).ToString());

                string tooltipTitle = $"{selectedMechDef.Name}: {selectedPilot.Name}";
                string tooltipText = String.Join("\n", details.ToArray());
                BaseDescriptionDef initiativeData = new BaseDescriptionDef("LLS_MECH_TT", tooltipTitle, tooltipText, null);
                ___initiativeTooltip.enabled = true;
                ___initiativeTooltip.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(initiativeData));
            }
        }

    }
}