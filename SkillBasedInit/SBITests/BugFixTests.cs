using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;
using System.Collections.Generic;
using System.Reflection;

namespace SBITests
{
    // Regression tests for the upstream bugs fixed in BTX_SkillBasedInit (see DESIGN.md, Bug fixes)
    [TestClass]
    public class BugFixTests : SBITestBase
    {
        [TestMethod]
        public void TestInitiativeToString()
        {
            Assert.AreEqual("30", InitiativeHelper.InitiativeToString(1));
            Assert.AreEqual("15", InitiativeHelper.InitiativeToString(16));
            Assert.AreEqual("1", InitiativeHelper.InitiativeToString(30));

            // Out of range values clamp instead of returning ERROR
            Assert.AreEqual("30", InitiativeHelper.InitiativeToString(0));
            Assert.AreEqual("30", InitiativeHelper.InitiativeToString(-4));
            Assert.AreEqual("1", InitiativeHelper.InitiativeToString(31));
        }

        [TestMethod]
        public void TestIsValidInitiative()
        {
            Assert.IsFalse(InitiativeHelper.IsValidInitiative(0));
            Assert.IsTrue(InitiativeHelper.IsValidInitiative(1));
            Assert.IsTrue(InitiativeHelper.IsValidInitiative(6));
            Assert.IsTrue(InitiativeHelper.IsValidInitiative(30));
            Assert.IsFalse(InitiativeHelper.IsValidInitiative(31));
        }

        // Upstream's postfix took 'bool __result' by value, so phases 6-30 were still reported invalid
        [TestMethod]
        public void TestHasValidInitiativePatch()
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(AbstractActor), "HasValidInitiative");
            HarmonyMethod postfix = new HarmonyMethod(typeof(AbstractActor_HasValidInitiative), "Postfix");
            TestGlobalInit.HarmonyInst.Patch(getter, postfix: postfix);
            try
            {
                Mech mech = TestHelper.BuildTestMech(tonnage: 50);

                mech.Initiative = 20;
                Assert.IsTrue(mech.HasValidInitiative);

                mech.Initiative = 30;
                Assert.IsTrue(mech.HasValidInitiative);

                mech.Initiative = 31;
                Assert.IsFalse(mech.HasValidInitiative);
            }
            finally
            {
                TestGlobalInit.HarmonyInst.Unpatch(getter, HarmonyPatchType.Postfix, TestGlobalInit.HarmonyInst.Id);
            }
        }

        [TestMethod]
        public void TestTonnageToPhase_IgnoresInsertionOrder()
        {
            // Same brackets as the defaults, inserted largest-first
            Dictionary<int, int> reversed = new Dictionary<int, int>
            {
                { 999, 6 }, { 100, 9 }, { 95, 10 }, { 85, 11 }, { 75, 12 }, { 65, 13 },
                { 55, 14 }, { 45, 15 }, { 35, 16 }, { 25, 17 }, { 15, 18 }, { 5, 19 }
            };

            Assert.AreEqual(19, InitiativeHelper.TonnageToPhase(reversed, 5));
            Assert.AreEqual(14, InitiativeHelper.TonnageToPhase(reversed, 50));
            Assert.AreEqual(14, InitiativeHelper.TonnageToPhase(reversed, 55));
            Assert.AreEqual(13, InitiativeHelper.TonnageToPhase(reversed, 56));
            Assert.AreEqual(9, InitiativeHelper.TonnageToPhase(reversed, 100));
            Assert.AreEqual(6, InitiativeHelper.TonnageToPhase(reversed, 101));
        }

        [TestMethod]
        public void TestTonnageToPhase_AboveAllKeys()
        {
            Dictionary<int, int> brackets = new Dictionary<int, int> { { 35, 16 }, { 100, 9 } };

            // Upstream's MechDef lookup returned phase 0 here (then initiative 31, which is invalid)
            Assert.AreEqual(9, InitiativeHelper.TonnageToPhase(brackets, 150));
            Assert.AreEqual(0, InitiativeHelper.TonnageToPhase(new Dictionary<int, int>(), 50));
        }

        // Upstream compared against the raw (negative) min, so a big Piloting bonus pushed max below min
        //   and Random.Next(min, max + 1) threw ArgumentOutOfRangeException
        [TestMethod]
        public void TestRandomnessBounds_HighPiloting()
        {
            Mod.Config.Mech.RandomnessMin = -3;
            Mod.Config.Mech.RandomnessMax = -9;

            Pilot pilot = TestHelper.BuildTestPilot();
            pilot.StatCollection.Set<int>(ModStats.MOD_SKILL_PILOT, 20);

            int[] bounds = pilot.RandomnessBounds(Mod.Config.Mech.RandomnessMin, Mod.Config.Mech.RandomnessMax);
            Assert.AreEqual(3, bounds[0]);
            Assert.AreEqual(3, bounds[1]);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(3, pilot.RandomnessModifier(Mod.Config.Mech));
            }
        }
    }
}
