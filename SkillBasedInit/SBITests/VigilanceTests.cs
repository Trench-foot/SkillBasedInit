using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;

namespace SBITests
{
    // Vigilance bonus is a hybrid: Vigilance.BasePhases + avg(Guts,Tactics), returned as a negative (earlier).
    //   No randomness, so every value is exact. Skill 1 => mod 0, 4 => 2, 6 => 3, 8 => 4, 10 => 5.
    [TestClass]
    public class VigilanceTests : SBITestBase
    {
        [TestInitialize]
        public void ClassInitialize()
        {
            // Patch isMoraleInspired to return true
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsFuryInspired, prefix: TestGlobalInit.HM_AlwaysFalse);
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsMoraleInspired, prefix: TestGlobalInit.HM_AlwaysFalse);

            Mod.Config.Mech.TypeMod = 0;
            Mod.Config.Vigilance.BasePhases = 3;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsFuryInspired, HarmonyPatchType.Prefix);
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsMoraleInspired, HarmonyPatchType.Prefix);
        }

        private static Mech BuildMech(int guts, int tactics)
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            mech.GetPilot().StatCollection.Set<int>("Guts", guts);
            mech.GetPilot().StatCollection.Set<int>("Tactics", tactics);
            mech.StatCollection.Set<int>(ModStats.MOD_VIGILANCE, 0);
            return mech;
        }

        [TestMethod]
        public void TestBasePhases()
        {
            Mech mech = BuildMech(guts: 1, tactics: 1);
            Assert.AreEqual(-3, mech.VigilanceInitMod());

            Mod.Config.Vigilance.BasePhases = 6;
            Assert.AreEqual(-6, mech.VigilanceInitMod());

            Mod.Config.Vigilance.BasePhases = 0;
            Assert.AreEqual(0, mech.VigilanceInitMod());
        }

        [TestMethod]
        public void TestPilotSkills()
        {
            // (guts 2 + tacMod 3) => avg 3
            Assert.AreEqual(-6, BuildMech(guts: 4, tactics: 6).VigilanceInitMod());

            // (guts 4 + tacMod 3) => avg 4
            Assert.AreEqual(-7, BuildMech(guts: 8, tactics: 6).VigilanceInitMod());

            // (guts 5 + tacMod 5) => avg 5
            Assert.AreEqual(-8, BuildMech(guts: 10, tactics: 10).VigilanceInitMod());
        }

        [TestMethod]
        public void TestPilotMod()
        {
            Mech mech = BuildMech(guts: 1, tactics: 1);

            // Positive mod increases the bonus
            mech.StatCollection.Set<int>(ModStats.MOD_VIGILANCE, 2);
            Assert.AreEqual(-5, mech.VigilanceInitMod());

            // A large negative mod can cancel the bonus, but never turns it into a penalty
            mech.StatCollection.Set<int>(ModStats.MOD_VIGILANCE, -4);
            Assert.AreEqual(0, mech.VigilanceInitMod());
        }
    }
}
