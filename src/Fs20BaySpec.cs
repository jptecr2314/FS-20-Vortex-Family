using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Simulation design budget. Native breakable joints and fuel consumption
    // remain active; these values are not a structural certification.
    internal static class Fs20BaySpec
    {
        internal const float BayMassIncrease = 85f;
        internal const float DuctMassIncrease = 30f;
        internal const float AirframeMassIncrease = 2f * BayMassIncrease + DuctMassIncrease;
        internal const float HookWellMass = 20f;
        internal const float HookMass = 100f;
        internal const float BayBreakForce = 350000f;
        internal const float BayBreakTorque = 300000f;
        internal const float HookBreakForce = 900000f;
        internal const float DoorHold = 1.2f;
        internal const float RailDelay = .20f;
        internal const float Rear = -2.869060f;
        internal const float Front = 1.874577f;
        internal const float BombX = .48f, BombY = -.96f, BombZ = -.50f, BombRoll = 30f;
        internal const float HookX = 0f, HookY = -1.20f, HookZ = -1.90f;
        internal const float HookScale = .65f;
        internal const float HookStowedAngle = 188f;
        internal const float HookDeployedAngle = 110f;
        internal const float HookDoorDrop = 0f;
        internal const float HookDoorAngle = 80f;
        // Bounded side pockets retain wing-root capacities. Repartition 190 kg
        // from the affected aft cell into the central-side cells; total stays.
        internal const float AftFuel = 110f;
        internal const float CentralFuel = 490f;
        internal const float FuelC = 2800f;
        internal const float FuelA = 3100f;
        internal static float BayUltimateLoad(float payloadKg, float g) =>
            (400f + BayMassIncrease + payloadKg) * 9.80665f * g * 1.5f;
        internal static float ArrestingPeak(float massKg, float speed, float distance) =>
            .75f * massKg * speed * speed / distance;
        internal static float LoadedCoordinate(float dry, float payload, float totalMass, float payloadMass) =>
            totalMass <= 0 ? dry : (dry * (totalMass - payloadMass) + payload * payloadMass) / totalMass;
    }
}
