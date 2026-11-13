using System;
using System.Collections.Generic;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TDamageGate
    {
        [ThreadStatic] private static Stack<HashSet<object>> fragments, shocks;
        internal static void Begin(bool shock)
        {
            if (shock) { if (shocks == null) shocks = new Stack<HashSet<object>>(); shocks.Push(new HashSet<object>()); }
            else { if (fragments == null) fragments = new Stack<HashSet<object>>(); fragments.Push(new HashSet<object>()); }
        }
        internal static void End(bool shock)
        {
            var stack = shock ? shocks : fragments;
            if (stack != null && stack.Count != 0) stack.Pop();
        }
        internal static bool Allow(object owner, bool shock)
        {
            var stack = shock ? shocks : fragments;
            return stack == null || stack.Count == 0 || stack.Peek().Add(owner);
        }
    }
}
