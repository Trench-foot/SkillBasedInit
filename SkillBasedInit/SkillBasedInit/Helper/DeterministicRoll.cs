using System.Text;

namespace SkillBasedInit.Helper
{
    // Each kind of roll is hashed separately, so e.g. the prone and shutdown rolls for a unit don't move together
    public enum RollKind
    {
        Randomness = 1,
        Inspired = 2,
        Prone = 3,
        Crippled = 4,
        Shutdown = 5,
        Hesitation = 6,
    }

    // Replaces System.Random. A roll is a hash of (contract, unit, round, kind), so:
    //   - reloading a save reproduces the same rolls (none of the inputs change)
    //   - rolls don't depend on how many other rolls happened first
    //   - the tooltip shows the same value combat uses, instead of re-rolling on every hover
    //   Combat.NetworkRandom isn't used: it restarts from seed 0 each combat and isn't saved.
    public static class DeterministicRoll
    {
        public static int Roll(AbstractActor actor, RollKind kind, int minInclusive, int maxInclusive)
        {
            CombatGameState combat = actor?.Combat;
            string contractId = combat?.ActiveContract?.GUID ?? string.Empty;
            string unitId = actor?.GUID ?? string.Empty;
            int round = combat?.TurnDirector?.CurrentRound ?? 0;

            return Roll($"{contractId}|{unitId}|{round}|{(int)kind}", minInclusive, maxInclusive);
        }

        // Pilots without a unit (shouldn't happen in combat) are keyed on the pilot instead
        public static int Roll(Pilot pilot, RollKind kind, int minInclusive, int maxInclusive)
        {
            if (pilot?.ParentActor != null) return Roll(pilot.ParentActor, kind, minInclusive, maxInclusive);

            return Roll($"pilot|{pilot?.GUID}|{(int)kind}", minInclusive, maxInclusive);
        }

        public static int Roll(string key, int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;

            uint range = (uint)(maxInclusive - minInclusive + 1);
            uint hash = Mix(Fnv1a(key));
            return minInclusive + (int)(hash % range);
        }

        // string.GetHashCode isn't guaranteed stable between runtimes, so hash the bytes ourselves
        private static uint Fnv1a(string key)
        {
            uint hash = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(key))
            {
                hash ^= b;
                hash *= 16777619;
            }
            return hash;
        }

        // MurmurHash3 finalizer: spreads FNV's output so similar keys (round 3 vs 4) don't give similar values
        private static uint Mix(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;
            return hash;
        }
    }
}
