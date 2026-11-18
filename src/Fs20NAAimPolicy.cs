using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Pure command shaping. Inputs are measured rates and the existing native
    // gun steering point; no forces, authority limits or PID state are changed.
    internal sealed class Fs20NAAimPolicy
    {
        internal struct Frame
        {
            internal float Weight, PitchErrorRate, YawErrorRate;
            internal float PitchFeedforward, YawFeedforward, PitchDamping, YawDamping, ThrottleCeiling;
            internal float Angle, ClosingSpeed;
        }

        private bool sampled;
        private float weight, pitchRate, yawRate, previousTargetPitch, previousTargetYaw;
        internal Frame Last { get; private set; }

        internal void Reset()
        {
            sampled = false;
            weight = pitchRate = yawRate = previousTargetPitch = previousTargetYaw = 0f;
            Last = new Frame { ThrottleCeiling = 1f };
        }

        internal Frame Step(float angle, float bankError, float rollRate,
            float targetPitchRate, float targetYawRate, float bodyPitchRate,
            float bodyYawRate, float coordinatedYaw, float fullPitchRate,
            float range, float closingSpeed, float equivalentSpeed, float height,
            float dt, bool jumped)
        {
            if (!Finite(angle) || !Finite(bankError) || !Finite(rollRate) || !Finite(targetPitchRate) || !Finite(targetYawRate) ||
                !Finite(bodyPitchRate) || !Finite(bodyYawRate) || !Finite(coordinatedYaw) || !Finite(fullPitchRate) || !Finite(range) ||
                !Finite(closingSpeed) || !Finite(equivalentSpeed) || !Finite(height) || !Finite(dt) || dt <= 0f || dt > .1f || range < 100f)
            {
                Reset();
                return Last;
            }

            float acceleration = sampled ? Math.Max(Math.Abs(targetPitchRate - previousTargetPitch), Math.Abs(targetYawRate - previousTargetYaw)) / dt : 0f;
            previousTargetPitch = targetPitchRate; previousTargetYaw = targetYawRate;
            bool release = jumped || angle >= 12f || Math.Abs(bankError)>=40f || Math.Abs(rollRate)>=1.1f || acceleration > 2f;
            if (!sampled || release)
            {
                pitchRate = targetPitchRate;
                yawRate = targetYawRate;
            }
            else
            {
                float filter = dt / (.08f + dt);
                pitchRate += (targetPitchRate - pitchRate) * filter;
                yawRate += (targetYawRate - yawRate) * filter;
            }
            sampled = true;

            float near = 1f - Smooth(3f, 10f, angle);
            float bank = (1f - Smooth(15f, 40f, Math.Abs(bankError))) *
                         (1f - Smooth(.45f, 1.1f, Math.Abs(rollRate)));
            float energy = Smooth(120f, 180f, equivalentSpeed) * Smooth(150f, 300f, height);
            float targetWeight = near * bank * energy;
            if (release) weight = 0f;
            else weight += (targetWeight - weight) * dt / (targetWeight < weight ? .03f + dt : .12f + dt);

            // Existing hardware interprets pitch as a normalized rate request and
            // NA yaw as coordinated rate + .65 rad/s * command. Feed forward only
            // the required target motion; brake excess motion relative to it.
            const float toDegrees = 57.2957795f;
            float pitchFeed = Clamp(pitchRate / Math.Max(.05f, fullPitchRate), -.3f, .3f);
            float yawFeed = Clamp((yawRate - coordinatedYaw) / .65f, -.3f, .3f);

            // This is a ceiling on the native throttle request, never a command
            // to accelerate. Low energy, opening range and distant pursuit retain
            // the native request. Native break-off/collision decisions stay intact.
            float approach = closingSpeed > 20f ? 1f - Smooth(4f, 10f, range / closingSpeed) : 0f;
            float throttle = release ? 1f : 1f - .65f * approach * (1f - Smooth(800f, 1600f, range)) *
                             (1f - Smooth(10f, 25f, angle)) * Smooth(190f, 230f, equivalentSpeed) * bank;
            Last = new Frame
            {
                Weight = weight,
                PitchErrorRate = (pitchRate - bodyPitchRate) * toDegrees,
                YawErrorRate = (yawRate - bodyYawRate) * toDegrees,
                PitchFeedforward = pitchFeed,
                YawFeedforward = yawFeed,
                PitchDamping = .25f / Math.Max(.05f, fullPitchRate) / toDegrees,
                YawDamping = .25f / .65f / toDegrees,
                ThrottleCeiling = throttle,
                Angle = angle,
                ClosingSpeed = closingSpeed
            };
            return Last;
        }

        internal static float Refine(float nativeScaled, float nativeDerivative,
            float derivativeCoefficient, float relativeErrorRate, float feedforward, float weight, float minimumDerivative = 0f)
        {
            if (weight <= 0f || !Finite(nativeScaled) || !Finite(nativeDerivative) || !Finite(derivativeCoefficient) || !Finite(relativeErrorRate) || !Finite(feedforward) || !Finite(weight) || !Finite(minimumDerivative))
                return nativeScaled;
            float replacement = relativeErrorRate * Math.Max(derivativeCoefficient, minimumDerivative);
            float correction = Clamp(replacement - nativeDerivative + feedforward, -.35f, .35f);
            return nativeScaled + Clamp(weight, 0f, 1f) * correction;
        }

        private static float Smooth(float lo, float hi, float value)
        {
            float x = Clamp((value - lo) / (hi - lo), 0f, 1f);
            return x * x * (3f - 2f * x);
        }
        private static float Clamp(float value, float lo, float hi) => Math.Max(lo, Math.Min(hi, value));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
