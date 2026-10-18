using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using SkillBasedInit.Helper;
using System.Collections.Generic;

namespace SBITests
{
    // Rolls are a hash of (contract, unit, round, kind): reproducible on reload, independent of call order (DESIGN.md, Randomness)
    [TestClass]
    public class DeterministicRollTests : SBITestBase
    {
        private static Mech BuildMech(string guid, int round)
        {
            Mech mech = TestHelper.BuildTestMech(tonnage: 50);
            mech.SetGuid(guid);
            Traverse.Create(mech.Combat.TurnDirector).Property("CurrentRound").SetValue(round);
            return mech;
        }

        [TestMethod]
        public void TestSameKeySameResult()
        {
            int first = DeterministicRoll.Roll("contract|unit|3|1", 3, 9);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(first, DeterministicRoll.Roll("contract|unit|3|1", 3, 9));
            }
        }

        [TestMethod]
        public void TestRangeAndSpread()
        {
            // 1000 different units: every value in 3..9 should appear, none outside, roughly evenly (~143 each)
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < 1000; i++)
            {
                int roll = DeterministicRoll.Roll($"contract|unit-{i}|1|1", 3, 9);
                Assert.IsTrue(roll >= 3 && roll <= 9, $"roll {roll} out of range");
                counts[roll] = counts.TryGetValue(roll, out int c) ? c + 1 : 1;
            }

            for (int value = 3; value <= 9; value++)
            {
                Assert.IsTrue(counts.ContainsKey(value), $"value {value} never rolled");
                Assert.IsTrue(counts[value] > 90 && counts[value] < 200, $"value {value} rolled {counts[value]} times");
            }
        }

        [TestMethod]
        public void TestDegenerateRange()
        {
            Assert.AreEqual(4, DeterministicRoll.Roll("k", 4, 4));
            Assert.AreEqual(4, DeterministicRoll.Roll("k", 4, 2));
        }

        [TestMethod]
        public void TestReloadReproducesRoll()
        {
            // A reloaded unit is a new object with the same GUID, in the same round
            Mech beforeSave = BuildMech("unit-reload", round: 4);
            Mech afterLoad = BuildMech("unit-reload", round: 4);

            foreach (RollKind kind in new[] { RollKind.Randomness, RollKind.Prone, RollKind.Hesitation })
            {
                Assert.AreEqual(
                    DeterministicRoll.Roll(beforeSave, kind, 0, 20),
                    DeterministicRoll.Roll(afterLoad, kind, 0, 20));
            }
        }

        [TestMethod]
        public void TestRoundsAndKindsDiffer()
        {
            HashSet<int> byRound = new HashSet<int>();
            for (int round = 1; round <= 10; round++)
            {
                byRound.Add(DeterministicRoll.Roll(BuildMech("unit-rounds", round), RollKind.Randomness, 0, 20));
            }
            Assert.IsTrue(byRound.Count > 1, "every round rolled the same value");

            Mech mech = BuildMech("unit-kinds", round: 2);
            HashSet<int> byKind = new HashSet<int>();
            foreach (RollKind kind in new[] { RollKind.Randomness, RollKind.Inspired, RollKind.Prone, RollKind.Crippled, RollKind.Shutdown, RollKind.Hesitation })
            {
                byKind.Add(DeterministicRoll.Roll(mech, kind, 0, 20));
            }
            Assert.IsTrue(byKind.Count > 1, "every roll kind rolled the same value");
        }

        // The tooltip used to re-roll prone, crippled and shutdown on every hover; now it matches what combat applied
        [TestMethod]
        public void TestRepeatedModifierCallsAgree()
        {
            Mod.Config.Mech.RandomnessMin = -3;
            Mod.Config.Mech.RandomnessMax = -9;

            Mech mech = BuildMech("unit-tooltip", round: 3);
            int first = mech.GetPilot().RandomnessModifier(Mod.Config.Mech);
            Assert.IsTrue(first >= 3 && first <= 9);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(first, mech.GetPilot().RandomnessModifier(Mod.Config.Mech));
            }
        }
    }
}
