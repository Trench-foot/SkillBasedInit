using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;

namespace SBITests
{
    // Melee hit delay is a hybrid: MeleeHit.BasePhases + max(0, attacker avg(Piloting,Tactics) - target avg(Guts,Piloting)).
    //   No randomness, so every value is exact. Skill 1 => mod 0, 6 => 3, 10 => 5.
    [TestClass]
    public class MeleeHitTests : SBITestBase
    {
        [TestInitialize]
        public void ClassInitialize()
        {
            Mod.Config.MeleeHit.BasePhases = 3;
        }

        private static Mech BuildAttacker(int piloting, int tactics)
        {
            Mech attacker = TestHelper.BuildTestMech(tonnage: 50);
            attacker.GetPilot().StatCollection.Set<int>("Piloting", piloting);
            attacker.GetPilot().StatCollection.Set<int>("Tactics", tactics);
            return attacker;
        }

        private static Mech BuildTarget(int guts, int piloting)
        {
            Mech target = TestHelper.BuildTestMech(tonnage: 50);
            target.GetPilot().StatCollection.Set<int>("Guts", guts);
            target.GetPilot().StatCollection.Set<int>("Piloting", piloting);
            return target;
        }

        [TestMethod]
        public void TestBasePhases()
        {
            Mech attacker = BuildAttacker(piloting: 1, tactics: 1);
            Mech target = BuildTarget(guts: 1, piloting: 1);

            // Equal skills => base only
            Assert.AreEqual(3, target.MeleeHitInitMod(attacker));

            Mod.Config.MeleeHit.BasePhases = 5;
            Assert.AreEqual(5, target.MeleeHitInitMod(attacker));

            // 0 disables it, even for a skilled attacker
            Mod.Config.MeleeHit.BasePhases = 0;
            Assert.AreEqual(0, target.MeleeHitInitMod(BuildAttacker(piloting: 10, tactics: 10)));
        }

        [TestMethod]
        public void TestAttackerBias()
        {
            Mech target = BuildTarget(guts: 1, piloting: 1);

            // (pilotMod 0 + tacMod 5) => avg 3 => 3 + 3
            Assert.AreEqual(6, target.MeleeHitInitMod(BuildAttacker(piloting: 1, tactics: 10)));

            // (pilotMod 5 + tacMod 5) => avg 5 => 3 + 5
            Assert.AreEqual(8, target.MeleeHitInitMod(BuildAttacker(piloting: 10, tactics: 10)));
        }

        [TestMethod]
        public void TestTargetBias()
        {
            Mech attacker = BuildAttacker(piloting: 10, tactics: 10);

            // attacker 5 - target 3
            Assert.AreEqual(5, BuildTarget(guts: 6, piloting: 6).MeleeHitInitMod(attacker));

            // attacker 5 - target 5 => base only
            Assert.AreEqual(3, BuildTarget(guts: 10, piloting: 10).MeleeHitInitMod(attacker));

            // A better target never drops below the base
            Assert.AreEqual(3, BuildTarget(guts: 10, piloting: 10).MeleeHitInitMod(BuildAttacker(piloting: 1, tactics: 1)));
        }

        [TestMethod]
        public void TestNullActors()
        {
            Mech target = BuildTarget(guts: 1, piloting: 1);
            Assert.AreEqual(0, target.MeleeHitInitMod(null));
        }
    }

    // A delay applied 'now' only lands if the unit hasn't started activating, and isn't already in the last phase
    [TestClass]
    public class DelayTimingTests : SBITestBase
    {
        [TestMethod]
        public void TestNotYetActivated()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            mech.Initiative = 10;
            mech.HasActivatedThisRound = false;
            mech.HasBegunActivation = false;
            Assert.IsFalse(InitiativeHelper.DelayMustWaitForNextRound(mech));
        }

        [TestMethod]
        public void TestMidActivation()
        {
            // e.g. a mech that knocks itself down with a failed DFA during its own activation
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            mech.Initiative = 10;
            mech.HasActivatedThisRound = false;
            mech.HasBegunActivation = true;
            Assert.IsTrue(InitiativeHelper.DelayMustWaitForNextRound(mech));
        }

        [TestMethod]
        public void TestAlreadyActivated()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            mech.Initiative = 10;
            mech.HasActivatedThisRound = true;
            Assert.IsTrue(InitiativeHelper.DelayMustWaitForNextRound(mech));
        }

        [TestMethod]
        public void TestLastPhase()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            mech.Initiative = Mod.MaxPhase;
            mech.HasActivatedThisRound = false;
            mech.HasBegunActivation = false;
            Assert.IsTrue(InitiativeHelper.DelayMustWaitForNextRound(mech));
        }
    }
}
