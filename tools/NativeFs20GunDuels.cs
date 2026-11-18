using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using BepInEx;using HarmonyLib;using Newtonsoft.Json;using UnityEngine;using Object=UnityEngine.Object;
namespace FS20Duels {
[DefaultExecutionOrder(32000)]
[BepInPlugin("jp.fs20.gun-duels","FS20 temporary gun duels","0.4.0")]
public sealed class DuelBench:BaseUnityPlugin {
const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
static Type guidanceType;static MethodInfo gainsMethod,yawMethod,captureMethod;static FieldInfo captureFrameField,captureWeightField;static FieldInfo scopePitch,scopeRoll,scopeYaw,scalePitch,scaleRoll,scaleYaw;static DuelBench self;string output;string probeError;int[] pidChecks=new int[2],captureChecks=new int[2],nativeChecks=new int[2],fullCommandChecks=new int[2];HashSet<AeroPID> checkedPids=new HashSet<AeroPID>();Aircraft[] pair;CameraStateManager cameraManager;int[] shots=new int[2];Dictionary<Gun,Tuple<int,WeaponStation>> ownedGuns=new Dictionary<Gun,Tuple<int,WeaponStation>>();float began,nextSample;StreamWriter csv;bool active;List<object> results=new List<object>();Dictionary<Aircraft,Vector3> raw=new Dictionary<Aircraft,Vector3>(),filtered=new Dictionary<Aircraft,Vector3>();
static object Get(object x,string name)=>x==null?null:AccessTools.Field(x.GetType(),name)?.GetValue(x);
static string S(object x)=>x==null?"":x.ToString();
void Stage(string s)=>File.AppendAllText(Path.Combine(output,"stages.log"),Time.realtimeSinceStartup+" "+s+"\n");
void Save(bool done,string error)=>File.WriteAllText(Path.Combine(output,"duels.json"),JsonConvert.SerializeObject(new{completed=done,error,product=Application.productName,resolution=new{Screen.width,Screen.height},rounds=results,scope="Native mission combat, weapons, damage and free flight; equal perfect shared opponent tracking supplied to isolate flight/gun AI from sensor acquisition. No guidance/throttle/FBW gains overwritten. 100% initial internal fuel, clean gun-only loadout, 1100 km/h (305.5556 m/s), native gun replenishment after each firing tick, 3000 m, 2500 m head-on separation, skill 1 and bravery 0.5; two mirrored rounds per pairing, independent seeds, 120 simulated seconds maximum, timeScale 1, native orbital camera with user input (no per-frame pose override), native fixedDeltaTime unchanged."},Formatting.Indented));
IEnumerator Start(){if(Environment.GetEnvironmentVariable("FS20_DUEL")!="1"){enabled=false;yield break;}self=this;guidanceType=AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20AiGuidance");if(guidanceType!=null){gainsMethod=AccessTools.Method(guidanceType,"Gains");captureMethod=AccessTools.Method(guidanceType,"CaptureTelemetry");captureFrameField=AccessTools.Field(guidanceType,"aimFrame");captureWeightField=captureFrameField==null?null:AccessTools.Field(captureFrameField.FieldType,"Weight");yawMethod=AccessTools.Method(guidanceType,"YawGain");scopeYaw=AccessTools.Field(guidanceType,"yaw");scaleYaw=AccessTools.Field(guidanceType,"yawScale");scopePitch=AccessTools.Field(guidanceType,"pitch");scopeRoll=AccessTools.Field(guidanceType,"roll");scalePitch=AccessTools.Field(guidanceType,"pitchScale");scaleRoll=AccessTools.Field(guidanceType,"rollScale");}output=Environment.GetEnvironmentVariable("FS20_DUEL_OUTPUT");Directory.CreateDirectory(output);Application.runInBackground=true;var harmony=new Harmony("jp.fs20.duel-witness");harmony.PatchAll(typeof(FilterWitness));harmony.PatchAll(typeof(InfiniteAmmoWitness));harmony.PatchAll(typeof(PidWitness));var work=Run();while(true){object n;try{if(!work.MoveNext())break;n=work.Current;}catch(Exception e){csv?.Dispose();Save(false,e.ToString());Time.timeScale=1;Application.Quit(1);yield break;}yield return n;}Save(true,null);Time.timeScale=1;Application.Quit(0);}
[HarmonyPatch(typeof(ControlsFilter),"Filter")]static class FilterWitness {
[HarmonyPriority(Priority.First)]static void Prefix(ControlsFilter __instance,Vector3 rawInputs){var a=Get(__instance,"aircraft")as Aircraft;if(self!=null&&self.active&&a&&self.pair.Contains(a))self.raw[a]=rawInputs;}
[HarmonyPriority(Priority.Last)]static void Postfix(ControlsFilter __instance,ControlInputs inputs){var a=Get(__instance,"aircraft")as Aircraft;if(self!=null&&self.active&&a&&self.pair.Contains(a))self.filtered[a]=new Vector3(inputs.pitch,inputs.yaw,inputs.roll);}
}
[HarmonyPatch(typeof(Gun),"FixedUpdate")]static class InfiniteAmmoWitness {
static void Prefix(Gun __instance,out int __state){__state=__instance.ammo;}
static void Postfix(Gun __instance,int __state){
 if(self==null||!self.active||!self.ownedGuns.TryGetValue(__instance,out var entry))return;
 int fired=__state-__instance.ammo;if(fired<=0)return;
 self.shots[entry.Item1]+=fired;
 // Native rearm restores both rounds and the mass removed by SpawnBullet.
 // Heat, rate of fire, recoil, ballistics, damage and firing decisions stay native.
 __instance.Rearm(fired,entry.Item2);entry.Item2.AccountAmmo();entry.Item2.Updated();
}
}
[HarmonyPatch(typeof(AeroPID),nameof(AeroPID.GetOutputClampP))]static class PidWitness {
 [HarmonyPriority(Priority.Last)]static void Postfix(AeroPID __instance,float __1,float __3,float __result){
  if(self==null||!self.active||(self.checkedPids.Contains(__instance)&&Time.frameCount%12!=0))return;
  if(guidanceType==null)return;
  var p=scopePitch.GetValue(null);var r=scopeRoll.GetValue(null);
  bool pitchAxis=ReferenceEquals(p,__instance),rollAxis=ReferenceEquals(r,__instance),yawAxis=scopeYaw!=null&&ReferenceEquals(scopeYaw.GetValue(null),__instance);if(!pitchAxis&&!rollAxis&&!yawAxis)return;
  var factors=(PIDFactors)Get(__instance,"factors");float speed=(float)Get(__instance,"referenceAirspeed");
  float n=Mathf.Clamp(speed*speed/Mathf.Max(__3*__3,10f),0f,4f);
  float native=Mathf.Clamp((float)Get(__instance,"p"),-__1,__1)*factors.P+(float)Get(__instance,"i")*factors.I+(float)Get(__instance,"d")*factors.D*n;
  float scale=(float)(pitchAxis?scalePitch:rollAxis?scaleRoll:scaleYaw).GetValue(null);
  float expected=native*scale;
  float weight=captureFrameField==null?0f:(float)captureWeightField.GetValue(captureFrameField.GetValue(null));
  float tolerance=.001f*Mathf.Max(1f,Mathf.Abs(expected));
  if(weight>0f&&!rollAxis)tolerance+=.3501f*weight;
  if(float.IsNaN(__result)||Mathf.Abs(__result-expected)>tolerance)self.probeError="Native scoped PID-output mismatch.";
  bool first=self.checkedPids.Add(__instance);
  for(int side=0;side<self.pair.Length;side++){
   var a=self.pair[side];if(!a||a.autopilot==null)continue;
   var f=AccessTools.Field(typeof(Autopilot),"forwardFlightController").GetValue(a.autopilot);
   if(ReferenceEquals(AccessTools.Field(f.GetType(),pitchAxis?"xPID":rollAxis?"zPID":"yPID").GetValue(f),__instance)){
    if(first)self.pidChecks[side]++;
    if(weight>0f&&!rollAxis)self.captureChecks[side]++;else self.nativeChecks[side]++;
    if(weight==0f&&Mathf.Abs(expected)>=.95f)self.fullCommandChecks[side]++;
   }
  }
 }
}
object Audit(Aircraft a){
 var method=gainsMethod;
 if(method==null)return null;
 var before=(Vector2)method.Invoke(null,new object[]{a});var pilot=a.pilots[0];bool was=pilot.playerControlled;
 bool manual,nullGuard;float yawGain=yawMethod==null?1f:(float)yawMethod.Invoke(null,new object[]{a});
 try{pilot.playerControlled=true;manual=(Vector2)method.Invoke(null,new object[]{a})==Vector2.one&&(yawMethod==null||(float)yawMethod.Invoke(null,new object[]{a})==1f);nullGuard=(Vector2)method.Invoke(null,new object[]{null})==Vector2.one;}finally{pilot.playerControlled=was;}
 bool stock=a.definition.code!="FS-20"||before==Vector2.one;
 if(!manual||!nullGuard||!stock)throw new Exception("AI/manual/native-B gain guard contract failed.");
 return new{pitch=before.x,roll=before.y,yaw=yawGain,manual,nullGuard,stock};
}
void LateUpdate(){if(!active||pair==null)return;if(Time.time<nextSample)return;nextSample=Time.time+.1f;for(int i=0;i<2;i++)Sample(i);}
int Ammo(Aircraft a)=>a?a.weaponStations.Where(w=>w.WeaponInfo.gun).Sum(w=>w.GetAmmoTotal()):0;
void Sample(int i){var a=pair[i];var enemy=pair[1-i];if(!a)return;var v=a.transform.InverseTransformDirection(a.rb.velocity-a.GetWindVelocity());var w=a.CockpitRB().transform.InverseTransformDirection(a.CockpitRB().angularVelocity)*Mathf.Rad2Deg;var input=a.GetInputs();var pilot=a.pilots.FirstOrDefault(p=>p);var mode=pilot==null?null:pilot.currentState;var r=raw.TryGetValue(a,out var rr)?rr:Vector3.zero;var f=filtered.TryGetValue(a,out var ff)?ff:Vector3.zero;
var flightController=AccessTools.Field(typeof(Autopilot),"forwardFlightController").GetValue(a.autopilot);
var surfaces=Object.FindObjectsOfType<ControlSurface>().Where(c=>Get(c,"aircraft")as Aircraft==a).Select(c=>new{name=c.name,angle=c.transform.localEulerAngles.ToString("F3"),split=Get(c,"splitAmount")}).ToArray();
var controllers=a.GetComponents<MonoBehaviour>().Where(c=>c&&c.GetType().Name.StartsWith("Fs20")&&(c.GetType().Name.Contains("Controller")||c.GetType().Name.Contains("Guidance"))).Select(c=>new{type=c.GetType().Name,values=c.GetType().GetFields(F).Where(k=>!k.IsStatic&&(k.FieldType==typeof(float)||k.FieldType==typeof(bool))).ToDictionary(k=>k.Name,k=>k.GetValue(c))}).ToArray();
object capture=captureMethod==null?null:captureMethod.Invoke(null,new object[]{a});
var captureData=capture==null?null:new{weight=Get(capture,"Weight"),angle=Get(capture,"Angle"),closingSpeed=Get(capture,"ClosingSpeed"),throttleCeiling=Get(capture,"ThrottleCeiling"),pitchRateError=Get(capture,"PitchErrorRate"),yawRateError=Get(capture,"YawErrorRate")};
var row=new{capture=captureData,t=Time.time-began,side=i,variant=a.definition.code,alive=!a.disabled,altitude=a.GlobalPosition().y,speed=a.speed,alpha=-Mathf.Atan2(v.y,Mathf.Max(1,v.z))*Mathf.Rad2Deg,beta=Mathf.Atan2(v.x,Mathf.Max(1,v.z))*Mathf.Rad2Deg,g=a.gForce,mass=a.GetMass(),fuel=a.GetFuelLevel(),ammo=Ammo(a),autopilotType=a.autopilot.GetType().Name,nativePitchError=Get(Get(flightController,"xPID"),"p"),nativeYawError=Get(Get(flightController,"yPID"),"p"),nativeRollError=Get(Get(flightController,"zPID"),"p"),flightAssist=a.flightAssist,fbw=a.GetControlsFilter().GetFlyByWire().Enabled,leadError=Get(mode,"lastTargetAngle"),gunGate=enemy?Mathf.Clamp(50f*enemy.maxRadius/Mathf.Max(1f,Vector3.Distance(a.transform.position,enemy.transform.position)),.5f,3f):0,targetAccurate=Get(mode,"targetAccurate"),strafeTimer=Get(mode,"strafeTimer"),onTargetSmoothed=Get(a.GetComponent<AutopilotPlane>(),"onTargetSmoothed"),shotsFired=shots[i],timeScale=Time.timeScale,cameraState=cameraManager?cameraManager.currentState.GetType().Name:"none",pitchRate=w.x,yawRate=w.y,rollRate=w.z,rawPitch=r.x,rawYaw=r.y,rawRoll=r.z,filteredPitch=f.x,filteredYaw=f.y,filteredRoll=f.z,finalPitch=input.pitch,finalRoll=input.roll,finalYaw=input.yaw,throttle=input.throttle,mode=S(Get(mode,"attackMode")),state=pilot?pilot.GetCurrentState():"none",target=S(Get(mode,"currentTarget")),distance=enemy?Vector3.Distance(a.transform.position,enemy.transform.position):0,angle=enemy?Vector3.Angle(a.transform.forward,enemy.transform.position-a.transform.position):180,partsLost=Object.FindObjectsOfType<UnitPart>().Count(p=>p.parentUnit==a&&p.IsDetached()),surfaces,controllers};csv.WriteLine(JsonConvert.SerializeObject(row));}
Aircraft Spawn(string v,FactionHQ h,GlobalPosition pos,Quaternion rot){string key=v=="B"?"SmallFighter1":v=="C"?"JP_FS20_CTOL":"JP_FS20_"+v;var d=(AircraftDefinition)Encyclopedia.Lookup[key];var load=new NuclearOption.SavedMission.Loadout();foreach(var set in d.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets)load.weapons.Add(set.weaponOptions.FirstOrDefault(m=>m&&(m.GunAmmo||(m.info&&m.info.gun&&!m.turret))));return Object.FindObjectOfType<Spawner>().SpawnAircraft(null,d.unitPrefab,load,1f,new LiveryKey(0),pos,rot,rot*Vector3.forward*(1100f/3.6f),null,h,"FS20 gun duel",1,.5f);}
void Track(Aircraft a,Aircraft b){if(!a||!b||a.disabled||b.disabled)return;var h=a.NetworkHQ;h.RpcUpdateTrackingInfo(b.persistentID);foreach(var p in a.pilots)if(p)p.SetPrimaryTarget(b);}
void Clean(){active=false;if(cameraManager&&pair.Any(a=>a&&cameraManager.followingUnit==a))cameraManager.SetFollowingUnit(null);ownedGuns.Clear();csv?.Dispose();csv=null;Time.timeScale=1;foreach(var a in pair)if(a){foreach(var p in Object.FindObjectsOfType<UnitPart>(true).Where(p=>p.parentUnit==a))if(p)Object.Destroy(p.gameObject);if(a)Object.Destroy(a.gameObject);}pair=null;}
IEnumerator Run(){
            Stage("Waiting for native game initialization");
            float deadline=Time.realtimeSinceStartup+360;
            var api=AccessTools.TypeByName("Blueprinter.Api");
            while(api!=null&&api.GetProperty("CurrentState",F)?.GetValue(null)?.ToString()!="Ready"&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
            if(api!=null&&api.GetProperty("CurrentState",F)?.GetValue(null)?.ToString()!="Ready")throw new Exception("Private Blueprinter initialization timed out");
            Stage("Native initialization ready; waiting for FS-20 registration");
            deadline=Mathf.Min(deadline,Time.realtimeSinceStartup+15);
            while((Encyclopedia.Lookup==null||!Encyclopedia.Lookup.ContainsKey("JP_FS20_NA"))&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
            if(Encyclopedia.Lookup==null||!Encyclopedia.Lookup.ContainsKey("JP_FS20_NA"))throw new Exception("Tested FS-20 family did not register");
            var parserType=AccessTools.TypeByName("NuclearOption.BuildScripts.CommandLineArgParser");
            var parser=Object.FindObjectOfType(parserType);
            parserType.GetField("missionName",F).SetValue(parser,"FS20 Spawn Profile");
            parserType.GetField("port",F).SetValue(parser,28998);
            parserType.GetField("state",F).SetValue(parser,GameState.SinglePlayer);
            var host=parserType.GetMethod("parseAutoHost",F);
            Stage("Starting offline mission; current state="+GameManager.gameState+"; data="+Application.persistentDataPath);
            if(GameManager.gameState!=GameState.SinglePlayer)host.Invoke(parser,new[]{Activator.CreateInstance(host.GetParameters()[0].ParameterType)});
            deadline=Time.realtimeSinceStartup+120;
            while((GameManager.gameState!=GameState.SinglePlayer||!Object.FindObjectsOfType<Airbase>().Any(b=>b.CurrentHQ))&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
            if(GameManager.gameState!=GameState.SinglePlayer)throw new Exception("Private offline mission did not start");
            Stage("Offline mission ready");
            yield return new WaitForSecondsRealtime(5);


var reference=Object.FindObjectsOfType<Airbase>().First(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin);var hqs=Object.FindObjectsOfType<FactionHQ>().Where(h=>h.faction).ToArray();var h0=reference.CurrentHQ;var h1=hqs.First(h=>h.faction!=h0.faction);cameraManager=SceneSingleton<CameraStateManager>.i;var duelCenter=reference.transform.GlobalPosition()+new Vector3(0,3000,50000);
foreach(string v in (Environment.GetEnvironmentVariable("FS20_DUEL_VARIANTS")??"B,A,C,T,NA").Split(','))for(int round=0;round<2;round++){
string label=v+"-vs-B-"+round;Stage(label);int seedBase=int.Parse(Environment.GetEnvironmentVariable("FS20_DUEL_SEED")??"7041");UnityEngine.Random.InitState(seedBase+round);Time.timeScale=0;var center=duelCenter;shots=new int[2];pidChecks=new int[2];captureChecks=new int[2];nativeChecks=new int[2];fullCommandChecks=new int[2];checkedPids.Clear();bool flip=round==1;var rot=Quaternion.Euler(0,flip?180:0,0);var dir=rot*Vector3.forward;
pair=new[]{Spawn(v,flip?h1:h0,center-dir*1250,rot),Spawn("B",flip?h0:h1,center+dir*1250,rot*Quaternion.Euler(0,180,0))};if(pair.Any(a=>!a))throw new Exception("Spawn failed");yield return null;Time.timeScale=1;yield return new WaitForSecondsRealtime(1);
var initial=pair.Select(a=>new{variant=a.definition.code,fuel=a.GetFuelLevel(),mass=a.GetMass(),fbwG=Get(a.GetControlsFilter().GetFlyByWire(),"gLimitPositive"),fbwRoll=Get(a.GetControlsFilter().GetFlyByWire(),"maxRollAngularVel"),spawnTargetSpeedKmh=1100,speedAtReleaseKmh=a.speed*3.6f,ammo=Ammo(a),weapons=a.weaponStations.Select(w=>new{name=w.WeaponInfo.name,gun=w.WeaponInfo.gun,energy=w.WeaponInfo.energy,ammo=w.GetAmmoTotal()}).ToArray(),corner=a.GetAircraftParameters().cornerSpeed}).ToArray();
if(initial.Any(x=>x.fuel<.995f||x.ammo==0||x.weapons.Any(w=>!w.gun))||initial[0].ammo!=initial[1].ammo)throw new Exception("Full fuel/equal gun-only loadout contract failed: "+JsonConvert.SerializeObject(initial));
foreach(var a in pair){a.SetGear(LandingGear.GearState.LockedRetracted);if(!a.Ignition)a.CmdToggleIgnition();}Track(pair[0],pair[1]);Track(pair[1],pair[0]);foreach(var a in pair){var p=a.pilots.First(p=>p);p.SwitchStateNew(new AIPilotCombatModes(a));}
for(int i=0;i<pair.Length;i++)foreach(var station in pair[i].weaponStations.Where(w=>w.WeaponInfo.gun))foreach(var gun in station.Weapons.OfType<Gun>()){
 if((int)Get(gun,"maxMagazines")!=0)throw new Exception("Infinite ammo fixture expects the native single-magazine rotary gun.");
 ownedGuns.Add(gun,Tuple.Create(i,station));
}
var gainAudit=pair.Select(Audit).ToArray();if(cameraManager)cameraManager.SetFollowingUnit(pair[0]);
csv=new StreamWriter(Path.Combine(output,label+".jsonl"));began=Time.time;nextSample=began;active=true;Time.timeScale=1;string outcome="time_limit";while(Time.time-began<120){if(probeError!=null)throw new Exception(probeError);if(GameManager.gameState!=GameState.SinglePlayer)throw new Exception("Duel mission was closed; stop the owned test.");if(pair.Any(a=>!a||a.disabled)){outcome="disabled_or_destroyed";break;}if(pair.Any(a=>a.GlobalPosition().y<Datum.LocalSeaY+50)){outcome="terrain_or_sea_danger";break;}Track(pair[0],pair[1]);Track(pair[1],pair[0]);yield return new WaitForSecondsRealtime(.25f);}results.Add(new{label,variant=v,round,seed=seedBase+round,initial,elapsed=Time.time-began,outcome,shotsFired=shots.ToArray(),pidChecks=pidChecks.ToArray(),captureChecks=captureChecks.ToArray(),nativeChecks=nativeChecks.ToArray(),fullCommandChecks=fullCommandChecks.ToArray(),gainAudit,end=pair.Select(a=>new{alive=a&&!a.disabled,ammo=Ammo(a),fuel=a?a.GetFuelLevel():0,altitude=a?a.GlobalPosition().y:0}).ToArray()});Save(false,null);Clean();yield return new WaitForSecondsRealtime(2);
}
}
}
}



