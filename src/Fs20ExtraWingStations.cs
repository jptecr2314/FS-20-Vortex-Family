using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using NuclearOption.SavedMission;

namespace Japensou.NuclearOption.VortexConventional
{
    // Native Hardpoint/WeaponMount stations. No independent force or firing model.
    public sealed class Fs20ExtraWingStations : MonoBehaviour
    {
        [SerializeField] internal string Variant;
        [SerializeField] internal int OriginalGroupCount;
        [Serializable] private sealed class Layouts { public Layout[] variants; }
        [Serializable] private sealed class Layout { public string variant; public float loadedWidth; public Pylon[] pylons; public Pylon[] reposition; public Tip[] tips; public LightMove[] nav; }
        [Serializable] private sealed class Pylon { public string owner; public Vector3 anchor; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Tip { public string owner; public Vector3 anchor; public float roll; }
        [Serializable] private sealed class LightMove { public string path; public Vector3 delta; }
        private static Layouts layouts;
        private static Encyclopedia stationCatalogue;
        private static readonly MethodInfo Clone = AccessTools.Method(typeof(object), "MemberwiseClone");

        internal static void Install(Encyclopedia encyclopedia, Transform root, string variant)
        {
            if (root.GetComponent<Fs20ExtraWingStations>() != null) return;
            if (variant != "A" && variant != "C" && variant != "T") throw new ArgumentException(nameof(variant));
            if (layouts == null)
                using (var stream = typeof(Fs20ExtraWingStations).Assembly.GetManifestResourceStream("VortexConventional.Fs20ExtraWingStations.json"))
                using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing wing-station layout")))
                    layouts = Newtonsoft.Json.JsonConvert.DeserializeObject<Layouts>(reader.ReadToEnd());
            stationCatalogue = encyclopedia;
            var layout = layouts.variants.Single(x => x.variant == variant);
            var aircraft = root.GetComponent<Aircraft>();
            var manager = aircraft.weaponManager;
            var marker = root.gameObject.AddComponent<Fs20ExtraWingStations>();
            marker.Variant = variant; marker.OriginalGroupCount = manager.hardpointSets.Length;
            int next = manager.hardpointSets.SelectMany(s => s.hardpoints).Max(p => p.HardpointIndex) + 1;
            var inner = manager.hardpointSets.Single(s => s.name == "Inner wing pylons");
            var outer = manager.hardpointSets.Single(s => s.name == "Outer wing pylons");
            var middle = new HardpointSet { name = "Middle wing pylons", precludingHardpointSets = new List<byte>(),
                hardpoints = new List<Hardpoint>(), weaponOptions = new List<WeaponMount> { null } };
            // Share the native inner list, including third-party additions. Refresh
            // the reference at native selection/spawn boundaries if a mod replaces it.
            middle.weaponOptions = inner.weaponOptions;
            foreach (var spec in layout.pylons)
            {
                var template = inner.hardpoints.First(p => Mathf.Sign(root.InverseTransformPoint(p.transform.position).x) == Mathf.Sign(spec.anchor.x));
                var owner = Require(root, spec.owner).GetComponent<AeroPart>();
                if (owner == null || template.Pylon == null) throw new InvalidOperationException("Native pylon owner/template missing");
                var go = new GameObject("FS20 additional middle pylon " + (spec.anchor.x < 0 ? "L" : "R"));
                go.transform.SetParent(owner.transform, false);go.transform.position = root.TransformPoint(spec.anchor);go.transform.rotation = root.rotation;
                var source = template.Pylon.GetComponent<MeshFilter>();
                go.AddComponent<MeshFilter>().sharedMesh = FittedMesh(root, source, template.transform, spec);
                var renderer = go.AddComponent<MeshRenderer>();renderer.sharedMaterials = template.Pylon.sharedMaterials;
                RegisterPaintAndDamage(owner, renderer);
                foreach(var group in root.GetComponentsInChildren<LODGroup>(true))
                {
                    var levels=group.GetLODs();bool changed=false;
                    for(int i=0;i<levels.Length;i++)if(levels[i].renderers.Contains(template.Pylon)){levels[i].renderers=levels[i].renderers.Concat(new[]{renderer}).ToArray();changed=true;}
                    if(changed){group.SetLODs(levels);group.RecalculateBounds();}
                }
                var mountPoint = new GameObject("Native mount").transform;mountPoint.SetParent(go.transform,false);
                mountPoint.rotation = template.transform.rotation;
                var point = CopyPoint(template, mountPoint, owner, next++, renderer);
                middle.hardpoints.Add(point);
            }
            if (middle.hardpoints.Count != 0) manager.hardpointSets = manager.hardpointSets.Concat(new[] { middle }).ToArray();
            // Retain the native inner station, weapon options, index and load-bearing
            // owner. Only refit its support and attachment datum to open usable room.
            if (layout.reposition != null)
                foreach (var spec in layout.reposition)
                {
                    var point = inner.hardpoints.Single(p => Mathf.Sign(root.InverseTransformPoint(p.transform.position).x) == Mathf.Sign(spec.anchor.x));
                    var renderer = point.Pylon;
                    if (renderer == null || point.part != Require(root, spec.owner).GetComponent<AeroPart>())
                        throw new InvalidOperationException("Original inner pylon owner changed");
                    var rotation = point.transform.rotation;
                    var mesh = FittedMesh(root, renderer.GetComponent<MeshFilter>(), point.transform, spec);
                    renderer.transform.position = root.TransformPoint(spec.anchor);
                    renderer.transform.rotation = root.rotation;
                    renderer.transform.localScale = Vector3.one;
                    renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
                    point.transform.position = root.TransformPoint(spec.anchor);
                    point.transform.rotation = rotation;
                    RegisterPaintAndDamage(point.part, renderer);
                    foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
                        if (group.GetLODs().Any(level => level.renderers.Contains(renderer))) group.RecalculateBounds();
                }
            if (layout.tips != null && layout.tips.Length != 0)
            {
                var donor = encyclopedia.aircraft.FirstOrDefault(d => d != null && d.jsonKey == "Fighter1");
                var source = donor == null ? null : donor.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets.FirstOrDefault(s => s.name == "Wingtip Pylons");
                if (source == null) throw new InvalidOperationException("Native FS-12 wingtip station unavailable");
                Fs20WingtipAlignment.Configure(encyclopedia.weaponMounts.FirstOrDefault(m => m != null && m.jsonKey == "AAM2_single"));
                var tips = new HardpointSet { name = "Wingtip rails", precludingHardpointSets = new List<byte>(),
                    hardpoints = new List<Hardpoint>(), weaponOptions = new List<WeaponMount> { null } };
                Fs20WingtipOptions.Synchronize(tips, encyclopedia.weaponMounts, source.weaponOptions, AirborneOptions(encyclopedia, manager));
                foreach (var spec in layout.tips)
                {
                    var owner = Require(root, spec.owner).GetComponent<AeroPart>();
                    var anchor = new GameObject("FS20" + variant + " wingtip rail " + (spec.anchor.x < 0 ? "L" : "R")).transform;
                    anchor.SetParent(owner.transform,false);anchor.position=root.TransformPoint(spec.anchor);anchor.rotation=root.rotation*Quaternion.Euler(0,0,spec.roll);
                    var template=source.hardpoints.First(p=>Mathf.Sign(donor.unitPrefab.transform.InverseTransformPoint(p.transform.position).x)==Mathf.Sign(spec.anchor.x));
                    tips.hardpoints.Add(CopyPoint(template,anchor,owner,next++,null));
                }
                manager.hardpointSets = manager.hardpointSets.Concat(new[] { tips }).ToArray();
                foreach (var move in layout.nav)
                {
                    var light = Require(root, move.path);
                    light.position += root.TransformVector(move.delta);
                }
                // Native mounted AAM fins reach about +/-6.13 m. Reserve the
                // loaded footprint for spawn/parking spacing, without scaling wings.
                ((AircraftDefinition)aircraft.definition).width = Mathf.Max(((AircraftDefinition)aircraft.definition).width, layout.loadedWidth > 0f ? layout.loadedWidth : 12.4f);
            }
        }
        private static void RegisterPaintAndDamage(UnitPart owner, Renderer renderer)
        {
            // UnitPart.SetLivery walks DamageMaterial.renderers, not arbitrary
            // descendants. Merely copying sharedMaterials leaves a new support
            // outside native paint selection and structural damage updates.
            var damage = (DamageMaterial)Fs20Tuning.Field(typeof(UnitPart), "damageMaterial").GetValue(owner);
            if (damage == null) throw new InvalidOperationException("Pylon paint/damage owner missing");
            if (!damage.renderers.Contains(renderer)) damage.renderers.Add(renderer);
            var indices = damage.indices ?? Array.Empty<byte>();
            Array.Resize(ref indices, damage.renderers.Count);
            damage.indices = indices;
        }
        private static Transform Require(Transform root,string path) => root.Find(path) ?? throw new InvalidOperationException("Wing station owner missing: "+path);

        private static Hardpoint CopyPoint(Hardpoint source, Transform transform, AeroPart owner, int index, MeshRenderer renderer)
        {
            var point=(Hardpoint)Clone.Invoke(source,null);
            point.transform=transform;point.part=owner;point.HardpointIndex=index;point.Pylon=renderer;
            point.bayDoors=Array.Empty<BayDoor>();point.BuiltInWeapons=Array.Empty<Weapon>();point.BuiltInTurrets=Array.Empty<Turret>();
            foreach(string name in new[]{"mount","spawnedPrefab"}) Fs20Tuning.Field(typeof(Hardpoint),name).SetValue(point,null);
            var field=Fs20Tuning.Field(typeof(Hardpoint),"pylonOptions");var options=(Array)field.GetValue(source);var copy=Array.CreateInstance(field.FieldType.GetElementType(),renderer==null?0:options.Length);
            for(int i=0;i<copy.Length;i++) {var option=Clone.Invoke(options.GetValue(i),null);Fs20Tuning.Field(option.GetType(),"renderer").SetValue(option,renderer);copy.SetValue(option,i);}
            field.SetValue(point,copy);return point;
        }

        private static Mesh FittedMesh(Transform root, MeshFilter source, Transform oldAnchor, Pylon spec)
        {
            var mesh=Fs20Geometry.ReadableCopy(source.sharedMesh);var old=mesh.vertices;
            var oldWorld=old.Select(v=>root.InverseTransformPoint(source.transform.TransformPoint(v))).ToArray();
            var offset=root.InverseTransformPoint(oldAnchor.position);var mapped=spec.vertices.Select(v=>v-spec.anchor).ToArray();
            // Preserve original native UV records; new rim vertices interpolate
            // from the nearest native face at the same longitudinal/lateral point.
            var nativeTriangles=mesh.triangles;var weights=new Vector3[mapped.Length];var ids=new int[mapped.Length];
            for(int i=old.Length;i<mapped.Length;i++)
            {
                var q=spec.vertices[i]-spec.anchor+offset;float best=float.PositiveInfinity;int selected=-1;Vector3 bary=Vector3.zero;
                for(int j=0;j<nativeTriangles.Length;j+=3)
                {
                    var a=oldWorld[nativeTriangles[j]];var b=oldWorld[nativeTriangles[j+1]];var c=oldWorld[nativeTriangles[j+2]];
                    float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<1e-9f)continue;
                    float u=((b.z-c.z)*(q.x-c.x)+(c.x-b.x)*(q.z-c.z))/det;
                    float v=((c.z-a.z)*(q.x-c.x)+(a.x-c.x)*(q.z-c.z))/det;float w=1-u-v;
                    if(Mathf.Min(u,Mathf.Min(v,w))<-.001f)continue;
                    float distance=Mathf.Abs(q.y-(u*a.y+v*b.y+w*c.y));if(distance<best){best=distance;selected=j;bary=new Vector3(u,v,w);}
                }
                if(selected<0)throw new InvalidOperationException("Cannot transfer native pylon UV");ids[i]=selected;weights[i]=bary;
            }
            var channels=new List<Vector4>[8];
            for(int channel=0;channel<8;channel++){channels[channel]=new List<Vector4>();mesh.GetUVs(channel,channels[channel]);}
            var colors=mesh.colors;
            var triangles=(int[])spec.triangles.Clone();
            if((root.worldToLocalMatrix*source.transform.localToWorldMatrix).determinant<0f)
                for(int i=0;i<triangles.Length;i+=3){int t=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=t;}
            mesh.Clear();mesh.name="FS20 fitted native middle pylon";mesh.vertices=mapped;mesh.triangles=triangles;
            for(int channel=0;channel<8;channel++)
            {
                var values=channels[channel];if(values.Count!=old.Length)continue;
                for(int i=old.Length;i<mapped.Length;i++){int j=ids[i];var w=weights[i];values.Add(w.x*values[nativeTriangles[j]]+w.y*values[nativeTriangles[j+1]]+w.z*values[nativeTriangles[j+2]]);}
                mesh.SetUVs(channel,values);
            }
            if(colors.Length==old.Length){var values=new List<Color>(colors);for(int i=old.Length;i<mapped.Length;i++){int j=ids[i];var w=weights[i];values.Add(w.x*colors[nativeTriangles[j]]+w.y*colors[nativeTriangles[j+1]]+w.z*colors[nativeTriangles[j+2]]);}mesh.colors=values.ToArray();}
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }

        internal static void SyncOptionLists(HardpointSet[] groups)
        {
            if (groups == null) return;
            var inner = groups.FirstOrDefault(group => group != null && group.name == "Inner wing pylons");
            var middle = groups.FirstOrDefault(group => group != null && group.name == "Middle wing pylons");
            if (inner != null && middle != null) middle.weaponOptions = inner.weaponOptions;
        }
        internal static void SyncOptions(WeaponManager manager)
        {
            if (manager == null || manager.GetComponentInParent<Fs20ExtraWingStations>(true) == null) return;
            var aircraft = manager.GetComponentInParent<Aircraft>(true);
            string key = aircraft == null || aircraft.definition == null ? null : aircraft.definition.jsonKey;
            if (key != "JP_FS20_A" && key != "JP_FS20_CTOL" && key != "JP_FS20_T") return;
            if (key == "JP_FS20_CTOL" || key == "JP_FS20_T") SyncOptionLists(manager.hardpointSets);
            var tips = manager.hardpointSets.FirstOrDefault(group => group != null && group.name == "Wingtip rails");
            if (tips == null) return;
            var donor = stationCatalogue == null ? null : stationCatalogue.aircraft.FirstOrDefault(definition => definition != null && definition.jsonKey == "Fighter1");
            var source = donor == null || donor.unitPrefab == null ? null : donor.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets.FirstOrDefault(group => group.name == "Wingtip Pylons");
            IEnumerable<WeaponMount> registry = Encyclopedia.WeaponLookup != null ? (IEnumerable<WeaponMount>)Encyclopedia.WeaponLookup.Values : stationCatalogue == null ? null : stationCatalogue.weaponMounts;
            Fs20WingtipOptions.Synchronize(tips, registry, source == null ? null : source.weaponOptions, AirborneOptions(stationCatalogue, manager));
        }
        internal static IEnumerable<WeaponMount> AirborneOptions(Encyclopedia catalogue, WeaponManager current)
        {
            var managers = new HashSet<WeaponManager>();
            if (current != null) managers.Add(current);
            IEnumerable<AircraftDefinition> definitions = catalogue == null ? Enumerable.Empty<AircraftDefinition>() : catalogue.aircraft;
            if (Encyclopedia.Lookup != null) definitions = definitions.Concat(Encyclopedia.Lookup.Values.OfType<AircraftDefinition>());
            foreach (var definition in definitions)
            {
                var aircraft = definition == null || definition.unitPrefab == null ? null : definition.unitPrefab.GetComponent<Aircraft>();
                if (aircraft != null && aircraft.weaponManager != null) managers.Add(aircraft.weaponManager);
            }
            foreach (var manager in managers)
            {
                if (manager.hardpointSets == null) continue;
                bool ours = manager.GetComponentInParent<Fs20ExtraWingStations>(true) != null;
                foreach (var group in manager.hardpointSets)
                {
                    if (group == null || group.weaponOptions == null || (ours && group.name == "Wingtip rails")) continue;
                    if (group.hardpoints == null || group.hardpoints.Count == 0 || group.hardpoints.Any(p => p != null && p.bayDoors != null && p.bayDoors.Length != 0)) continue;
                    foreach (var mount in group.weaponOptions) if (mount != null) yield return mount;
                }
            }
        }
        internal static void Normalize(WeaponManager manager, Loadout loadout)
        {
            if(manager==null||loadout==null||manager.GetComponentInParent<Fs20ExtraWingStations>(true)==null)return;
            SyncOptions(manager);
            while(loadout.weapons.Count<manager.hardpointSets.Length)loadout.weapons.Add(null);
            for (int i = 0; i < manager.hardpointSets.Length; i++)
            {
                var group = manager.hardpointSets[i];
                if (group != null && group.name == "Wingtip rails" && !group.weaponOptions.Contains(loadout.weapons[i])) loadout.weapons[i] = null;
            }
        }
        private static int DisplayRank(string name)
        {
            switch (name)
            {
                case "Internal Cannon": return 0;
                case "Weapon Bays (inner)": return 10;
                case "Weapon Bays (outer)": return 20;
                case "Inner wing pylons": return 30;
                case "Middle wing pylons": return 40;
                case "Outer wing pylons": return 50;
                case "Wingtip rails": return 60;
                case "Internal Tail Hook": return 100;
                default: return 80;
            }
        }
        [HarmonyPatch(typeof(LoadoutSelector), "ShowHardpoints")]
        internal static class DisplayOrder
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void Prefix(Aircraft ___aircraft)
            {
                if (___aircraft != null) SyncOptions(___aircraft.weaponManager);
            }
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(Aircraft ___aircraft, List<WeaponSelector> ___weaponSelectors)
            {
                if (___aircraft == null || ___aircraft.weaponManager == null || ___aircraft.definition == null) return;
                string key = ___aircraft.definition.jsonKey;
                if (key != "JP_FS20_CTOL" && key != "JP_FS20_T") return;
                var groups = ___aircraft.weaponManager.hardpointSets;
                if (___weaponSelectors == null || ___weaponSelectors.Count != groups.Length || groups.Length == 0) return;
                var parent = ___weaponSelectors[0].transform.parent;
                if (___weaponSelectors.Any(view => view == null || view.transform.parent != parent)) return;
                var slots = ___weaponSelectors.Select(view => view.transform.GetSiblingIndex()).OrderBy(i => i).ToArray();
                var views = ___weaponSelectors.Select((view, index) => new { View = view, Rank = DisplayRank(groups[index].name), Index = index })
                    .OrderBy(row => row.Rank).ThenBy(row => row.Index).ToArray();
                // The native selector list and hardpointSets must remain in their
                // saved/network order. Only move the visual Transform siblings.
                for (int i = 0; i < views.Length; i++) views[i].View.transform.SetSiblingIndex(slots[i]);
            }
        }
        [HarmonyPatch(typeof(WeaponSelector), "Initialize", new[] { typeof(Aircraft), typeof(HardpointSet), typeof(FactionHQ), typeof(Airbase) })]
        internal static class SelectorOptions
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void Prefix(Aircraft __0)
            {
                if (__0 != null) SyncOptions(__0.weaponManager);
            }
        }
        internal static bool IsManagedWingtipGroup(HardpointSet group)
        {
            if (group == null || group.name != "Wingtip rails" || group.hardpoints == null) return false;
            foreach (var point in group.hardpoints)
            {
                var aircraft = point == null || point.transform == null ? null : point.transform.GetComponentInParent<Aircraft>(true);
                if (aircraft == null || aircraft.definition == null || aircraft.weaponManager == null || aircraft.GetComponent<Fs20ExtraWingStations>() == null) continue;
                string key = aircraft.definition.jsonKey;
                if ((key == "JP_FS20_A" || key == "JP_FS20_CTOL") && aircraft.weaponManager.hardpointSets.Any(candidate => ReferenceEquals(candidate, group))) return true;
            }
            return false;
        }
        internal static void SyncWingtipGroup(HardpointSet group)
        {
            if (!IsManagedWingtipGroup(group)) return;
            foreach (var point in group.hardpoints)
            {
                if (point == null || point.transform == null) continue;
                var aircraft = point.transform.GetComponentInParent<Aircraft>(true);
                if (aircraft == null || aircraft.weaponManager == null || !aircraft.weaponManager.hardpointSets.Any(candidate => ReferenceEquals(candidate, group))) continue;
                SyncOptions(aircraft.weaponManager); return;
            }
        }
        // Shared by live selectors, mission-editor selectors and native AI choices.
        [HarmonyPatch(typeof(WeaponChecker), "GetAvailableWeaponsNonAlloc", new[] { typeof(global::NuclearOption.Networking.Player), typeof(HardpointSet), typeof(Airbase), typeof(FactionHQ), typeof(bool), typeof(List<WeaponMount>) })]
        internal static class WingtipChoices
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void Prefix(HardpointSet __1) => SyncWingtipGroup(__1);
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(HardpointSet __1, List<WeaponMount> __5)
            {
                if (IsManagedWingtipGroup(__1)) Fs20WingtipOptions.DeduplicateAvailable(__5, __1.weaponMount);
            }
        }
        // Native PopulateOptions appends its tuple list. Clear only our rail menu,
        // retaining the original widget, availability checks and option creation.
        [HarmonyPatch(typeof(WeaponSelector), "PopulateOptions", new[] { typeof(Airbase), typeof(FactionHQ), typeof(HardpointSet), typeof(bool) })]
        internal static class WingtipMenu
        {
            [HarmonyPrefix]
            private static void Prefix(HardpointSet __2, IList ___dropdownOptions)
            {
                if (IsManagedWingtipGroup(__2)) ___dropdownOptions.Clear();
            }
        }
        // An old saved alias remains a valid definition, but is represented by the
        // one equivalent option surviving native availability filtering in this UI.
        [HarmonyPatch(typeof(WeaponSelector), "SetValue", new[] { typeof(WeaponMount) })]
        internal static class WingtipSetValue
        {
            [HarmonyPrefix]
            private static void Prefix(ref WeaponMount __0, HardpointSet ___hardpointSet, List<WeaponMount> ___getCache)
            {
                if (IsManagedWingtipGroup(___hardpointSet)) __0 = Fs20WingtipOptions.ResolveVisible(__0, ___getCache);
            }
        }
        // Remote loadouts are checked against the server template, which may never
        // have opened a local loadout menu. Refresh before the original membership test.
        [HarmonyPatch(typeof(WeaponChecker), "MountAllowedHardpoint", new[] { typeof(WeaponMount), typeof(HardpointSet) })]
        internal static class WingtipValidation
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void Prefix(HardpointSet __1) => SyncWingtipGroup(__1);
        }
        [HarmonyPatch(typeof(WeaponManager),nameof(WeaponManager.SpawnWeapons))]
        internal static class Spawn
        {
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(WeaponManager __instance){SyncOptions(__instance);var a=__instance.GetComponentInParent<Aircraft>(true);Normalize(__instance,a==null?null:a.loadout);}
        }
        [HarmonyPatch(typeof(SavedLoadout),nameof(SavedLoadout.CreateLoadout),new[]{typeof(WeaponManager)})]
        internal static class Saved
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void Prefix(WeaponManager __0) => SyncOptions(__0);
            private static void Postfix(WeaponManager __0,Loadout __result)=>Normalize(__0,__result);
        }
    }
}
