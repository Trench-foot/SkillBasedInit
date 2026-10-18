using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;

namespace SBITests
{
    // PhaseTrackSlot must always return a valid index. Vanilla's PhaseIcons[phase - 1] threw for phases above 5,
    //   which broke CombatHUD.Init when CustomUnits spawned assault mechs at PhaseAssault (30).
    [TestClass]
    public class PhaseTrackTests : SBITestBase
    {
        [TestMethod]
        public void TestNotInterleaved_UsesLastSlot()
        {
            // The mission-start crash: an assault mech at phase 30 before combat is interleaved
            Assert.AreEqual(4, InitiativeHelper.PhaseTrackSlot(phase: 30, currentPhase: 0, isInterleaved: false, slotCount: 5));
            Assert.AreEqual(4, InitiativeHelper.PhaseTrackSlot(phase: 2, currentPhase: 0, isInterleaved: false, slotCount: 5));
        }

        [TestMethod]
        public void TestStartOfRound()
        {
            // Phase 1 => bounds shown as 30 29 28 27 26
            Assert.AreEqual(0, InitiativeHelper.PhaseTrackSlot(phase: 1, currentPhase: 1, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(1, InitiativeHelper.PhaseTrackSlot(phase: 2, currentPhase: 1, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(2, InitiativeHelper.PhaseTrackSlot(phase: 3, currentPhase: 1, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(3, InitiativeHelper.PhaseTrackSlot(phase: 4, currentPhase: 1, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(4, InitiativeHelper.PhaseTrackSlot(phase: 5, currentPhase: 1, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(4, InitiativeHelper.PhaseTrackSlot(phase: 30, currentPhase: 1, isInterleaved: true, slotCount: 5));
        }

        [TestMethod]
        public void TestMidRound()
        {
            // Phase 15 => bounds shown as 18 17 16 15 14, current phase in the middle
            Assert.AreEqual(0, InitiativeHelper.PhaseTrackSlot(phase: 1, currentPhase: 15, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(1, InitiativeHelper.PhaseTrackSlot(phase: 14, currentPhase: 15, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(2, InitiativeHelper.PhaseTrackSlot(phase: 15, currentPhase: 15, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(3, InitiativeHelper.PhaseTrackSlot(phase: 16, currentPhase: 15, isInterleaved: true, slotCount: 5));
            Assert.AreEqual(4, InitiativeHelper.PhaseTrackSlot(phase: 30, currentPhase: 15, isInterleaved: true, slotCount: 5));
        }

        [TestMethod]
        public void TestAlwaysValidIndex()
        {
            for (int currentPhase = 0; currentPhase <= 31; currentPhase++)
            {
                for (int phase = -2; phase <= 33; phase++)
                {
                    foreach (bool interleaved in new[] { true, false })
                    {
                        int slot = InitiativeHelper.PhaseTrackSlot(phase, currentPhase, interleaved, slotCount: 5);
                        Assert.IsTrue(slot >= 0 && slot <= 4, $"phase {phase} currentPhase {currentPhase} interleaved {interleaved} => {slot}");
                    }
                }
            }

            // A track with fewer slots than expected still gets a valid index
            Assert.AreEqual(2, InitiativeHelper.PhaseTrackSlot(phase: 15, currentPhase: 15, isInterleaved: true, slotCount: 3));
            Assert.AreEqual(0, InitiativeHelper.PhaseTrackSlot(phase: 15, currentPhase: 15, isInterleaved: true, slotCount: 1));
        }
    }
}
