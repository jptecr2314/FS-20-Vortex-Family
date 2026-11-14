using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Native value units are millions of in-game dollars. Keep these in the
    // shared definition used by native UI and server purchase/sale commands.
    internal static class Fs20VariantPricing
    {
        internal static float Price(string key)
        {
            switch(key) {
                case "JP_FS20_A": return 75f;
                case "SmallFighter1": return 90f;
                case "JP_FS20_CTOL": return 105f;
                case "JP_FS20_T": return 125f;
                case "JP_FS20_NA": return 150f;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }
        internal static void Apply(AircraftDefinition definition)
        {
            if(definition==null)throw new ArgumentNullException(nameof(definition));
            definition.value=Price(definition.jsonKey);
        }
    }
}
