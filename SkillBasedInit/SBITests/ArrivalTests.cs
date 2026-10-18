using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;

namespace SBITests
{
    // Units joining after round 1 get a full roll plus Arrival.DelayPhases, never earlier than the phase in progress
    [TestClass]
    public class ArrivalTests : SBITestBase
    {
        [TestInitialize]
        public void ClassInitialize()
        {
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsFuryInspired, prefix: TestGlobalInit.HM_AlwaysFalse);
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsMoraleInspired, prefix: TestGlobalInit.HM_AlwaysFalse);

            Mod.Config.Mech.TypeMod = 0;
            Mod.Config.Mech.RandomnessMax = 0;
            Mod.Config.Mech.RandomnessMin = 0;
            Mod.Config.Arrival.DelayPhases = 5;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsFuryInspired, HarmonyPatchType.Prefix);
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsMoraleInspired, HarmonyPatchType.Prefix);
        }

        private static Mech BuildArrivingMech(int currentPhase)
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            Traverse.Create(mech.Combat.TurnDirector).Property("CurrentPhase").SetValue(currentPhase);
            return mech;
        }

        [TestMethod]
        public void TestDelayApplied()
        {
            // Tonnage = phase 14 => init 17, + 5 arrival
            Mech mech = BuildArrivingMech(currentPhase: 1);
            Assert.AreEqual(5, InitiativeHelper.ArrivalInitiative(mech, applyDelay: true));
            Assert.AreEqual(22, mech.Initiative);
        }

        [TestMethod]
        public void TestNoDelayInRoundOne()
        {
            Mech mech = BuildArrivingMech(currentPhase: 1);
            Assert.AreEqual(0, InitiativeHelper.ArrivalInitiative(mech, applyDelay: false));
            Assert.AreEqual(17, mech.Initiative);
        }

        [TestMethod]
        public void TestNotBeforeCurrentPhase()
        {
            // Rolled 22, but phase 25 is already in progress => act in 25 rather than skip the round
            Mech mech = BuildArrivingMech(currentPhase: 25);
            InitiativeHelper.ArrivalInitiative(mech, applyDelay: true);
            Assert.AreEqual(25, mech.Initiative);
        }

        [TestMethod]
        public void TestClampedToMaxPhase()
        {
            Mod.Config.Arrival.DelayPhases = 20;
            Mech mech = BuildArrivingMech(currentPhase: 1);
            InitiativeHelper.ArrivalInitiative(mech, applyDelay: true);
            Assert.AreEqual(Mod.MaxPhase, mech.Initiative);
        }
    }
}
