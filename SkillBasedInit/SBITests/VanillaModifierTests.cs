using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;
using System.Collections.Generic;

namespace SBITests
{
    // Vanilla/modpack BaseInitiative modifiers folded into SBI phases (DESIGN.md, Per-round initiative)
    [TestClass]
    public class VanillaModifierTests : SBITestBase
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

        // Matches CustomUnits Mech_InitStats: BaseInitiative's default is the weight class phase
        private static Mech BuildMechWithBaseInit(int defaultValue = 3)
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            mech.StatCollection.AddStatistic<int>(ModStats.HBS_BASE_INITIATIVE, defaultValue);
            return mech;
        }

        private static void AddBaseInitEffect(AbstractActor actor, int modValue)
        {
            actor.StatCollection.ModifyStat<int>("test", -1, ModStats.HBS_BASE_INITIATIVE, StatCollection.StatOperation.Int_Add, modValue);
        }

        [TestMethod]
        public void TestNoStat()
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            Assert.AreEqual(0, InitiativeHelper.VanillaModifierPoints(mech));

            InitiativeHelper.UpdateInitiative(mech);
            // Tonnage = phase 14 => init 17, no vanilla modifiers
            Assert.AreEqual(17, mech.Initiative);
        }

        [TestMethod]
        public void TestUnmodifiedStat()
        {
            Mech mech = BuildMechWithBaseInit();
            Assert.AreEqual(0, InitiativeHelper.VanillaModifierPoints(mech));

            InitiativeHelper.UpdateInitiative(mech);
            Assert.AreEqual(17, mech.Initiative);
        }

        [TestMethod]
        public void TestDefaultValueDoesNotMatter()
        {
            // CustomUnits uses PhaseAssault for assault mechs; only the delta should count
            Mech mech = BuildMechWithBaseInit(defaultValue: 30);
            AddBaseInitEffect(mech, -1);
            Assert.AreEqual(-1, InitiativeHelper.VanillaModifierPoints(mech));
        }

        [TestMethod]
        public void TestBonusGear()
        {
            // Command console style: Int_Add -1 => 1 point earlier => 3 phases earlier
            Mech mech = BuildMechWithBaseInit();
            AddBaseInitEffect(mech, -1);
            InitiativeHelper.UpdateInitiative(mech);
            Assert.AreEqual(14, mech.Initiative);

            // Console + lance comms stack
            mech = BuildMechWithBaseInit();
            AddBaseInitEffect(mech, -1);
            AddBaseInitEffect(mech, -1);
            InitiativeHelper.UpdateInitiative(mech);
            Assert.AreEqual(11, mech.Initiative);
        }

        [TestMethod]
        public void TestPenalty()
        {
            // TisButAScratch concussion style: Int_Add +1 => 3 phases later
            Mech mech = BuildMechWithBaseInit();
            AddBaseInitEffect(mech, 1);
            InitiativeHelper.UpdateInitiative(mech);
            Assert.AreEqual(20, mech.Initiative);
        }

        [TestMethod]
        public void TestPhasesPerPoint()
        {
            Mod.Config.VanillaModifiers.PhasesPerPoint = 5;

            Mech mech = BuildMechWithBaseInit();
            AddBaseInitEffect(mech, -1);
            InitiativeHelper.UpdateInitiative(mech);
            Assert.AreEqual(12, mech.Initiative);
        }

        [TestMethod]
        public void TestClampedToMinPhase()
        {
            Mech mech = BuildMechWithBaseInit();
            for (int i = 0; i < 10; i++) AddBaseInitEffect(mech, -1);
            InitiativeHelper.UpdateInitiative(mech);
            Assert.AreEqual(Mod.MinPhase, mech.Initiative);
        }

        [TestMethod]
        public void TestSelfEffectPoints_SkipsLanceWide()
        {
            List<EffectData> effects = new List<EffectData>
            {
                BuildInitEffect("-1", EffectTargetType.Creator),
                BuildInitEffect("-1", EffectTargetType.AllLanceMates),
                BuildInitEffect("1", EffectTargetType.Creator),
                BuildInitEffect("-1", EffectTargetType.Creator, statName: "TurnRadius"),
            };

            // Only the two self-targeted initiative effects count: -1 + 1
            Assert.AreEqual(0, InitiativeHelper.SelfEffectPoints(effects));

            effects.RemoveAt(2);
            Assert.AreEqual(-1, InitiativeHelper.SelfEffectPoints(effects));
        }

        private static EffectData BuildInitEffect(string modValue, EffectTargetType target, string statName = ModStats.HBS_BASE_INITIATIVE)
        {
            EffectData effect = new EffectData();
            effect.effectType = EffectType.StatisticEffect;
            effect.statisticData = new StatisticEffectData();
            effect.statisticData.statName = statName;
            effect.statisticData.operation = StatCollection.StatOperation.Int_Add;
            effect.statisticData.modValue = modValue;
            effect.targetingData = new EffectTargetingData();
            effect.targetingData.effectTargetType = target;
            effect.durationData = new EffectDurationData();
            return effect;
        }
    }
}
