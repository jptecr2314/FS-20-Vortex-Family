using System;
using System.Collections.Generic;

namespace Japensou.NuclearOption.SharedAirframes
{
    internal static class AirframePool
    {
        internal static bool IsFamily(string key) => key == "SmallFighter1" || key == "JP_FS20_A" || key == "JP_FS20_CTOL";
        internal static bool Same(string a, string b) => a != null && b != null && (a == b || IsFamily(a) && IsFamily(b));
        internal static int Count(IEnumerable<int> counts)
        {
            long total = 0;
            foreach (int n in counts)
                total += Math.Max(0, n);
            return (int)Math.Min(int.MaxValue, total);
        }

        // Validate the whole debit before applying any synchronized write.
        internal static int[] PlanDebit(int[] counts, long quantity)
        {
            if (quantity < 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (Count(counts) < quantity)
                throw new InvalidOperationException("Insufficient shared airframes.");
            var debit = new int[counts.Length];
            for (int i = 0; i < counts.Length && quantity > 0; i++)
            {
                debit[i] = (int)Math.Min(Math.Max(0, counts[i]), quantity);
                quantity -= debit[i];
            }

            return debit;
        }

        internal static int[] SelectOwned(string[] keys, bool[] reserved, string requested, long quantity)
        {
            if (quantity < 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (keys.Length != reserved.Length)
                throw new ArgumentException("Ownership flags do not match cells.");
            var indices = new List<int>();
            // Sell/return ordinary cells first; native ownership checks still
            // prevent selling a reserved-only inventory. Flight preserves flags.
            foreach (bool reservation in new[]
            {
                false,
                true
            }

            )
                for (int i = keys.Length - 1; i >= 0 && indices.Count < quantity; i--)
                    if (reserved[i] == reservation && Same(keys[i], requested))
                        indices.Add(i);
            if (indices.Count < quantity)
                throw new InvalidOperationException("Insufficient owned airframes.");
            indices.Sort((a, b) => b.CompareTo(a));
            return indices.ToArray();
        }
    }
}
