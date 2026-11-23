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
        [Serializable] private sealed class Layout { public string variant; public Pylon[] pylons; public Tip[] tips; public LightMove[] nav; }
        [Serializable] private sealed class Pylon { public string owner; public Vector3 anchor; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Tip { public string owner; public Vector3 anchor; public float roll; }
        [Serializable] private sealed class LightMove { public string path; public Vector3 delta; }
        private static Layouts layouts;
        private static readonly MethodInfo Clone = AccessTools.Method(typeof(object), "MemberwiseClone");

        internal static void Install(Encyclopedia encyclopedia, Transform root, string variant)
        {
            if (root.GetComponent<Fs20ExtraWingStations>() != null) return;
            if (variant != "C" && variant != "T") throw new ArgumentException(nameof(variant));
            if (layouts == null)
                using (var stream = typeof(Fs20ExtraWingStations).Assembly.GetManifestResourceStream("VortexConventional.Fs20ExtraWingStations.json"))
                using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing wing-station layout")))
                    layouts = Newtonsoft.Json.JsonConvert.DeserializeObject<Layouts>(reader.ReadToEnd());
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
            var compact = new HashSet<string>(new[]{"AAM1_single","AAM2_single","AAM4_single","bomb_250_single","bomb_500_single"});
            // Single native carriers keep the extra station compact. Fit of each
            // loaded combination is a separate validation from station creation.
            foreach (var mount in outer.weaponOptions)
                if (mount != null && mount.ammo == 1 && mount.IsAllowed(false) && compact.Contains(mount.jsonKey)) middle.weaponOptions.Add(mount);
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
            manager.hardpointSets = manager.hardpointSets.Concat(new[] { middle }).ToArray();
            if (layout.tips != null && layout.tips.Length != 0)
            {
                var donor = encyclopedia.aircraft.FirstOrDefault(d => d != null && d.jsonKey == "Fighter1");
                var source = donor == null ? null : donor.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets.FirstOrDefault(s => s.name == "Wingtip Pylons");
                if (source == null) throw new InvalidOperationException("Native FS-12 wingtip station unavailable");
                var tips = new HardpointSet { name = "Wingtip rails", precludingHardpointSets = new List<byte>(),
                    hardpoints = new List<Hardpoint>(), weaponOptions = new List<WeaponMount> { null } };
                foreach (var mount in source.weaponOptions)
                    if (mount != null && mount.ammo == 1 && mount.IsAllowed(false) && mount.jsonKey.StartsWith("AAM",StringComparison.Ordinal)) tips.weaponOptions.Add(mount);
                foreach (var spec in layout.tips)
                {
                    var owner = Require(root, spec.owner).GetComponent<AeroPart>();
                    var anchor = new GameObject("FS20C wingtip rail " + (spec.anchor.x < 0 ? "L" : "R")).transform;
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
                ((AircraftDefinition)aircraft.definition).width = Mathf.Max(((AircraftDefinition)aircraft.definition).width, 12.4f);
            }
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

        internal static void Normalize(WeaponManager manager, Loadout loadout)
        {
            if(manager==null||loadout==null||manager.GetComponentInParent<Fs20ExtraWingStations>(true)==null)return;
            while(loadout.weapons.Count<manager.hardpointSets.Length)loadout.weapons.Add(null);
        }
        [HarmonyPatch(typeof(WeaponManager),nameof(WeaponManager.SpawnWeapons))]
        internal static class Spawn
        {
            private static void Prefix(WeaponManager __instance){var a=__instance.GetComponentInParent<Aircraft>(true);Normalize(__instance,a==null?null:a.loadout);}
        }
        [HarmonyPatch(typeof(SavedLoadout),nameof(SavedLoadout.CreateLoadout),new[]{typeof(WeaponManager)})]
        internal static class Saved
        {
            private static void Postfix(WeaponManager __0,Loadout __result)=>Normalize(__0,__result);
        }
    }
}
