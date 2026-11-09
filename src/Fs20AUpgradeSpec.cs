using System;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20AUpgradeSpec
    {
        internal const float EngineInstallationSaving = 400f;
        internal const float TankDryMass = 50f;
        internal const float ExtraFuel = 300f;
        internal const float TankOuterRadius = 0.55f;
        internal const float TankInnerRadius = 0.25f;
        internal const float TankHeight = 0.75f;
        internal const float TankSectorDegrees = 300f;
        internal const float TankStartDegrees = -60f;
        internal const float TankCenterX = 0f;
        internal const float TankCenterY = -0.21f;
        internal const float TankCenterZ = 2.316f;
        internal const float ModeledFuelDensity = 800f;
        internal static float TankVolume => (float)Math.PI *
            (TankOuterRadius*TankOuterRadius-TankInnerRadius*TankInnerRadius)*TankHeight*TankSectorDegrees/360f;
    }
}
