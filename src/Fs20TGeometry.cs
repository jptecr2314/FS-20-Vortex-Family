using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20TGeometry
    {
        internal sealed class Result { internal float AreaRatio, NativeLiftArea, TunedLiftArea, AddedMass, Span; internal int Colliders, IntactColliders, LodGroups, DamageRegions, ElevonHinges; }
        internal static Result Apply(Transform root)
        {
            var asset = Fs20TAsset.Read();
            var result = new Result();
            var liftCenters = new Dictionary<AeroPart, Vector3>();
            var massCenters = new Dictionary<AeroPart, Vector3>();
            foreach (var part in root.GetComponentsInChildren<AeroPart>(true))
            {
                var lift = part.GetLiftTransform() ?? part.transform;
                var offset = (Vector3)Fs20Tuning.Field(typeof(AeroPart), "centerOfLift").GetValue(part);
                var massCenter = (Transform)Fs20Tuning.Field(typeof(UnitPart), "centerOfMass").GetValue(part);
                Vector3 center = massCenter == null ? part.transform.position : massCenter.position;
                massCenters[part] = root.InverseTransformPoint(center);
                liftCenters[part] = root.InverseTransformPoint(center + lift.rotation * offset);
            }
            RepositionWingNodes(root);
            result.ElevonHinges = AlignElevonHinges(root, asset);
            var damageSections = new Fs20TDamageSections(root, asset);
            result.DamageRegions = damageSections.Owners.Count;
            var donors = root.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            var materials = new Dictionary<string, Material>();
            var deltaPaint = new List<Renderer>();
            var hinges = new Dictionary<string, Fs20TSlat>();
            var carriages = new Dictionary<string, Transform>();
            foreach (var h in asset.Hinges)
            {
                var hinge = Require(root, h.Path);
                hinge.position = root.TransformPoint(h.Pivot);
                hinge.rotation = root.rotation * h.Rotation;
                var marker = hinge.gameObject.AddComponent<Fs20TSlat>();
                marker.ClosedPosition = hinge.localPosition;
                marker.ClosedRotation = hinge.localRotation;
                marker.Axis = hinge.parent.InverseTransformDirection(root.TransformDirection(h.Axis)).normalized;
                marker.Stroke = hinge.parent.InverseTransformVector(root.TransformVector(h.Stroke));
                marker.FullAngle = h.Angle;
                var carriage = Child("FS20T carriage " + h.Tag, hinge.parent);
                carriage.position = root.TransformPoint(h.Pivot);
                carriage.rotation = root.rotation;
                marker.Carriage = carriage;
                marker.CarriageClosed = carriage.localPosition;
                marker.CarriageStroke = carriage.parent.InverseTransformVector(root.TransformVector(h.Stroke));
                hinges.Add(h.Tag, marker);
                carriages.Add(h.Tag, carriage);
            }
            var colliderOwners = new HashSet<UnitPart>();
            var collisionBounds = new Dictionary<UnitPart, Bounds>();
            var oldGroups = root.GetComponentsInChildren<LODGroup>(true);
            var legacyLodRenderers = new HashSet<Renderer>(root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.IndexOf("LOD", StringComparison.OrdinalIgnoreCase) >= 0));
            foreach (var p in asset.Pieces)
            {
                damageSections.Owners.TryGetValue(p.Owner, out var section);
                var owner = section != null ? section.Part : Require(root, p.Owner).GetComponent<AeroPart>() ?? throw new InvalidOperationException("Missing FS-20T damage owner: " + p.Owner);
                Transform target;
                if (p.Replace) target = Require(root, p.Path);
                else
                {
                    Transform parent = section != null ? owner.transform : Require(root, p.Parent);
                    if (p.Motion.EndsWith(":hinge", StringComparison.Ordinal)) parent = hinges[p.Motion.Split(':')[0]].transform;
                    if (p.Motion.EndsWith(":carriage", StringComparison.Ordinal)) parent = carriages[p.Motion.Split(':')[0]];
                    target = Child("FS20T " + p.Name, parent);
                    if (section != null) section.States[damageSections.StageByPiece[p.Name]] = target.gameObject;
                    AddDisintegration(owner, target.gameObject);
                }
                var material = p.Materials.Select(key =>
                {
                    if (section != null && key == "aircraft_structure") key = "__metal";
                    if (!materials.TryGetValue(key, out var m)) materials[key] = m = ResolveMaterial(key, donors);
                    return m;
                }).ToArray();
                if (!target.TryGetComponent<MeshFilter>(out var filter)) filter = target.gameObject.AddComponent<MeshFilter>();
                if (!target.TryGetComponent<MeshRenderer>(out var renderer)) renderer = target.gameObject.AddComponent<MeshRenderer>();
                foreach (var g in oldGroups)
                {
                    var oldLods = g.GetLODs();
                    var legacy = legacyLodRenderers.Where(r => r.GetComponentInParent<UnitPart>(true) == owner).ToArray();
                    foreach (var r in legacy) r.gameObject.SetActive(false);
                    bool found = oldLods.Any(l => l.renderers.Contains(renderer) || l.renderers.Any(r => legacy.Contains(r)));
                    if (!found) continue;
                    for (int i = 0; i < oldLods.Length; i++) oldLods[i].renderers = oldLods[i].renderers.Where(r => r != renderer && !legacy.Contains(r)).ToArray();
                    g.SetLODs(oldLods);
                }
                Matrix4x4 map = target.worldToLocalMatrix * root.localToWorldMatrix;
                filter.sharedMesh = p.Lods[0].Create(map);
                renderer.sharedMaterials = material;
                renderer.enabled = true;
                var lodRoot = Child("FS20T LOD " + p.Name, target);
                var lodRenderers = new Renderer[3];
                lodRenderers[0] = renderer;
                for (int i = 1; i < 3; i++)
                {
                    var child = Child("FS20T " + p.Name + " LOD" + i, lodRoot);
                    child.gameObject.AddComponent<MeshFilter>().sharedMesh = p.Lods[i].Create(child.worldToLocalMatrix * root.localToWorldMatrix);
                    var r = child.gameObject.AddComponent<MeshRenderer>();
                    r.sharedMaterials = material;
                    lodRenderers[i] = r;
                }
                var group = lodRoot.gameObject.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.SetLODs(new[] { new LOD(.12f, new[] { lodRenderers[0] }), new LOD(.045f, new[] { lodRenderers[1] }), new LOD(.006f, new[] { lodRenderers[2] }) });
                group.RecalculateBounds();
                result.LodGroups++;
                if (p.Materials[0] == Fs20TDeltaPaint.MaterialKey) deltaPaint.AddRange(lodRenderers);
                var damage = (DamageMaterial)Fs20Tuning.Field(typeof(UnitPart), "damageMaterial").GetValue(owner);
                if (damage != null && (p.Materials[0] == "SmallFighter1_skin" || p.Materials[0] == Fs20TDeltaPaint.MaterialKey))
                {
                    foreach (var r in lodRenderers)
                        if (!damage.renderers.Contains(r)) damage.renderers.Add(r);
                    var indices = damage.indices ?? new byte[0];
                    Array.Resize(ref indices, damage.renderers.Count);
                    damage.indices = indices;
                }
                AddDisintegration(owner, lodRoot.gameObject);
                if (p.Hulls.Length != 0)
                {
                    if (colliderOwners.Add(owner))
                        foreach (var collider in owner.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                    foreach (var hull in p.Hulls)
                    {
                        var collision = Child("FS20T collision " + p.Name, target);
                        Vector3 cellCenter = Vector3.zero;
                        foreach (var v in hull.Vertices) cellCenter += v;
                        collision.position = root.TransformPoint(cellCenter / hull.Vertices.Length);
                        var mesh = hull.Create(collision.worldToLocalMatrix * root.localToWorldMatrix);
                        collision.gameObject.AddComponent<Fs20TDamageProxy>().Owner = owner;
                        var collider = collision.gameObject.AddComponent<MeshCollider>();
                        collider.convex = true;
                        collider.sharedMesh = mesh;
                        if (collider.sharedMesh == null) throw new InvalidOperationException("FS-20T collider failed: " + p.Name);
                        AddDisintegration(owner, collision.gameObject);
                        Matrix4x4 toOwner = owner.transform.worldToLocalMatrix * root.localToWorldMatrix;
                        foreach (var v in hull.Vertices)
                        {
                            Vector3 point = toOwner.MultiplyPoint3x4(v);
                            if (!collisionBounds.TryGetValue(owner, out var b)) b = new Bounds(point, Vector3.zero);
                            b.Encapsulate(point);
                            collisionBounds[owner] = b;
                        }
                        result.Colliders++;
                        if (section == null || damageSections.StageByPiece[p.Name] == 0) result.IntactColliders++;
                    }
                }
                foreach (var vertex in p.Lods[0].Vertices) result.Span = Mathf.Max(result.Span, 2f * Mathf.Abs(vertex.x));
            }
            root.gameObject.AddComponent<Fs20TDeltaPaint>().Bind(deltaPaint.ToArray());
            float beforeArea = 0, afterArea = 0;
            foreach (var a in asset.Areas)
            {
                var part = Require(root, a.Path).GetComponent<AeroPart>();
                if (part == null || a.Before <= .001f || a.After <= .001f) throw new InvalidOperationException("Invalid FS-20T lift section: " + a.Path);
                float ratio = a.After / a.Before;
                beforeArea += a.Before;
                afterArea += a.After;
                float stockArea = part.GetWingArea();
                part.SetWingArea(stockArea * ratio);
                result.NativeLiftArea += stockArea;
                result.TunedLiftArea += part.GetWingArea();
                float growth = Mathf.Max(0f, ratio - 1f);
                part.dragArea *= 1f + growth * Fs20TFlightPolicy.AddedParasiteDragFraction;
                float added = part.mass * growth * Fs20TFlightPolicy.AddedStructureFraction;
                part.mass += added;
                Fs20Tuning.Set(part, "baseMass", part.mass);
                result.AddedMass += added;
                var delta = a.AfterCenter - a.BeforeCenter;
                bool splitExtension = damageSections.Budgets.Contains(part);
                liftCenters[part] = Fs20Geometry.Widen(liftCenters[part]);
                if (!splitExtension) liftCenters[part] += delta;
                var center = Fs20Geometry.Widen(massCenters[part]);
                if (!splitExtension && added > 0f) center += delta * (added / part.mass);
                massCenters[part] = center;
                float leverage = Mathf.Max(1f, Mathf.Abs(a.AfterCenter.x) / Mathf.Max(.1f, Mathf.Abs(a.BeforeCenter.x)));
                foreach (var joint in part.Joints)
                {
                    joint.breakForce *= Mathf.Max(1f, ratio);
                    joint.breakTorque *= Mathf.Max(1f, ratio * leverage);
                }
            }
            result.AreaRatio = afterArea / beforeArea;
            damageSections.Allocate(root);
            foreach (var part in liftCenters.Keys)
                Fs20TPartPhysics.Configure(part, root.TransformPoint(massCenters[part]), root.TransformPoint(liftCenters[part]));
            foreach (var a in asset.Areas)
            {
                var part = Require(root, a.Path).GetComponent<AeroPart>();
                var device = part.GetComponent<HighLiftDevice>();
                if (device == null) continue;
                var sections = damageSections.Owners.Values.Where(s => s.Budget == part).Select(s => s.Part.GetComponent<Fs20TDamageState>()).ToArray();
                part.gameObject.AddComponent<Fs20THighLift>().Configure(device, part, a.HighLiftRatio, sections,
                    part.GetComponentsInChildren<Fs20TSlat>(true));
            }
            foreach (var part in colliderOwners)
            {
                // Collider.bounds is empty on this inactive template. Derive
                // bounds from the exported hulls before native Awake caches it.
                var bounds = collisionBounds[part];
                part.GetComponent<Fs20TPartPhysics>().StoreCollisionBounds(bounds);
            }
            Fs20TNativeContacts.Configure(root);
            return result;
        }
        private static void RepositionWingNodes(Transform root)
        {
            var branches = Fs20Geometry.WingPaths.Select(path => Require(root, path)).ToArray();
            var nodes = branches.SelectMany(t => t.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            var positions = nodes.ToDictionary(t => t, t => root.InverseTransformPoint(t.position));
            foreach (var node in nodes)
            {
                Transform pylon = node;
                while (pylon != null && pylon != root && !pylon.name.StartsWith("pylon_", StringComparison.Ordinal)) pylon = pylon.parent;
                Vector3 original = positions[node];
                Vector3 target = Fs20Geometry.Widen(original);
                if (pylon != null && pylon != root && positions.TryGetValue(pylon, out var anchor)) target = original + Fs20Geometry.Widen(anchor) - anchor;
                node.position = root.TransformPoint(target);
            }
        }

        private static int AlignElevonHinges(Transform root, Fs20TAsset asset)
        {
            var visibleMesh = Fs20Tuning.Field(typeof(ControlSurface), "visibleMesh");
            var liftNormal = Fs20Tuning.Field(typeof(AeroPart), "liftNormal");
            var attachedSurface = Fs20Tuning.Field(typeof(ControlSurface), "attachedSurface");
            int count = 0;
            foreach (var piece in asset.Pieces)
            {
                string name = piece.Name;
                bool outer = name == "aileron_L_visible" || name == "aileron_R_visible";
                if (!outer && name != "elevator_L_visible" && name != "elevator_R_visible")
                    continue;

                var owner = root.Find(piece.Owner);
                var visible = root.Find(piece.Path);
                var control = owner == null ? null : owner.GetComponent<ControlSurface>();
                var part = owner == null ? null : owner.GetComponent<AeroPart>();
                if (control == null || part == null || visible == null || !visible.IsChildOf(owner))
                {
                    VortexConventionalPlugin.ReportAssetState("FS-20T elevon alignment skipped: " + name);
                    continue;
                }

                float side = name.EndsWith("_R_visible", StringComparison.Ordinal) ? 1f : -1f;
                // Bind the actual replacement transform before the native jobs cache it.
                visibleMesh.SetValue(control, visible.gameObject);
                liftNormal.SetValue(part, visible);
                if ((UnitPart)attachedSurface.GetValue(control) == null)
                    attachedSurface.SetValue(control, part);
                // Root-space fit to the leading edge of the exported elevon.
                // The mesh is localized to this pose below, so its neutral outline is preserved.
                Vector3 pivot = outer ? new Vector3(side * 4.59819f, -.20575f, -3.65258f)
                    : new Vector3(side * 2.82405f, -.17959f, -3.76081f);
                Vector3 axis = new Vector3(1f, side * (outer ? -.01489f : -.01857f), side * .06089f);
                Vector3 forward = Vector3.Cross(axis, Vector3.up);
                visible.SetPositionAndRotation(root.TransformPoint(pivot),
                    root.rotation * Quaternion.LookRotation(forward, Vector3.Cross(forward, axis)));
                var elevon = control.gameObject.AddComponent<Fs20TElevon>();
                elevon.Side = side;
                elevon.Outer = outer;
                count++;
            }
            return count;
        }
        private static Transform Require(Transform root, string path) => root.Find(path) ?? throw new InvalidOperationException("FS-20T native path missing: " + path);
        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return child.transform;
        }
        private static void AddDisintegration(UnitPart owner, GameObject child)
        {
            var field = Fs20Tuning.Field(typeof(UnitPart), "disintegrateObjects");
            var old = (GameObject[])field.GetValue(owner) ?? new GameObject[0];
            field.SetValue(owner, old.Concat(new[] { child }).Distinct().ToArray());
        }
        private static Material ResolveMaterial(string key, Material[] donors)
        {
            var native = donors.FirstOrDefault(m => m.name == key || m.name.StartsWith(key + " ", StringComparison.Ordinal));
            if (native != null) return native;
            if (key != Fs20TDeltaPaint.MaterialKey && key != "__deltaEdge" && key != "__unfinished" && key != "__metal")
                throw new InvalidOperationException("FS-20T native material missing: " + key);
            // Use the installed Lit shader without the aircraft atlas or damage maps.
            var lit = donors.FirstOrDefault(m => m.shader != null && m.shader.name == "Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("FS-20T Lit shader donor missing.");
            if (key == Fs20TDeltaPaint.MaterialKey || key == "__deltaEdge")
            {
                var skinDonor = donors.First(m => m.name.StartsWith("SmallFighter1_skin", StringComparison.Ordinal));
                return Fs20TDeltaPaint.NativeMaterial(skinDonor);
            }
            bool skin = key == "__unfinished";
            var result = new Material(lit.shader) { name = skin ? "FS20T unfinished skin" : "FS20T structural metal" };
            result.SetColor("_BaseColor", skin ? new Color(.24f, .34f, .4f) : new Color(.28f, .29f, .3f));
            result.SetFloat("_Metallic", skin ? .05f : .45f);
            result.SetFloat("_Smoothness", skin ? .28f : .42f);
            return result;
        }
    }

    public sealed class Fs20TSlat : MonoBehaviour
    {
        [SerializeField] internal Vector3 ClosedPosition, Axis, Stroke, CarriageClosed, CarriageStroke;
        [SerializeField] internal Quaternion ClosedRotation;
        [SerializeField] internal float FullAngle;
        [SerializeField] internal Transform Carriage;
        internal void Deploy(float fraction)
        {
            float u = Mathf.Clamp01(fraction);
            transform.localPosition = ClosedPosition + Stroke * u;
            transform.localRotation = Quaternion.AngleAxis(FullAngle * u, Axis) * ClosedRotation;
            if (Carriage != null) Carriage.localPosition = CarriageClosed + CarriageStroke * u;
        }
    }
}
