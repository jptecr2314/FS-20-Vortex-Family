using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class AuditFs20PaintSurfaces {
 [Serializable] public class Record {public string name,path,role;public bool active,primary;public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;}
 [Serializable] public class Geometry {public string variant;public Record[] meshes;}
 public static void Run(){
  string output=Environment.GetEnvironmentVariable("FS20_PAINT_AUDIT"),basis=Environment.GetEnvironmentVariable("FS20_PAINT_AUDIT_GEOMETRY")??"D:/Artifacts/fs20-own-liveries/2026-11-16";
  Directory.CreateDirectory(output);var bundle=AssetBundle.LoadFromFile(Environment.GetEnvironmentVariable("FS20_PAINT_AUDIT_BUNDLE"));
  var pack=JsonUtility.FromJson<BuildFs20PreparedPaints.Pack>(bundle.LoadAsset<TextAsset>("fs20manifest").text);
  int count=0;
  foreach(string variant in new[]{"A","T","NA"}) {
   var geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText(basis+"/"+(variant=="A"?"geometry-a4":variant=="T"?"geometry":"geometry-na")+"/geometry-"+variant+".json"));
   var root=new GameObject("Paint audit "+variant);var slots=new Dictionary<Renderer,string>();
   int level=int.Parse(Environment.GetEnvironmentVariable("FS20_PAINT_AUDIT_LOD")??"0");
   var selected=new List<Record>();
   foreach(var primary in geometry.meshes.Where(m=>m.active&&m.primary)){
    string slot=primary.name.Substring(primary.name.LastIndexOf('_'));
    var replacement=level==0?null:geometry.meshes.FirstOrDefault(m=>m.active&&!m.primary&&m.path.StartsWith(primary.path+"/FS20T LOD ",StringComparison.Ordinal)&&m.name.EndsWith("LOD"+level+slot,StringComparison.Ordinal));
    selected.Add(replacement??primary);
   }
   foreach(var record in selected){
    var go=new GameObject(record.name);go.transform.SetParent(root.transform);var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.vertices=record.vertices;mesh.normals=record.normals;mesh.uv=record.uv;mesh.triangles=record.triangles;mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;slots.Add(go.AddComponent<MeshRenderer>(),record.role);
   }
   var bounds=slots.Keys.First().bounds;foreach(var rr in slots.Keys)bounds.Encapsulate(rr.bounds);
   foreach(var paint in pack.paints.Where(p=>p.variant==variant)){
    var materials=new List<Material>();foreach(var slot in slots){var mat=new Material(Shader.Find("Unlit/Texture"));mat.mainTexture=bundle.LoadAsset<Texture2D>(slot.Value=="wing"?paint.wing:paint.body);slot.Key.sharedMaterial=mat;materials.Add(mat);}
    foreach(string view in new[]{"top","bottom","oblique"}) {
     var cameraGo=new GameObject("Audit camera");var camera=cameraGo.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.15f,.17f);camera.orthographic=true;camera.aspect=1.4f;
     Vector3 direction=view=="top"?Vector3.up:view=="bottom"?Vector3.down:new Vector3(1,1.4f,1);
     camera.transform.position=bounds.center+direction.normalized*45;camera.transform.rotation=Quaternion.LookRotation(bounds.center-camera.transform.position,view=="oblique"?Vector3.up:Vector3.forward);camera.orthographicSize=Mathf.Max(bounds.extents.z,bounds.extents.x/1.4f)*1.14f;
     var rt=new RenderTexture(1400,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var png=new Texture2D(1400,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1400,1000),0,0);png.Apply();File.WriteAllBytes(output+"/"+paint.id+"-"+view+".png",png.EncodeToPNG());count++;
     RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(png);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraGo);
    }
    foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);
   }
   foreach(var slot in slots)UnityEngine.Object.DestroyImmediate(slot.Key.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(root);
  }
  bundle.Unload(true);File.WriteAllText(output+"/render-summary.json","{\"renders\":"+count+",\"nativeGameShader\":false,\"productionGeometryAndUv\":true}");Debug.Log("FS20_PAINT_AUDIT_RENDER_PASS "+count);
 }
}
