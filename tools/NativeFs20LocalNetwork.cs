using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using NuclearOption.Networking;
using NuclearOption.DedicatedServer;
using Object=UnityEngine.Object;
namespace FS20LocalNetworkVerification {
 [BepInPlugin("jp.fs20.private-network-check","FS20 private native network check","0.1.0")]
 public sealed class Check:BaseUnityPlugin {
  const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  string output,role;int assertions;float deadline;
  void Assert(bool value,string message){assertions++;if(!value)throw new Exception(message);}
  bool Expired=>Time.realtimeSinceStartup>deadline;
  void Write(string file,object value)=>File.WriteAllText(Path.Combine(output,file),JsonConvert.SerializeObject(value,Formatting.Indented));
  object Cache()=>AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20PaintCache").GetMethod("Snapshot",Flags).Invoke(null,null);
  Texture[] Textures(Aircraft plane){var values=new System.Collections.Generic.List<Texture>();var block=new MaterialPropertyBlock();foreach(var renderer in plane.GetAllParts().SelectMany(p=>p.GetComponentsInChildren<Renderer>(true)).Distinct()){var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++){var material=materials[i];if(!material)continue;renderer.GetPropertyBlock(block,i);foreach(string key in new[]{"_Livery","_BaseMap","_Basecolor"}){var texture=block.GetTexture(key)??(material.HasProperty(key)?material.GetTexture(key):null);if(texture!=null)values.Add(texture);}block.Clear();}}return values.Distinct().ToArray();}
  IEnumerator Start(){
   output=Environment.GetEnvironmentVariable("FS20_NET_OUTPUT");role=Environment.GetEnvironmentVariable("FS20_NET_ROLE");if(string.IsNullOrEmpty(output))yield break;
   Application.runInBackground=true;Application.targetFrameRate=30;deadline=Time.realtimeSinceStartup+300;
   var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Run());
   while(stack.Count>0){object value=null;bool next=false;Exception error=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
    if(error!=null){Write(role+".json",new{completed=false,error=error.ToString(),assertions,nativeAuthenticationUnmodified=true});Application.Quit(1);yield break;}
    if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;
   }
   Write(role+".json",new{completed=true,assertions,nativeAuthenticationUnmodified=true,realNativeProcesses=true,sameComputer=true,twoPcAcceptance=false,cache=Cache()});Application.Quit(0);
  }
  IEnumerator Run(){
   while((Encyclopedia.Lookup==null||!Encyclopedia.Lookup.ContainsKey("JP_FS20_NA"))&&!Expired)yield return new WaitForSecondsRealtime(.2f);
   Assert(!Expired,"Family registration timed out");
   if(role=="server"){
    while((!SteamManager.ServerInitialized||!Steamworks.SteamGameServer.BLoggedOn())&&!Expired)yield return new WaitForSecondsRealtime(.2f);
    Assert(!Expired,"Native anonymous Steam server initialization failed/timed out");
    var manager=NetworkManagerNuclearOption.i;
    while((manager==null||!manager.Server.Active||!manager.Server.Listening)&&!Expired){manager=NetworkManagerNuclearOption.i;yield return new WaitForSecondsRealtime(.2f);}
    Assert(!Expired,"Native dedicated server is not listening");Assert(manager.DedicatedServerManager.Config.Hidden,"Private test server must be hidden");
    Write("ready.json",new{id=Steamworks.SteamGameServer.GetSteamID().m_SteamID.ToString(),hidden=true});
    while(!Object.FindObjectsOfType<Airbase>().Any(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin)&&!Expired)yield return new WaitForSecondsRealtime(.2f);
    Assert(!Expired,"Native mission did not start after client connection");
    var airbase=Object.FindObjectsOfType<Airbase>().First(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin);var spawner=Object.FindObjectOfType<Spawner>();
    foreach(string key in new[]{"JP_FS20_A","JP_FS20_CTOL","JP_FS20_T","JP_FS20_NA"}){
     var def=(AircraftDefinition)Encyclopedia.Lookup[key];var load=new NuclearOption.SavedMission.Loadout();foreach(var station in def.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets)load.weapons.Add(null);
     var plane=spawner.SpawnAircraft(null,def.unitPrefab,load,.6f,new LiveryKey(1),airbase.transform.GlobalPosition()+new Vector3(1500+assertions*150,1500,1000),Quaternion.identity,Vector3.forward*200,null,airbase.CurrentHQ,"FS20 private network test",1,.5f);
     Assert(plane!=null,"Native server failed to spawn "+key);
    }
    Write("spawned.json",new{variants=new[]{"JP_FS20_A","JP_FS20_CTOL","JP_FS20_T","JP_FS20_NA"},paintIndex=1});
    while(!File.Exists(Path.Combine(output,"observed.json"))&&!Expired)yield return new WaitForSecondsRealtime(.2f);
    Assert(!Expired,"Client did not observe native spawned variants");
    var t=Object.FindObjectsOfType<Aircraft>().First(p=>p.definition.jsonKey=="JP_FS20_T");var part=t.GetAllParts().First(p=>p.name.StartsWith("FS20T region "));
    byte id=(byte)typeof(UnitPart).GetField("id",Flags).GetValue(part);t.DetachPart(id,Vector3.zero,Vector3.zero);
    Write("damage.json",new{part=part.name});
    while(!File.Exists(Path.Combine(output,"client.json"))&&!Expired)yield return new WaitForSecondsRealtime(.2f);
    Assert(!Expired,"Client did not finish native damage replication check");
    Assert((bool)JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(output,"client.json")))["completed"],"Client verification failed");
   }else{
    var menu=MainMenu.WaitForLoaded(System.Threading.CancellationToken.None).GetAwaiter();while(!menu.IsCompleted&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Native main menu initialization timed out");menu.GetResult();
    var preload=NetworkManagerNuclearOption.Preload(System.Threading.CancellationToken.None).GetAwaiter();while(!preload.IsCompleted&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Native network manager preload timed out");preload.GetResult();
    while(!File.Exists(Path.Combine(output,"ready.json"))&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Server readiness timed out");
    var ready=JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(output,"ready.json")));
    var options=new ConnectOptions(SocketType.Steam,ready["id"].ToString()){Password=Environment.GetEnvironmentVariable("FS20_NET_PASSWORD")};NetworkManagerNuclearOption.i.StartClient(options);
    var mp=AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20Multiplayer");var accepted=mp.GetProperty("ClientAccepted",Flags);
    while(!(bool)accepted.GetValue(null)&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Native authentication / FS20 compatibility handshake did not finish");
    var keys=new[]{"JP_FS20_A","JP_FS20_CTOL","JP_FS20_T","JP_FS20_NA"};
    while(!keys.All(k=>Object.FindObjectsOfType<Aircraft>().Any(p=>p.definition!=null&&p.definition.jsonKey==k))&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Native remote aircraft spawn timed out");
    var planes=keys.Select(k=>Object.FindObjectsOfType<Aircraft>().First(p=>p.definition!=null&&p.definition.jsonKey==k)).ToArray();
    while(planes.Any(p=>p.GetComponent<LiveryBehaviour>().IsLoading)&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Remote native livery loading timed out");
    yield return new WaitForSecondsRealtime(.3f);
    var paints=(System.Collections.IDictionary)AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20PreparedLiveries").GetField("paints",Flags).GetValue(null);
    var materialInfo=new System.Collections.Generic.List<object>();
    foreach(var plane in planes){
     Assert(!plane.LocalSim,"Remote AI incorrectly acquired local simulation authority");
     if(plane.definition.jsonKey=="JP_FS20_CTOL")continue;
     string guid=((AircraftDefinition)plane.definition).aircraftParameters.liveries[1].assetReference.AssetGUID;
     var paint=paints.Values.Cast<object>().Single(p=>(string)p.GetType().GetProperty("Guid",Flags).GetValue(p)==guid);var actual=Textures(plane);var body=(Texture)paint.GetType().GetField("bodyTexture",Flags).GetValue(paint);var wing=(Texture)paint.GetType().GetField("wingTexture",Flags).GetValue(paint);
     Assert(body!=null&&actual.Contains(body),"Remote body paint identity mismatch");Assert(wing!=null&&actual.Contains(wing),"Remote wing paint identity mismatch");
     materialInfo.Add(new{variant=plane.definition.jsonKey,body=body.name,wing=wing.name});
    }
    Write("observed.json",new{keys,authenticated=true,compatibilityAccepted=true,remoteAuthority=true,paintBindings=materialInfo,cache=Cache()});
    while(!File.Exists(Path.Combine(output,"damage.json"))&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Server damage trigger timed out");
    var name=JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(Path.Combine(output,"damage.json")))["part"];var part=planes[2].GetAllParts().First(p=>p.name==name);
    while(!part.IsDetached()&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(part.IsDetached(),"Native damage did not reach remote T part");
   }
  }
 }
}
