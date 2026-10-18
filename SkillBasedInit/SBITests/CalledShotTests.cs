using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;

namespace SBITests
{
    // Called shot penalty is a hybrid: CalledShot.BasePhases + max(0, attacker avg(Gunnery,Tactics) - target avg(Guts,Tactics)).
    //   No randomness, so every value is exact. Skill 1 => mod 0, 6 => 3, 10 => 5.
    [TestClass]
    public class CalledShotTests : SBITestBase
    {
        [TestInitialize]
        public void ClassInitialize()
        {
            // Patch isMoraleInspired to return true
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsFuryInspired, prefix: TestGlobalInit.HM_AlwaysFalse);
            TestGlobalInit.HarmonyInst.Patch(TestGlobalInit.MI_IsMoraleInspired, prefix: TestGlobalInit.HM_AlwaysFalse);

            Mod.Config.Mech.TypeMod = 0;
            Mod.Config.CalledShot.BasePhases = 3;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsFuryInspired, HarmonyPatchType.Prefix);
            TestGlobalInit.HarmonyInst.Unpatch(TestGlobalInit.MI_IsMoraleInspired, HarmonyPatchType.Prefix);
        }

        private static Mech BuildAttacker(int gunnery, int tactics)
        {
            Mech attacker = TestHelper.BuildTestMech(tonnage: 50);
            attacker.GetPilot().StatCollection.Set<int>("Gunnery", gunnery);
            attacker.GetPilot().StatCollection.Set<int>("Tactics", tactics);
            attacker.StatCollection.Set<int>(ModStats.MOD_CALLED_SHOT_ATTACKER, 0);
            return attacker;
        }

        private static Mech BuildTarget(int guts, int tactics)
        {
            Mech target = TestHelper.BuildTestMech(tonnage: 50);
            target.GetPilot().StatCollection.Set<int>("Guts", guts);
            target.GetPilot().StatCollection.Set<int>("Tactics", tactics);
            target.StatCollection.Set<int>(ModStats.MOD_CALLED_SHOT_TARGET, 0);
            return target;
        }

        [TestMethod]
        public void TestBasePhases()
        {
            Mech attacker = BuildAttacker(gunnery: 1, tactics: 1);
            Mech target = BuildTarget(guts: 1, tactics: 1);

            // Equal skills => base only
            Assert.AreEqual(3, target.CalledShotInitMod(attacker));

            Mod.Config.CalledShot.BasePhases = 5;
            Assert.AreEqual(5, target.CalledShotInitMod(attacker));

            Mod.Config.CalledShot.BasePhases = 0;
            Assert.AreEqual(0, target.CalledShotInitMod(attacker));
        }

        [TestMethod]
        public void TestIsDeterministic()
        {
            Mech attacker = BuildAttacker(gunnery: 6, tactics: 6);
            Mech target = BuildTarget(guts: 1, tactics: 1);

            int first = target.CalledShotInitMod(attacker);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(first, target.CalledShotInitMod(attacker));
            }
        }

        [TestMethod]
        public void TestAttackerBiasSkills()
        {
            Mech target = BuildTarget(guts: 1, tactics: 1);

            // (gunMod 0 + tacMod 5) => avg 3 => 3 + 3
            Assert.AreEqual(6, target.CalledShotInitMod(BuildAttacker(gunnery: 1, tactics: 10)));

            // (gunMod 5 + tacMod 5) => avg 5 => 3 + 5
            Assert.AreEqual(8, target.CalledShotInitMod(BuildAttacker(gunnery: 10, tactics: 10)));
        }

        [TestMethod]
        public void TestAttackerBiasMods()
        {
            Mech target = BuildTarget(guts: 1, tactics: 1);
            Mech attacker = BuildAttacker(gunnery: 1, tactics: 1);

            // Attacker mod is interpreted as a phase modifier, so inverted: -3 => +3
            attacker.StatCollection.Set<int>(ModStats.MOD_CALLED_SHOT_ATTACKER, -3);
            Assert.AreEqual(6, target.CalledShotInitMod(attacker));

            // +3 => -3, and a negative skill difference never goes below the base
            attacker.StatCollection.Set<int>(ModStats.MOD_CALLED_SHOT_ATTACKER, 3);
            Assert.AreEqual(3, target.CalledShotInitMod(attacker));
        }

        [TestMethod]
        public void TestTargetBias()
        {
            Mech attacker = BuildAttacker(gunnery: 10, tactics: 10);

            // attacker 5 - target 0
            Assert.AreEqual(8, BuildTarget(guts: 1, tactics: 1).CalledShotInitMod(attacker));

            // attacker 5 - target 3
            Assert.AreEqual(5, BuildTarget(guts: 6, tactics: 6).CalledShotInitMod(attacker));

            // attacker 5 - target 5 => base only
            Assert.AreEqual(3, BuildTarget(guts: 10, tactics: 10).CalledShotInitMod(attacker));

            // Target mod is a phase modifier: -2 => target 2 better
            Mech target = BuildTarget(guts: 1, tactics: 1);
            target.StatCollection.Set<int>(ModStats.MOD_CALLED_SHOT_TARGET, -2);
            Assert.AreEqual(6, target.CalledShotInitMod(attacker));
        }
    }
}
