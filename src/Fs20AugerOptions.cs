using System;
using System.Linq;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20AugerOptions
    {
        // -1 denotes the one canonical paired carrier appended to the inner
        // group. Other weapon entries retain their order, including Empty.
        internal static int[] Keep(string[] keys, bool[] sameInfo, bool inner)
        {
            if (keys.Length != sameInfo.Length) throw new ArgumentException("Option info count mismatch.");
            var kept = Enumerable.Range(0, keys.Length).Where(i => !sameInfo[i] && !Fs20AugerMount.IsAuger(keys[i]));
            return (inner ? kept.Concat(new[] { -1 }) : kept).ToArray();
        }
    }
}
