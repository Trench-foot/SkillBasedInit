using Localize;
using System;
using System.Collections.Generic;

namespace SkillBasedInit.Helper
{
    // Out-of-combat initiative for the Mech Bay and lance loadout screens. Shared so both screens agree.
    //   Returns an actor.Initiative value (+ = later). Detail lines are shown as initiative (+ = earlier),
    //   matching the combat HUD.
    public static class LoadoutHelper
    {
        public static int StaticInitiative(MechDef mechDef, Pilot pilot, int lanceInitiativeModifier, List<string> details)
        {
            // Tonnage
            int tonnageMod = mechDef.GetBaseInitByTonnage();
            int initiative = tonnageMod;
            details.Add(new Text(Mod.LocalizedText.MechBay[ModText.LT_MB_TONNAGE], new object[] { InitiativeHelper.InitiativeToPhase(tonnageMod) }).ToString());

            // Unit type
            int typeMod = mechDef.GetTypeModifier();
            initiative += typeMod;
            AddSignedDetail(details, ModText.LT_MB_UNIT_TYPE, typeMod, alwaysShow: true);

            // Gear on the unit itself
            int gearMod = InitiativeHelper.VanillaPointsToPhases(InitiativeHelper.ComponentModifierPoints(mechDef));
            initiative += gearMod;
            AddSignedDetail(details, ModText.LT_MB_COMPONENTS, gearMod);

            if (pilot != null)
            {
                // Tactics is positive for better pilots, which moves them earlier
                int tacticsMod = pilot.SBITacticsMod();
                initiative -= tacticsMod;
                details.Add(new Text(Mod.LocalizedText.MechBay[ModText.LT_MB_TACTICS], new object[] { tacticsMod }).ToString());

                int pilotTagsMod = PilotHelper.GetTagsModifier(pilot);
                initiative += pilotTagsMod;
                details.AddRange(PilotHelper.GetTagsModifierDetails(pilot));

                int abilitiesMod = InitiativeHelper.VanillaPointsToPhases(InitiativeHelper.PilotAbilityModifierPoints(pilot));
                initiative += abilitiesMod;
                AddSignedDetail(details, ModText.LT_MB_PILOT_ABILITIES, abilitiesMod);
            }

            // Lance-wide effects from every unit in the lance. Vanilla reports this as initiative (+ = earlier), so invert to points.
            int lanceMod = InitiativeHelper.VanillaPointsToPhases(lanceInitiativeModifier * -1);
            initiative += lanceMod;
            AddSignedDetail(details, ModText.LT_MB_LANCE, lanceMod);

            return initiative;
        }

        public static int ClampInitiative(int initiative)
        {
            return Math.Min(Math.Max(initiative, Mod.MinPhase), Mod.MaxPhase);
        }

        // initiativeDelta is + = later; the detail line shows it as + = earlier
        private static void AddSignedDetail(List<string> details, string textKey, int initiativeDelta, bool alwaysShow = false)
        {
            if (initiativeDelta == 0 && !alwaysShow) return;

            int displayMod = initiativeDelta * -1;
            string color = displayMod >= 0 ? "00FF00" : "FF0000";
            details.Add(new Text(Mod.LocalizedText.MechBay[textKey], new object[] { color, displayMod }).ToString());
        }
    }
}
