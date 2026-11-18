using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional {
 // The native distant shader reads vertex RGB, not _Color/_BaseColor.
 // Cache a color-only mesh per original/paint; never edit donor/collider meshes.
 internal static class Fs20PaintLods {
  static readonly Dictionary<Fs20PreparedLiveries.Paint,Dictionary<Mesh,Mesh>> meshes=new Dictionary<Fs20PreparedLiveries.Paint,Dictionary<Mesh,Mesh>>();
  static readonly Dictionary<Mesh,Mesh> readable=new Dictionary<Mesh,Mesh>();
  static readonly HashSet<Mesh> unprepared=new HashSet<Mesh>();
  internal static bool Native(Material material)=>material!=null&&material.shader!=null&&material.shader.name=="Shader Graphs/LOD";
  internal static bool Painted(MeshRenderer renderer){
   if(!Array.Exists(renderer.sharedMaterials,Native))return false;
   for(var t=renderer.transform;t!=null;t=t.parent){string name=t.name;
    if(name=="nozzle"||name.StartsWith("nozzle_",StringComparison.Ordinal)||name.StartsWith("swivelDuct",StringComparison.Ordinal)||name.StartsWith("gearBay_",StringComparison.Ordinal))return false;
    if(t.GetComponent<Aircraft>()!=null)break;
   }return true;
  }
  // Native far meshes can be GPU-only. Prepare their tiny immutable copies
  // while building definitions, never read GPU buffers during paint selection.
  internal static void Prepare(GameObject prefab){
   MatchDistantExterior(prefab);
   foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true)){
   if(!Painted(renderer))continue;var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
   var source=filter.sharedMesh;if(!source.isReadable){if(!readable.TryGetValue(source,out var copy)){copy=Fs20Geometry.ReadableCopy(source);readable.Add(source,copy);}source=copy;filter.sharedMesh=source;}
   var binding=filter.GetComponent<Fs20LodPaintBinding>()??filter.gameObject.AddComponent<Fs20LodPaintBinding>();binding.Original=source;
  }}
  private static void MatchDistantExterior(GameObject prefab)
  {
   var groups = prefab.GetComponentsInChildren<LODGroup>(true);
   var distantMaterial = prefab.GetComponentsInChildren<MeshRenderer>(true)
    .Where(Painted).SelectMany(renderer => renderer.sharedMaterials).FirstOrDefault(Native);
   if (distantMaterial == null) return;

   foreach (var group in groups)
   {
    // Authored groups retain textured LOD0 and LOD1. Use the same
    // low-detail finish as the retained fuselage on the final level.
    if (!group.name.StartsWith("FS20T LOD ", StringComparison.Ordinal)) continue;
    var levels = group.GetLODs();
    if (levels.Length != 3) continue;
    foreach (var renderer in levels[2].renderers.OfType<MeshRenderer>())
    {
     var slots = renderer.sharedMaterials;
     bool changed = false;
     for (int i = 0; i < slots.Length; i++)
     {
      var material = slots[i];
      if (!Fs20PreparedLiveries.WingMaterial(material) && !Fs20PreparedLiveries.BodyMaterial(material)) continue;
      slots[i] = distantMaterial;
      changed = true;
     }
     if (changed) renderer.sharedMaterials = slots;
    }
   }
  }
  internal static void Apply(MeshRenderer renderer,Fs20PreparedLiveries.Paint paint){
   var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)return;
   var binding=filter.GetComponent<Fs20LodPaintBinding>()??filter.gameObject.AddComponent<Fs20LodPaintBinding>();
   if(binding.Original==null)binding.Original=filter.sharedMesh;
   var source=binding.Original;
   if(!meshes.TryGetValue(paint,out var variants))meshes.Add(paint,variants=new Dictionary<Mesh,Mesh>());
   if(!variants.TryGetValue(source,out var colored)||colored==null){
    if(!source.isReadable){if(unprepared.Add(source))VortexConventionalPlugin.ReportAssetState("FS-20 distant mesh kept its native finish (unprepared): "+source.name);return;}
    var colors=source.colors32;int count=source.vertexCount;if(colors.Length!=count){colors=new Color32[count];for(int i=0;i<count;i++)colors[i]=new Color32(255,255,255,255);}
    var selected=new bool[count];var materials=renderer.sharedMaterials;
    for(int sub=0;sub<source.subMeshCount&&sub<materials.Length;sub++)if(Native(materials[sub]))foreach(int index in source.GetTriangles(sub))selected[index]=true;
    float mean=0;int samples=0;for(int i=0;i<count;i++)if(selected[i]){mean+=colors[i].r+colors[i].g+colors[i].b;samples++;}mean=samples==0?1:Mathf.Max(1,mean/samples);
    Color tint=((Color)paint.Tint).linear;
    for(int i=0;i<count;i++)if(selected[i]){var c=colors[i];float shade=(c.r+c.g+c.b)/mean;var painted=(Color32)(new Color(tint.r*shade,tint.g*shade,tint.b*shade,1));painted.a=c.a;colors[i]=painted;}
    colored=UnityEngine.Object.Instantiate(source);colored.name="FS20 distant "+paint.id+" "+source.name;colored.colors32=colors;variants[source]=colored;
   }
   filter.sharedMesh=colored;
  }
  internal static void Release(Fs20PreparedLiveries.Paint paint){if(!meshes.TryGetValue(paint,out var variants))return;foreach(var mesh in variants.Values)if(mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}meshes.Remove(paint);}
  internal static int Count{get{int count=0;foreach(var variants in meshes.Values)foreach(var mesh in variants.Values)if(mesh!=null)count++;return count;}}
 }
 public sealed class Fs20LodPaintBinding:MonoBehaviour {[SerializeField] internal Mesh Original;}
}
