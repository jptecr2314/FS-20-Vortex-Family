namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20NALiveries
    {
        internal static void Configure(AircraftParameters parameters) => Fs20PreparedLiveries.Configure("NA",parameters);
        internal static LiveryData Preset(Fs20NANightScheme scheme) => Fs20PreparedLiveries.Scheme("NA",(int)scheme).Data;
    }
}
