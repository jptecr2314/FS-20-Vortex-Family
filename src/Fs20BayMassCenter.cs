using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20BayMassCenter : MonoBehaviour
    {
        [SerializeField] internal Transform Marker;
        [SerializeField] internal Vector3 DryCenter, PayloadCenter;
        private static readonly FieldInfo Fired = AccessTools.Field(typeof(MountedMissile), "fired");
        private static readonly FieldInfo Mount = Fs20Tuning.Field(typeof(Hardpoint), "mount");
        private static readonly FieldInfo Spawned = Fs20Tuning.Field(typeof(Hardpoint), "spawnedPrefab");
        private AeroPart part;
        private WeaponManager manager;
        private MountedMissile missile;
        private float previousLoad = -1f;
        private void Awake()
        {
            part = GetComponent<AeroPart>();
            var aircraft = GetComponentInParent<Aircraft>();
            manager = aircraft == null ? null : aircraft.weaponManager;
            if (manager != null) manager.OnWeaponsLoaded += Bind;
        }
        private void Start() => Bind();
        private void Bind()
        {
            missile = null;
            if (manager != null)
                foreach (var hp in manager.hardpointSets.SelectMany(s => s.hardpoints))
                {
                    if (hp.part != part) continue;
                    var mount = (WeaponMount)Mount.GetValue(hp);var prefab = (GameObject)Spawned.GetValue(hp);
                    if (mount != null && mount.jsonKey.StartsWith("JP_FS20_INTERNAL_AUGER_") && prefab != null)
                        missile = prefab.GetComponentInChildren<MountedMissile>();
                }
            Refresh();
        }
        private void FixedUpdate() => Refresh();
        private void Refresh()
        {
            if (part == null || Marker == null || part.mass <= 0) return;
            float load = missile != null && !(bool)Fired.GetValue(missile) ? 1000f : 0f;
            // The native weapon system owns mass addition/removal. Only move
            // its bay's mass center to the actual stored Auger position.
            load = Mathf.Min(load, Mathf.Max(0, part.mass - 1));
            Marker.localPosition = new Vector3(
                Fs20BaySpec.LoadedCoordinate(DryCenter.x, PayloadCenter.x, part.mass, load),
                Fs20BaySpec.LoadedCoordinate(DryCenter.y, PayloadCenter.y, part.mass, load),
                Fs20BaySpec.LoadedCoordinate(DryCenter.z, PayloadCenter.z, part.mass, load));
            if (part.rb != null && part.rb.transform == part.transform)
            {
                part.rb.centerOfMass = Marker.localPosition;
                if (load != previousLoad) part.rb.ResetInertiaTensor();
            }
            previousLoad = load;
        }
        private void OnDestroy() { if (manager != null) manager.OnWeaponsLoaded -= Bind; }
    }
}
