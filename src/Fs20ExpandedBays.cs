using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20ExpandedBays
    {
        internal static void Install(Encyclopedia encyclopedia, Transform root, bool carrier)
        {
            var manager = root.GetComponent<Aircraft>().weaponManager;
            var reference = Fs20SkinSurgery.Reference(root);
            foreach (int group in new[] { 1, 2 })
                foreach (var point in manager.hardpointSets[group].hardpoints)
                {
                    var rack = point.transform.gameObject.AddComponent<Fs20LegacyRack>();
                    rack.Group = group; rack.Side = root.InverseTransformPoint(point.transform.position).x < 0 ? -1 : 1;
                    rack.OriginalLocal = point.transform.localPosition;
                    rack.OriginalRotation = point.transform.localRotation;
                }
            Material metal = Fs20HookInterior.NativeMetal(root);
            Material liningMaterial = Fs20InteriorFinish.Material(root);
            Fs20InternalDucts.Apply(root, metal);
            var doors = new List<Fs20SkinSurgery.Door>();
            var bySide = new Dictionary<int, BayDoor[]>();
            string prefix = carrier ? "c" : "a";
            foreach (int side in new[] { -1, 1 })
            {
                var part = root.Find("weaponBay_" + (side < 0 ? "L" : "R"));
                if (part == null || part.GetComponent<AeroPart>() == null) throw new InvalidOperationException("FS-20 weapons bay missing.");
                AddMassAt(root, part.GetComponent<AeroPart>(), Fs20BaySpec.BayMassIncrease, new Vector3(side * .55f, -.93f, -.50f));
                var balance = part.gameObject.AddComponent<Fs20BayMassCenter>();
                balance.Marker = (Transform)Fs20Tuning.Field(typeof(UnitPart), "centerOfMass").GetValue(part.GetComponent<AeroPart>());
                balance.DryCenter = balance.Marker.localPosition;
                balance.PayloadCenter = part.InverseTransformPoint(root.TransformPoint(new Vector3(side * Fs20BaySpec.BombX, Fs20BaySpec.BombY, Fs20BaySpec.BombZ)));
                Reinforce(part.GetComponent<AeroPart>());
                var wall = Fs20InternalMeshes.Create(prefix + "_wall", root, part, liningMaterial, side);
                Fs20HookInterior.RegisterDamageRenderer(part, wall.GetComponent<Renderer>());
                var supports = part.gameObject.AddComponent<Fs20BaySupports>();
                supports.Side = side;supports.Brackets = new MeshFilter[4];
                for (int s = 0; s < 4; s++)
                {
                    var bracket = Fs20InternalMeshes.Create(prefix + "_support_" + s, root, part, metal, side);
                    supports.Brackets[s] = bracket.GetComponent<MeshFilter>();
                    Fs20HookInterior.RegisterDamageRenderer(part, bracket.GetComponent<Renderer>());
                }
                var native = new List<BayDoor>();
                var donor = part.GetComponentsInChildren<BayDoor>(true).FirstOrDefault();
                string key = prefix + "_door_full";
                var data = Fs20InternalMeshes.Get(key);var pivot = data.Pivot;pivot.x *= side;
                var anchor = Fs20SkinSurgery.Attach(root, part, "FS20 continuous bay hinge frame", pivot);
                var hinge = new GameObject("FS20 continuous bay door").transform;hinge.SetParent(anchor, false);
                var bd = hinge.gameObject.AddComponent<BayDoor>();
                Fs20Tuning.Set(bd, "hingeAngle", data.Angle * side);Fs20Tuning.Set(bd, "openSpeed", 5f);Fs20Tuning.Set(bd, "closeSpeed", 5f);
                if (donor != null)
                    foreach (string field in new[] { "openStartSound", "closeStartSound" })
                        Fs20Tuning.Set(bd, field, Fs20Tuning.Field(typeof(BayDoor), field).GetValue(donor));
                var lining = Fs20InternalMeshes.Create(key, root, hinge, liningMaterial, side);
                Fs20HookInterior.RegisterDamageRenderer(part, lining.GetComponent<Renderer>());
                // Native doors are visual children; enclosing a curved
                // leaf in a solid box can break nearby joints at spawn.
                float x0 = carrier ? .22f : .101f;
                doors.Add(new Fs20SkinSurgery.Door { Name = side + "/full", Hinge = hinge, Owner = part, Native = bd,
                    NativeSkinFront = 1.40f,
                    Planes = Fs20SkinSurgery.Box(side < 0 ? -.965f : x0, side < 0 ? -x0 : .965f, Fs20BaySpec.Rear, Fs20BaySpec.Front, -.545f) });
                native.Add(bd);
                bySide.Add(side, native.ToArray());
                foreach (var hp in manager.hardpointSets.SelectMany(h => h.hardpoints).Where(h => h.part == part.GetComponent<UnitPart>()))
                { hp.bayDoors = native.ToArray(); hp.doorOpenDuration = Fs20BaySpec.DoorHold; }
            }
            var branches = new[] { root, root.Find("liftFan"), root.Find("weaponBay_L"), root.Find("weaponBay_R"), root.Find("engine/fuselage_R") };
            Fs20SkinSurgery.Cut(root, branches.Where(p => p != null), doors, true);
            foreach(int side in new[]{-1,1})
                for(int i=4;i<6;i++)
                {
                    var part=root.Find("weaponBay_"+(side<0?"L":"R"));
                    Fs20PanelSkin.Create(root,part,bySide[side][0].transform,prefix+"_skin_"+i,side,reference);
                }
            foreach (var part in new[] { root.Find("weaponBay_L"), root.Find("weaponBay_R") })
                foreach (var old in part.GetComponentsInChildren<BayDoor>(true))
                    if (!bySide.Values.Any(a => a.Contains(old))) RetireDoor(root, old);
            AddMassAt(root, root.GetComponent<AeroPart>(), Fs20BaySpec.DuctMassIncrease, new Vector3(0, -.10f, .60f));
            if (carrier) AddMassAt(root, root.GetComponent<AeroPart>(), Fs20BaySpec.HookWellMass, new Vector3(0, -1.1f, -2.5f));
            SetFuel(root, "", Fs20BaySpec.CentralFuel);
            SetFuel(root, "engine/fuselage_R", Fs20BaySpec.AftFuel);
            float capacity = root.GetComponentsInChildren<FuelTank>(true).Sum(t => t.GetCapacity());
            if (Mathf.Abs(capacity - (carrier ? Fs20BaySpec.FuelC : Fs20BaySpec.FuelA)) > .1f)
                throw new InvalidOperationException("FS-20 repartitioned fuel budget does not balance.");
            if (carrier)
            {
                var state = root.gameObject.AddComponent<Fs20ExpandedBayState>();
                state.Left = bySide[-1];state.Right = bySide[1];
            }
            else Fs20AugerMount.Add(encyclopedia, manager, root, bySide);
        }
        internal static void AddCarrierWeapons(Encyclopedia encyclopedia, Transform root)
        {
            var state = root.GetComponent<Fs20ExpandedBayState>();
            if (state == null) throw new InvalidOperationException("FS-20C bay state missing.");
            Fs20AugerMount.Add(encyclopedia, root.GetComponent<Aircraft>().weaponManager, root,
                new Dictionary<int, BayDoor[]> { { -1, state.Left }, { 1, state.Right } });
        }
        private static void SetFuel(Transform root, string path, float capacity)
        {
            var tank = (path.Length == 0 ? root : root.Find(path)).GetComponent<FuelTank>();
            if (tank == null) throw new InvalidOperationException("FS-20 fuel cell missing: " + path);
            Fs20Tuning.Set(tank, "fuelCapacity", capacity);tank.fuelMass = 0;
        }
        private static void RetireDoor(Transform root, BayDoor door)
        {
            var obsolete = new HashSet<Renderer>(door.GetComponentsInChildren<Renderer>(true));
            foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = group.GetLODs();
                for (int i = 0; i < levels.Length; i++)
                    levels[i].renderers = levels[i].renderers.Where(r => r != null && !obsolete.Contains(r)).ToArray();
                group.SetLODs(levels);
            }
            // The new leaves already contain these panels' native UV skin.
            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }
        private static void Reinforce(AeroPart part)
        {
            var array = (Array)Fs20Tuning.Field(typeof(AeroPart), "joints").GetValue(part);
            for (int i = 0; i < array.Length; i++)
            {
                var joint = array.GetValue(i);
                Fs20Tuning.Set(joint, "breakForce", Fs20BaySpec.BayBreakForce);
                Fs20Tuning.Set(joint, "breakTorque", Fs20BaySpec.BayBreakTorque);
                array.SetValue(joint, i);
            }
        }
        private static void AddMassAt(Transform root, AeroPart part, float mass, Vector3 position)
        {
            var field = Fs20Tuning.Field(typeof(UnitPart), "centerOfMass");
            var previous = (Transform)field.GetValue(part);
            var old = root.InverseTransformPoint(previous == null ? part.transform.position : previous.position);
            var point = (old * part.mass + position * mass) / (part.mass + mass);
            var marker = Fs20SkinSurgery.Attach(root, part.transform, "FS20 revised dry center of mass", point);
            field.SetValue(part, marker);part.mass += mass;
        }
    }

    public sealed class Fs20ExpandedBayState : MonoBehaviour
    {
        [SerializeField] internal BayDoor[] Left, Right;
    }
}
