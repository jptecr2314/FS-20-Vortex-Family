using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20LegacyRack : MonoBehaviour
    {
        [SerializeField] internal Vector3 OriginalLocal;
        [SerializeField] internal Quaternion OriginalRotation;
        [SerializeField] internal int Group, Side;
        internal bool SharedBay, OutwardRail;

        internal static Vector3 Offset(int group, string key)
        {
            if (key == "AAM2_single_internal") return group == 2 ? new Vector3(-.02f, 0, -.10f) : new Vector3(.02f, 0, 0);
            if (group != 1) return Vector3.zero;
            if (key == "AAM1_double_internal") return new Vector3(0, 0, .20f);
            if (key == "AAM3_triple_internal_compact") return new Vector3(0, -.02f, 0);
            if (key == "AGM_heavy_internal") return new Vector3(0, 0, .10f);
            return Vector3.zero;
        }
        internal static Vector3 PairedOffset(string key)
        {
            if (key == "AAM2_single_internal") return new Vector3(-.07f, -.06f, .40f);
            if (key == "AAM3_single_internal") return new Vector3(.02f, 0, 0);
            if (key == "AAM1_single_internal") return new Vector3(.08f, -.10f, .40f);
            if (key == "bomb_glide1_double_inernal") return new Vector3(0, .05f, .40f);
            return Offset(2, key);
        }
        internal static float PairedRoll(string key) => key == "AAM2_single_internal" ? 30f : key == "AAM1_single_internal" ? 300f : 0f;

        [HarmonyPatch(typeof(Hardpoint), nameof(Hardpoint.SpawnMount))]
        internal static class Spawn
        {
            private static void Prefix(Hardpoint __instance, Aircraft __0, ref WeaponMount __1)
            {
                var point = __instance.transform;
                if (point == null) return;
                var rack = point.GetComponent<Fs20LegacyRack>();
                if (rack == null) return;
                if (__0 == null) return;
                if (rack.Group == 1 && Fs20AugerMount.IsAuger(__1 == null ? null : __1.jsonKey))
                {
                    __1 = Fs20AugerMount.ForSide(rack.Side);
                    point.position = __0.transform.TransformPoint(new Vector3(rack.Side * Fs20BaySpec.BombX, Fs20BaySpec.BombY, Fs20BaySpec.BombZ));
                    point.rotation = __0.transform.rotation * Quaternion.AngleAxis(rack.Side * Fs20BaySpec.BombRoll, Vector3.forward);
                    return;
                }
                var offset = Offset(rack.Group, __1 == null ? null : __1.jsonKey);
                bool auger = __0.loadout != null && __0.loadout.weapons.Count > 1 && Fs20AugerMount.IsAuger(__0.loadout.weapons[1]?.jsonKey);
                string key = __1 == null ? null : __1.jsonKey;
                rack.SharedBay = auger && rack.Group == 2;
                rack.OutwardRail = rack.SharedBay && key == "AAM1_single_internal";
                if (rack.SharedBay) offset = PairedOffset(key);
                offset.x *= rack.Side;
                // Convert the aircraft-space adjustment into the rack parent's
                // local axes. Always start from the original saved position.
                point.localPosition = rack.OriginalLocal + point.parent.InverseTransformVector(__0.transform.TransformVector(offset));
                point.localRotation = rack.OriginalRotation;
                if (rack.SharedBay && PairedRoll(key) != 0)
                    point.rotation = __0.transform.rotation * Quaternion.AngleAxis(rack.Side * PairedRoll(key), Vector3.forward);
            }

            private static void Postfix(Hardpoint __instance, GameObject __result)
            {
                if (__result == null || __instance.transform == null || __instance.transform.GetComponent<Fs20LegacyRack>() == null) return;
                var rack = __instance.transform.GetComponent<Fs20LegacyRack>();
                foreach (var missile in __result.GetComponentsInChildren<MountedMissile>(true))
                {
                    // Change this aircraft's spawned rack only, so the new
                    // leaves finish opening before any native store moves.
                    var delay = Fs20Tuning.Field(typeof(MountedMissile), "railDelay");
                    delay.SetValue(missile, Mathf.Max(Fs20BaySpec.RailDelay, (float)delay.GetValue(missile)));
                    Fs20Tuning.Set(missile, "doorOpenDuration", Fs20BaySpec.DoorHold);
                    if (rack.SharedBay)
                    {
                        float length = Mathf.Max(1.5f, (float)Fs20Tuning.Field(typeof(MountedMissile), "railLength").GetValue(missile));
                        float speed = (float)Fs20Tuning.Field(typeof(MountedMissile), "railSpeed").GetValue(missile);
                        Fs20Tuning.Set(missile, "railLength", length);
                        Fs20Tuning.Set(missile, "doorOpenDuration", Mathf.Max(Fs20BaySpec.DoorHold, Fs20BaySpec.RailDelay + length / Mathf.Max(.1f, speed) + .3f));
                        if (rack.OutwardRail)
                        {
                            var direction = Fs20Tuning.Field(typeof(MountedMissile), "railDirection");
                            direction.SetValue(missile, System.Enum.ToObject(direction.FieldType, rack.Side > 0 ? 2 : 3));
                        }
                    }
                }
            }
        }
    }
}
