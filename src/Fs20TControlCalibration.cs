using System;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TControlCalibration
    {
        internal struct Measurement
        {
            internal float PitchInertia, RollInertia, PitchAuthority, RollAuthority;
        }

        internal static Measurement Measure(Transform root, AircraftParameters parameters)
        {
            var parts = root.GetComponentsInChildren<AeroPart>(true);
            float mass = 0f;
            Vector3 center = Vector3.zero;
            foreach (var part in parts) { mass += part.mass; center += part.mass * MassCenter(part); }
            center /= mass;
            var result = new Measurement();
            foreach (var part in parts)
            {
                Vector3 d = root.InverseTransformDirection(MassCenter(part) - center);
                result.PitchInertia += part.mass * (d.y * d.y + d.z * d.z);
                result.RollInertia += part.mass * (d.x * d.x + d.y * d.y);
                var control = part.GetComponent<ControlSurface>();
                if (control == null) continue;
                var physics = part.GetComponent<Fs20TPartPhysics>();
                var visible = (GameObject)Fs20Tuning.Field(typeof(ControlSurface), "visibleMesh").GetValue(control);
                if (visible == null) continue;
                Vector3 point = physics != null ? physics.WorldForcePoint : MassCenter(part) +
                    visible.transform.rotation * (Vector3)Fs20Tuning.Field(typeof(AeroPart), "centerOfLift").GetValue(part);
                float pitch = Derivative(root, parameters, part, control, visible.transform.rotation, point - center, true);
                float roll = Derivative(root, parameters, part, control, visible.transform.rotation, point - center, false);
                result.PitchAuthority += pitch;
                result.RollAuthority += roll;
            }
            result.PitchAuthority = Mathf.Abs(result.PitchAuthority);
            result.RollAuthority = Mathf.Abs(result.RollAuthority);
            return result;
        }

        private static Vector3 MassCenter(AeroPart part)
        {
            var marker = part.GetComponent<Fs20TPartPhysics>();
            if (marker != null) return marker.WorldMassCenter;
            var center = (Transform)Fs20Tuning.Field(typeof(UnitPart), "centerOfMass").GetValue(part);
            return center == null ? part.transform.position : center.position;
        }

        private static float Derivative(Transform root, AircraftParameters parameters, AeroPart part,
            ControlSurface control, Quaternion neutral, Vector3 arm, bool pitch)
        {
            const float input = .02f;
            var marker = part.GetComponent<Fs20TElevon>();
            float range = (float)Fs20Tuning.Field(typeof(ControlSurface), pitch ? "pitchRange" : "rollRange").GetValue(control);
            float positive = marker == null ? input * range : Fs20TFlightPolicy.ElevonAngle(pitch ? input : 0f, pitch ? 0f : input, marker.Side, marker.Outer);
            float negative = -positive;
            int index = (int)Fs20Tuning.Field(typeof(AeroPart), "airfoil").GetValue(part);
            Vector3 a = Force(root.forward, neutral * Quaternion.AngleAxis(positive, Vector3.right), parameters, index, part.GetWingArea());
            Vector3 b = Force(root.forward, neutral * Quaternion.AngleAxis(negative, Vector3.right), parameters, index, part.GetWingArea());
            Vector3 moment = Vector3.Cross(arm, (a - b) / (2f * input));
            return Vector3.Dot(moment, pitch ? root.right : -root.forward);
        }

        private static Vector3 Force(Vector3 velocity, Quaternion rotation, AircraftParameters parameters, int index, float area)
        {
            Vector3 local = Quaternion.Inverse(rotation) * velocity;
            float alpha = Mathf.Atan2(local.y, local.z);
            float cl = index < 0 ? 1.8f * Mathf.Sin(5f * alpha) : parameters.airfoils[index].liftCoef.Evaluate(alpha);
            return -Vector3.Cross(velocity, rotation * Vector3.right).normalized * cl * area;
        }
    }
}
