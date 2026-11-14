using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Local clean-airframe planning calibration, 2026-11-14. Reference data and
    // native guidance receipts: D:/Artifacts/fs20-ai-calibration/2026-11-14/.
    // Landing speeds are native maxWeight references, not touchdown speeds at
    // arbitrary mass. Native AI applies sqrt(currentMass / maxWeight) itself.
    internal static class Fs20AiCalibration
    {
        internal static void Apply(string variant, AircraftParameters parameters)
        {
            float takeoff, landing, approach, corner, radius, cruise;
            bool vtol;
            switch (variant)
            {
                case "A": takeoff=110; landing=110; approach=125; corner=140; radius=550; cruise=.4f; vtol=false; break;
                case "C": takeoff=95; landing=95; approach=110; corner=120; radius=400; cruise=.4f; vtol=false; break;
                case "T": takeoff=0; landing=85; approach=100; corner=100; radius=300; cruise=.45f; vtol=true; break;
                case "NA": takeoff=85; landing=85; approach=100; corner=100; radius=300; cruise=.4f; vtol=false; break;
                case "B": return; // Stock native VTOL is the unchanged control reference.
                default: throw new ArgumentOutOfRangeException(nameof(variant));
            }
            if(parameters==null || parameters.verticalLanding!=vtol)
                throw new InvalidOperationException("FS-20 AI profile does not match the aircraft landing mode.");
            if(!vtol) {
                parameters.takeoffSpeed=takeoff;
            }
            // Do not convert a dry-thrust ground-roll estimate into takeoffDistance.
            // Native consumers use this metadata for recovery/base selection, not
            // for the takeoff state's rotation logic. Preserve the caller's policy.
            // Preserve the T's native low-speed VTOL takeoff/hover thresholds.
            parameters.landingSpeed=landing;
            parameters.shortLandingSpeed=landing;
            parameters.approachSpeed=approach;
            parameters.cornerSpeed=corner;
            parameters.turningRadius=radius;
            parameters.cruiseThrottle=cruise;
            // FBW gains, G ceiling, hover PIDs, minimum terrain altitude, stores,
            // collision avoidance and native combat/weapon decisions stay native.
        }
    }
}
