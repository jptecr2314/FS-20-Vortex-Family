using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace FS20StreamingVerification {
 [BepInPlugin("jp.fs20.streaming-bench","FS20 private streaming verification","0.1.0")]
 public sealed class StreamingBench:BaseUnityPlugin {
  const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  string output,label;Camera camera;Aircraft target;Spawner spawner;Airbase airbase;Type cache;MethodInfo snapshot,collect;long previous;
  readonly List<double> frames=new List<double>();readonly List<object> measurements=new List<object>();readonly List<Aircraft> owned=new List<Aircraft>();int assertions;RenderTexture workload;
  [StructLayout(LayoutKind.Sequential)] struct Counters{public uint cb,faults;public UIntPtr peakWorking,working,peakPaged,paged,peakNonpaged,nonpaged,pagefile,peakPagefile,privateBytes;}
  [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
  [DllImport("psapi.dll")] static extern bool GetProcessMemoryInfo(IntPtr process,out Counters counters,uint size);
  ulong WorkingSet(){Counters c;return GetProcessMemoryInfo(GetCurrentProcess(),out c,(uint)Marshal.SizeOf(typeof(Counters)))?c.working.ToUInt64():0;}
  void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
  object Cache()=>snapshot==null?null:snapshot.Invoke(null,null);
  static bool Coat(Texture t)=>t!=null&&(t.name.StartsWith("a.")||t.name.StartsWith("t.")||t.name.StartsWith("na.")||t.name.StartsWith("example.creator.")||t.name=="fs20wingbasis"||t.name.StartsWith("FS20 CTOL paint a."));
  object Memory(){var textures=Resources.FindObjectsOfTypeAll<Texture2D>().Where(Coat).ToArray();return new{preparedTextureCount=textures.Length,preparedTextureBytes=textures.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)),managedBytes=GC.GetTotalMemory(false),processWorkingSet=WorkingSet(),graphicsDriverBytes=UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver(),cache=Cache(),textures=textures.Select(t=>new{name=t.name,width=t.width,height=t.height,format=t.format.ToString(),readable=t.isReadable,id=t.GetInstanceID()}).ToArray()};}
  void LateUpdate(){if(target&&camera){var offset=new Vector3(9,12,13);camera.transform.SetPositionAndRotation(target.transform.position+offset,Quaternion.LookRotation(-offset));if(workload!=null)RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=workload});}long now=Stopwatch.GetTimestamp();if(label!=null&&previous!=0)frames.Add((now-previous)*1000d/Stopwatch.Frequency);previous=now;}
  void Begin(string phase){label=phase;frames.Clear();previous=Stopwatch.GetTimestamp();}
  void End(double operationMs){var sorted=frames.OrderBy(x=>x).ToArray();measurements.Add(new{phase=label,operationMs,frames=sorted.Length,maxFrameMs=sorted.Length==0?0:sorted.Last(),p99FrameMs=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.99)],over50ms=sorted.Count(x=>x>50),over100ms=sorted.Count(x=>x>100),memory=Memory()});label=null;Save(false,null);}
  void Save(bool completed,string error)=>File.WriteAllText(Path.Combine(output,"streaming.json"),JsonConvert.SerializeObject(new{completed,error,assertions,nativeGame=true,graphics=SystemInfo.graphicsDeviceType.ToString(),batchMode=Application.isBatchMode,product=Application.productName,installedGame=false,weather="mission weatherIntensity=1; cloudAltitude=1800; native environment",measurements},Formatting.Indented));
  IEnumerator Start(){output=Environment.GetEnvironmentVariable("FS20_STREAM_OUTPUT");if(string.IsNullOrEmpty(output)){enabled=false;yield break;}Directory.CreateDirectory(output);Application.runInBackground=true;var stack=new Stack<IEnumerator>();stack.Push(Run());while(stack.Count>0){bool next=false;object current=null;Exception error=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){error=e;}if(error!=null){Save(false,error.ToString());Application.Quit(1);yield break;}if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(current is IEnumerator nested)stack.Push(nested);else yield return current;}Save(true,null);Application.Quit(0);}
  Aircraft Spawn(string variant){string key=variant=="B"?"SmallFighter1":variant=="C"?"JP_FS20_CTOL":"JP_FS20_"+variant;var definition=(AircraftDefinition)Encyclopedia.Lookup[key];var load=new NuclearOption.SavedMission.Loadout();foreach(var station in definition.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets)load.weapons.Add(null);var plane=spawner.SpawnAircraft(null,definition.unitPrefab,load,.6f,new LiveryKey(0),airbase.transform.GlobalPosition()+new Vector3(1200+owned.Count*100,500,1000),Quaternion.identity,Vector3.forward*180,null,airbase.CurrentHQ,"FS20 streaming test",1,.5f);Check(plane!=null,"Native spawn failed");foreach(var part in plane.GetAllParts())foreach(var rb in part.GetComponentsInChildren<Rigidbody>(true))rb.isKinematic=true;owned.Add(plane);target=plane;return plane;}
  IEnumerator WaitPaint(Aircraft plane){yield return null;float until=Time.realtimeSinceStartup+30;var livery=plane.GetComponent<LiveryBehaviour>();while(livery!=null&&livery.IsLoading&&Time.realtimeSinceStartup<until)yield return null;Check(livery!=null&&!livery.IsLoading,"Native paint load timed out");yield return null;}
  Texture2D PreparedTexture(Component root){var block=new MaterialPropertyBlock();foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++){var m=slots[i];if(!m)continue;renderer.GetPropertyBlock(block,i);var texture=block.GetTexture("_Livery")??(m.HasProperty("_Livery")?m.GetTexture("_Livery"):null);if(Coat(texture))return texture as Texture2D;block.Clear();}}return null;}
  void ValidateLods(Aircraft plane){
   var type=AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20LodPaintBinding");if(type==null)return;int changed=0;
   foreach(var part in plane.GetAllParts())foreach(var binding in part.GetComponentsInChildren(type,true)){
    var filter=binding.GetComponent<MeshFilter>();var original=(Mesh)type.GetField("Original",Flags).GetValue(binding);var mesh=filter.sharedMesh;if(!mesh.name.StartsWith("FS20 distant "))continue;changed++;
    Check(original!=mesh&&original.vertices.SequenceEqual(mesh.vertices)&&original.normals.SequenceEqual(mesh.normals)&&original.uv.SequenceEqual(mesh.uv)&&original.triangles.SequenceEqual(mesh.triangles)&&original.bounds==mesh.bounds,"LOD recolor changed geometry/normal/UV/bounds");
    var before=original.colors32;var after=mesh.colors32;Check(before.Length==after.Length&&before.Select(c=>c.a).SequenceEqual(after.Select(c=>c.a)),"LOD recolor changed the alpha channel");
   }Check(changed>0,"No native distant paint binding was exercised");
  }
  void ValidateSharedLods(){var filters=owned.Where(p=>p!=null).SelectMany(p=>p.GetAllParts()).SelectMany(p=>p.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh!=null&&f.sharedMesh.name.StartsWith("FS20 distant "));foreach(var group in filters.GroupBy(f=>f.sharedMesh.name))Check(group.Select(f=>f.sharedMesh.GetInstanceID()).Distinct().Count()==1,"Aircraft with the same paint duplicated a distant mesh");}
  IEnumerator Capture(string name,int lod){
   if(Environment.GetEnvironmentVariable("FS20_STREAM_SKIP_CAPTURES")=="1")yield break;
   var groups=target.GetAllParts().SelectMany(p=>p.GetComponentsInChildren<LODGroup>(true)).Distinct().ToArray();
   if(lod==0){var rows=new List<object>();foreach(var r in target.GetAllParts().SelectMany(p=>p.GetComponentsInChildren<MeshRenderer>(true)).Distinct()){var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh||!r.sharedMaterials.Any(m=>m&&m.shader&&m.shader.name=="Shader Graphs/LOD"))continue;var mesh=mf.sharedMesh;rows.Add(new{renderer=r.name,mesh=mesh.name,readable=mesh.isReadable,vertices=mesh.vertexCount,colors=mesh.isReadable?mesh.colors32:null,positions=mesh.isReadable?mesh.vertices.Select(v=>new[]{v.x,v.y,v.z}).ToArray():null,uv=mesh.isReadable?mesh.uv.Select(v=>new[]{v.x,v.y}).ToArray():null,triangles=mesh.isReadable?mesh.triangles:null});}File.WriteAllText(Path.Combine(output,name+"-native-lod.json"),JsonConvert.SerializeObject(rows));}
   Time.timeScale=0;foreach(var g in groups)g.ForceLOD(Math.Min(lod,g.lodCount-1));yield return null;yield return null;
   var rt=new RenderTexture(1400,1000,24);var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
   Check(RenderPipeline.SupportsRenderRequest(camera,request),"Native renderer does not support capture requests");RenderPipeline.SubmitRenderRequest(camera,request);var old=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(1400,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1400,1000),0,0);png.Apply();File.WriteAllBytes(Path.Combine(output,name+"-lod"+lod+".png"),png.EncodeToPNG());RenderTexture.active=old;Object.Destroy(png);rt.Release();Object.Destroy(rt);foreach(var g in groups)g.ForceLOD(-1);Time.timeScale=1;
  }
  void DestroyOwned(){foreach(var plane in owned)if(plane){foreach(var part in plane.GetAllParts().ToArray())if(part&&part.gameObject!=plane.gameObject)Object.Destroy(part.gameObject);Object.Destroy(plane.gameObject);}owned.Clear();target=null;}
  IEnumerator Run(){
   float deadline=Time.realtimeSinceStartup+360;var api=AccessTools.TypeByName("Blueprinter.Api");while(api!=null&&api.GetProperty("CurrentState",Flags)?.GetValue(null)?.ToString()!="Ready"&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
   while((Encyclopedia.Lookup==null||!Encyclopedia.Lookup.ContainsKey("JP_FS20_NA"))&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);Check(Encyclopedia.Lookup!=null&&Encyclopedia.Lookup.ContainsKey("JP_FS20_NA"),"Family registration failed");
   cache=AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20PaintCache");snapshot=cache?.GetMethod("Snapshot",Flags);collect=cache?.GetMethod("Collect",Flags);
   var parserType=AccessTools.TypeByName("NuclearOption.BuildScripts.CommandLineArgParser");var parser=Object.FindObjectOfType(parserType);parserType.GetField("missionName",Flags).SetValue(parser,"FS20 Streaming QA");parserType.GetField("port",Flags).SetValue(parser,28998);parserType.GetField("state",Flags).SetValue(parser,GameState.SinglePlayer);
   if(GameManager.gameState!=GameState.SinglePlayer){var method=parserType.GetMethod("parseAutoHost",Flags);method.Invoke(parser,new[]{Activator.CreateInstance(method.GetParameters()[0].ParameterType)});}
   deadline=Time.realtimeSinceStartup+120;while(!Object.FindObjectsOfType<Airbase>().Any(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin)&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
   airbase=Object.FindObjectsOfType<Airbase>().First(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin);spawner=Object.FindObjectOfType<Spawner>();camera=Camera.main??Camera.allCameras.FirstOrDefault(c=>c.enabled&&c.targetTexture==null);Check(camera!=null,"Native camera unavailable");Check(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"A real graphics device is required");
   foreach(var canvas in Object.FindObjectsOfType<Canvas>(true))canvas.enabled=false;workload=new RenderTexture(1400,1000,24);workload.Create();
   yield return new WaitForSecondsRealtime(10);Begin("idle-after-registration");yield return new WaitForSecondsRealtime(5);End(0);
   foreach(string variant in Environment.GetEnvironmentVariable("FS20_STREAM_FOCUS_CACHE")=="1"?new[]{"T"}:new[]{"B","A","C","T","NA"}){
    Begin(variant+"-warm-spawn");var timer=Stopwatch.StartNew();var plane=Spawn(variant);yield return WaitPaint(plane);timer.Stop();yield return new WaitForSecondsRealtime(3);End(timer.Elapsed.TotalMilliseconds);
    var definition=plane.definition as AircraftDefinition;int count=definition.aircraftParameters.liveries.Count;
    for(int index=0;index<count;index++){
     Begin(variant+"-paint-"+index);timer.Restart();plane.GetComponent<LiveryBehaviour>().SetKey(new LiveryKey(index));yield return WaitPaint(plane);timer.Stop();yield return new WaitForSecondsRealtime(2);End(timer.Elapsed.TotalMilliseconds);
     if(variant!="B"&&variant!="C"){Check(PreparedTexture(plane)!=null,"Prepared coat missing on "+variant);ValidateLods(plane);yield return Capture(variant+"-paint"+index,0);yield return Capture(variant+"-paint"+index,variant=="A"?1:2);}
    }
    plane.GetComponent<LiveryBehaviour>().SetKey(new LiveryKey(0));yield return WaitPaint(plane);
    Begin(variant+"-burst5");timer.Restart();for(int i=0;i<5;i++)Spawn(variant);timer.Stop();yield return new WaitForSecondsRealtime(5);End(timer.Elapsed.TotalMilliseconds);ValidateSharedLods();DestroyOwned();yield return new WaitForSecondsRealtime(2);
   }
   if(cache!=null){
    var plane=Spawn("T");yield return WaitPaint(plane);var livery=plane.GetComponent<LiveryBehaviour>();livery.SetKey(new LiveryKey(1));yield return WaitPaint(plane);
    var part=plane.GetAllParts().First(p=>p.gameObject.activeInHierarchy&&p.name.StartsWith("FS20T region ")&&PreparedTexture(p)!=null&&!p.IsDetached());var texture=PreparedTexture(part);int oldTextureId=texture.GetInstanceID();part.Detach(Vector3.zero,Vector3.zero);yield return new WaitForSecondsRealtime(.25f);Check(part.IsDetached(),"Native detachment failed");
    // Release the native Addressables handle while native attached/detached
    // renderers still use the coat. The renderer leases must now keep it alive.
    var handle=typeof(LiveryBehaviour).GetField("handle",Flags);var args=new[]{handle.GetValue(livery)};typeof(LiveryBehaviour).GetMethod("ReleaseHandle",Flags).Invoke(null,args);handle.SetValue(livery,args[0]);yield return null;collect.Invoke(null,new object[]{true});Check(part!=null&&texture!=null&&PreparedTexture(part)==texture,"Live detached renderer lost paint after native handle release");
    measurements.Add(new{phase="detached-and-attached-renderers-pin-after-handle-release",memory=Memory()});
    // Native Aircraft.OnDestroy removes every part in its partLookup, including
    // detached pieces. Do not assert that native debris survives this teardown.
    DestroyOwned();yield return null;yield return null;collect.Invoke(null,new object[]{true});yield return null;
    var leaseRows=new List<object>();foreach(var lease in (System.Collections.IEnumerable)cache.GetField("leases",Flags).GetValue(null)){var component=(Component)lease;if(component==null)continue;var t=component.transform;string path=t.name;for(var parent=t.parent;parent!=null;parent=parent.parent)path=parent.name+"/"+path;leaseRows.Add(new{path,paint=component.GetType().GetField("paintId",Flags).GetValue(component),active=component.gameObject.activeInHierarchy,scene=component.gameObject.scene.name,renderer=component.GetComponent<Renderer>()?.enabled});}File.WriteAllText(Path.Combine(output,"post-destroy-leases.json"),JsonConvert.SerializeObject(leaseRows));measurements.Add(new{phase="forced-idle-reclaim",memory=Memory()});Save(false,null);Check(texture==null,"Unused detached coat was not reclaimed");
    plane=Spawn("T");yield return WaitPaint(plane);plane.GetComponent<LiveryBehaviour>().SetKey(new LiveryKey(1));yield return WaitPaint(plane);var reloaded=PreparedTexture(part=plane.GetAllParts().First(p=>p.name.StartsWith("FS20T region ")));Check(reloaded!=null&&reloaded.GetInstanceID()!=oldTextureId,"Evicted paint did not reload as a valid new asset");DestroyOwned();yield return null;yield return null;collect.Invoke(null,new object[]{true});measurements.Add(new{phase="reload-and-reclaim",memory=Memory()});
   }
  }
 }
}
