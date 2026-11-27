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
  void MemoryLoadedSignature(){
   var method=AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20NetworkSignature").GetMethod("Create",Flags);
   var baseline=(byte[])method.Invoke(null,null);
   string file=Environment.GetEnvironmentVariable("FS20_NET_MEMORY_DLL");
   var memory=Assembly.Load(File.ReadAllBytes(file));var type=memory.GetType("FS20MemoryFixture.Plugin",true);
   var metadata=(BepInPlugin)type.GetCustomAttributes(typeof(BepInPlugin),false).Single();
   var info=new PluginInfo();typeof(PluginInfo).GetProperty("Metadata",Flags).SetValue(info,metadata);typeof(PluginInfo).GetProperty("Location",Flags).SetValue(info,file);
   var property=typeof(PluginInfo).GetProperty("Instance",Flags);
   var holder=new GameObject("Private in-memory assembly fixture");holder.SetActive(false);
   BepInEx.Bootstrap.Chainloader.PluginInfos.Add(metadata.GUID,info);
   try{
    Assert(string.IsNullOrEmpty(memory.Location),"Native Mono must load regression DLL without a Location");
    var instance=(BaseUnityPlugin)holder.AddComponent(type);instance.enabled=false;
    Assert(string.IsNullOrEmpty(instance.GetType().Assembly.Location),"Byte-loaded plugin instance lost its in-memory assembly");
    property.SetValue(info,instance);
    var digest=(byte[])method.Invoke(null,null);Assert(digest.Length==32&&!digest.SequenceEqual(baseline),"In-memory plugin was omitted from compatibility digest");
    string modified=Path.Combine(output,role+"-memory-modified.dll");File.Copy(file,modified);using(var stream=File.OpenWrite(modified)){stream.Seek(0,SeekOrigin.End);stream.WriteByte(71);}
    typeof(PluginInfo).GetProperty("Location",Flags).SetValue(info,modified);
    Assert(!((byte[])method.Invoke(null,null)).SequenceEqual(digest),"Changed plugin bytes were ignored by full native signature");
    typeof(PluginInfo).GetProperty("Location",Flags).SetValue(info,file);
    Assert(((byte[])method.Invoke(null,null)).SequenceEqual(digest),"Loader path fallback signature is not deterministic");
   }finally{BepInEx.Bootstrap.Chainloader.PluginInfos.Remove(metadata.GUID);Object.Destroy(holder);}
   Assert(((byte[])method.Invoke(null,null)).SequenceEqual(baseline),"Signature changed after fixture restoration");
   Write(role+"-memory-signature.json",new{passed=true,actualNativeMono=true,inMemoryPlugin=true,fullSignatureUnchanged=true});
  }
  MountedMissile[] Stores(Aircraft plane){
   var ids=plane.weaponManager.hardpointSets.Where(g=>g.name=="Wingtip rails"||g.name=="Middle wing pylons").SelectMany(g=>g.hardpoints).Select(p=>p.HardpointIndex).ToArray();
   return ids.SelectMany(id=>plane.weaponManager.HardpointsIndexes.TryGetValue(id,out var weapons)?weapons:Enumerable.Empty<Weapon>()).OfType<MountedMissile>().Distinct().ToArray();
  }
  Hardpoint Point(Weapon weapon)=>(Hardpoint)typeof(Weapon).GetField("hardpoint",Flags).GetValue(weapon);
  WeaponStation Station(Weapon weapon)=>(WeaponStation)typeof(Weapon).GetField("weaponStation",Flags).GetValue(weapon);
  int Ammo(Weapon weapon)=>weapon.GetAmmoLoaded();
  IEnumerator Weapons(Aircraft[] planes){
   var expected=new[]{2,4,2,0};
   for(int i=0;i<planes.Length;i++){Assert(Stores(planes[i]).Length==expected[i],"Native extra-store count differs: "+planes[i].definition.jsonKey);Assert(Stores(planes[i]).All(w=>Ammo(w)==1),"Native store did not spawn loaded");}
   if(role=="server"){
    while(!File.Exists(Path.Combine(output,"stores-ready.json"))&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Client did not confirm loaded stores");
    foreach(var plane in planes)foreach(var store in Stores(plane)){
     var station=Station(store);
     station.LaunchMount(plane,null,plane.transform.GlobalPosition()+plane.transform.forward*10000f);
     yield return new WaitForSecondsRealtime(1.2f);
    }
    foreach(var plane in planes)Assert(Stores(plane).All(w=>Ammo(w)==0),"Native firing did not spend new stores");
    Write("stores-fired.json",new{passed=true});
    while(!File.Exists(Path.Combine(output,"stores-observed-fired.json"))&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Client did not observe native store firing");
    foreach(var plane in planes){
     if(Stores(plane).Length==0)continue;
     var stations=plane.weaponManager.HardpointsIndexes.Values.SelectMany(w=>w).Select(Station).Where(s=>s!=null).Distinct().ToArray();
     var counts=new int[stations.Max(s=>(int)s.Number)+1];foreach(var station in stations){counts[station.Number]=station.FullAmmo;station.Rearm(station.FullAmmo);}
     plane.RpcRearm(new RearmEventArgs{Rearmer=plane,Stations=counts});
    }
    foreach(var plane in planes)Assert(Stores(plane).All(w=>Ammo(w)==1),"Native rearm failed");Write("stores-rearmed.json",new{passed=true});
    while(!File.Exists(Path.Combine(output,"stores-observed-rearmed.json"))&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Client did not observe native rearm");
   }else{
    Write("stores-ready.json",new{passed=true});
    while(!File.Exists(Path.Combine(output,"stores-fired.json"))&&!Expired)yield return new WaitForSecondsRealtime(.1f);
    while(planes.Any(p=>Stores(p).Any(w=>Ammo(w)!=0))&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Remote ammo did not reflect firing");Write("stores-observed-fired.json",new{passed=true});
    while(!File.Exists(Path.Combine(output,"stores-rearmed.json"))&&!Expired)yield return new WaitForSecondsRealtime(.1f);
    while(planes.Any(p=>Stores(p).Any(w=>Ammo(w)!=1))&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Remote rearm did not restore missiles");Write("stores-observed-rearmed.json",new{passed=true});
   }
  }
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
   MemoryLoadedSignature();
   if(role=="server"){
    while((!SteamManager.ServerInitialized||!Steamworks.SteamGameServer.BLoggedOn())&&!Expired)yield return new WaitForSecondsRealtime(.2f);
    Assert(!Expired,"Native anonymous Steam server initialization failed/timed out");
    var manager=NetworkManagerNuclearOption.i;
    while((manager==null||!manager.Server.Active||!manager.Server.Listening)&&!Expired){manager=NetworkManagerNuclearOption.i;yield return new WaitForSecondsRealtime(.2f);}
    Assert(!Expired,"Native dedicated server is not listening");Assert(manager.DedicatedServerManager.Config.Hidden,"Private test server must be hidden");
    Write("ready.json",new{id=Steamworks.SteamGameServer.GetSteamID().m_SteamID.ToString(),hidden=true,gameAssemblyInMemory=string.IsNullOrEmpty(typeof(Aircraft).Assembly.Location),signature=Convert.ToBase64String((byte[])AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20NetworkSignature").GetMethod("Create",Flags).Invoke(null,null))});
    while(!Object.FindObjectsOfType<Airbase>().Any(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin)&&!Expired)yield return new WaitForSecondsRealtime(.2f);
    Assert(!Expired,"Native mission did not start after client connection");
    var airbase=Object.FindObjectsOfType<Airbase>().First(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin);var spawner=Object.FindObjectOfType<Spawner>();
    foreach(string key in new[]{"JP_FS20_A","JP_FS20_CTOL","JP_FS20_T","JP_FS20_NA"}){
     var def=(AircraftDefinition)Encyclopedia.Lookup[key];var load=new NuclearOption.SavedMission.Loadout();foreach(var station in def.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets)load.weapons.Add(station.name=="Wingtip rails"||station.name=="Middle wing pylons"?station.weaponOptions.First(m=>m!=null&&m.jsonKey=="AAM2_single"):null);
     var plane=spawner.SpawnAircraft(null,def.unitPrefab,load,.6f,new LiveryKey(1),airbase.transform.GlobalPosition()+new Vector3(1500+assertions*150,1500,1000),Quaternion.identity,Vector3.forward*200,null,airbase.CurrentHQ,"FS20 private network test",1,.5f);
     Assert(plane!=null,"Native server failed to spawn "+key);
    }
    Write("spawned.json",new{variants=new[]{"JP_FS20_A","JP_FS20_CTOL","JP_FS20_T","JP_FS20_NA"},paintIndex=1});
    while(!File.Exists(Path.Combine(output,"observed.json"))&&!Expired)yield return new WaitForSecondsRealtime(.2f);
    Assert(!Expired,"Client did not observe native spawned variants");
    yield return Weapons(new[]{"JP_FS20_A","JP_FS20_CTOL","JP_FS20_T","JP_FS20_NA"}.Select(k=>Object.FindObjectsOfType<Aircraft>().First(p=>p.definition.jsonKey==k)).ToArray());
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
    yield return Weapons(planes);
    while(!File.Exists(Path.Combine(output,"damage.json"))&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(!Expired,"Server damage trigger timed out");
    var name=JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(Path.Combine(output,"damage.json")))["part"];var part=planes[2].GetAllParts().First(p=>p.name==name);
    while(!part.IsDetached()&&!Expired)yield return new WaitForSecondsRealtime(.2f);Assert(part.IsDetached(),"Native damage did not reach remote T part");
    var stop=(Cysharp.Threading.Tasks.UniTask)typeof(NetworkManagerNuclearOption).GetMethod("StopAsync",Flags).Invoke(NetworkManagerNuclearOption.i,new object[]{false});
    var stopping=stop.GetAwaiter();while(!stopping.IsCompleted&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Native client stop timed out");stopping.GetResult();
    Assert(!(bool)accepted.GetValue(null),"Compatibility state leaked after disconnect");
    var reload=MainMenu.WaitForLoaded(System.Threading.CancellationToken.None).GetAwaiter();while(!reload.IsCompleted&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Main menu did not restore after disconnect");reload.GetResult();
    var reconnectPreload=NetworkManagerNuclearOption.Preload(System.Threading.CancellationToken.None).GetAwaiter();while(!reconnectPreload.IsCompleted&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Network preload did not restore");reconnectPreload.GetResult();
    NetworkManagerNuclearOption.i.StartClient(options);
    while(!(bool)accepted.GetValue(null)&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Second native handshake failed");
    while(!keys.All(k=>Object.FindObjectsOfType<Aircraft>().Any(p=>p.definition!=null&&p.definition.jsonKey==k))&&!Expired)yield return new WaitForSecondsRealtime(.1f);Assert(!Expired,"Late/reconnected native variants did not spawn");
    Write("reconnected.json",new{passed=true,nativeDisconnect=true,secondHandshake=true,lateSpawn=true});
   }
  }
 }
}
