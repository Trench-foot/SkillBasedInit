using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;

namespace SBITests
{
    // Hesitation stacks on consecutive reserves: no Tactics+1 decay if the unit reserved last round,
    //   carried hesitation capped at MaxPhase - MinPhase (DESIGN.md, Hesitation stacking)
    [TestClass]
    public class HesitationStackingTests : SBITestBase
    {
        [TestInitialize]
        public void ClassInitialize()
        {
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsFuryInspired, prefix: TestGlobalInit.HM_AlwaysFalse);
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsMoraleInspired, prefix: TestGlobalInit.HM_AlwaysFalse);

            Mod.Config.Mech.TypeMod = 0;
            Mod.Config.Mech.RandomnessMax = 0;
            Mod.Config.Mech.RandomnessMin = 0;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsFuryInspired, HarmonyPatchType.Prefix);
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsMoraleInspired, HarmonyPatchType.Prefix);
        }

        private static void SetRound(AbstractActor actor, int round)
        {
            Traverse.Create(actor.Combat.TurnDirector).Property("CurrentRound").SetValue(round);
        }

        [TestMethod]
        public void TestAddHesitation_StacksAndCaps()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);

            InitiativeHelper.AddHesitation(mech, 4, currentRound: 2);
            Assert.AreEqual(4, mech.StatCollection.GetValue<int>(ModStats.STATE_HESITATION));
            Assert.AreEqual(2, mech.StatCollection.GetValue<int>(ModStats.STATE_LAST_RESERVE_ROUND));

            InitiativeHelper.AddHesitation(mech, 3, currentRound: 3);
            Assert.AreEqual(7, mech.StatCollection.GetValue<int>(ModStats.STATE_HESITATION));

            InitiativeHelper.AddHesitation(mech, 40, currentRound: 4);
            Assert.AreEqual(InitiativeHelper.MaxHesitation, mech.StatCollection.GetValue<int>(ModStats.STATE_HESITATION));
        }

        [TestMethod]
        public void TestReservedLastRound()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            SetRound(mech, 3);
            Assert.IsFalse(InitiativeHelper.ReservedLastRound(mech));

            InitiativeHelper.AddHesitation(mech, 2, currentRound: 2);
            Assert.IsTrue(InitiativeHelper.ReservedLastRound(mech));

            SetRound(mech, 4);
            Assert.IsFalse(InitiativeHelper.ReservedLastRound(mech));
        }

        [TestMethod]
        public void TestNoDecayAfterConsecutiveReserve()
        {
            // Tactics 1 => mod 0, so normal decay is 1 per round
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            InitiativeHelper.AddHesitation(mech, 6, currentRound: 2);
            SetRound(mech, 3);

            InitiativeHelper.UpdateInitiative(mech);
            // Tonnage = phase 14 => init 17, + 6 carried hesitation, no decay
            Assert.AreEqual(23, mech.Initiative);
            Assert.AreEqual(6, mech.StatCollection.GetValue<int>(ModStats.STATE_HESITATION));
        }

        [TestMethod]
        public void TestDecayWhenUnitStopsReserving()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            InitiativeHelper.AddHesitation(mech, 6, currentRound: 2);

            // Round 4: the unit didn't reserve in round 3, so hesitation decays by Tactics+1
            SetRound(mech, 4);
            InitiativeHelper.UpdateInitiative(mech);
            Assert.AreEqual(22, mech.Initiative);
            Assert.AreEqual(5, mech.StatCollection.GetValue<int>(ModStats.STATE_HESITATION));
        }

        [TestMethod]
        public void TestConsecutiveReservesReachLastPhase()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            for (int round = 1; round <= 5; round++)
            {
                SetRound(mech, round);
                InitiativeHelper.UpdateInitiative(mech);
                InitiativeHelper.AddHesitation(mech, 6, currentRound: round);
            }

            SetRound(mech, 6);
            InitiativeHelper.UpdateInitiative(mech);
            // 17 + 29 (capped hesitation) clamps to the last phase
            Assert.AreEqual(Mod.MaxPhase, mech.Initiative);
        }
    }
}
