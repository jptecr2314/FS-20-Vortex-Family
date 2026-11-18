using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FS20PrivateControlBench
{
    [BepInPlugin("jp.fs20.private-ai-control-bench","FS-20 private AI control bench","0.1.0")]
    public sealed class NativeFs20AiControlBench:BaseUnityPlugin
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static object Get(object value,string name)=>AccessTools.Field(value.GetType(),name)?.GetValue(value);
        string output; bool driving; int timeScaleResets; readonly List<object> results=new List<object>();
        // A private batch process must retain its controlled simulation clock.
        // This never writes the user's normal game profile or process.
        void LateUpdate(){if(driving && Mathf.Abs(Time.timeScale-4f)>.001f){timeScaleResets++;Time.timeScale=4;}}
        IEnumerator Start() {
            if(Environment.GetEnvironmentVariable("FS20_AI_NATIVE")!="1"||Application.productName!="FS20AIControl"){enabled=false;yield break;}
            output=Environment.GetEnvironmentVariable("FS20_AI_NATIVE_OUTPUT");
            if(!Path.IsPathRooted(output??"")){enabled=false;yield break;}
            Directory.CreateDirectory(output);Application.runInBackground=true;Application.targetFrameRate=60;
            new Harmony("jp.fs20.private-ai-pid-witness").PatchAll(typeof(PidWitness));
            var work=Run();
            while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){Save(false,e.ToString());Application.Quit(1);yield break;}yield return next;}
            Save(true,null);Application.Quit(0);
        }
        void Save(bool complete,string error)=>File.WriteAllText(Path.Combine(output,"native-flight.json"),JsonConvert.SerializeObject(new {
            completed=complete,error,product=Application.productName,
            scope="Native game free rigidbody/aerodynamic/actuator/FBW simulation, controlled native Autopilot.AutoAim route and speed-hold test state. Actual mission combat decisions, weapons, ground departure, VTOL transition and landing contact are not exercised.",
            timeScaleResets,cases=results},Formatting.Indented));
        void Stage(string text){File.AppendAllText(Path.Combine(output,"stages.log"),Time.realtimeSinceStartup.ToString("F2")+" "+text+"\n");}
        IEnumerator Run() {
            Stage("Waiting for native game initialization");
            float deadline=Time.realtimeSinceStartup+360;
            var api=AccessTools.TypeByName("Blueprinter.Api");
            while(api!=null&&api.GetProperty("CurrentState",Flags)?.GetValue(null)?.ToString()!="Ready"&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
            if(api!=null&&api.GetProperty("CurrentState",Flags)?.GetValue(null)?.ToString()!="Ready")throw new Exception("Private Blueprinter initialization timed out");
            Stage("Native initialization ready; waiting for FS-20 registration");
            deadline=Mathf.Min(deadline,Time.realtimeSinceStartup+15);
            while((Encyclopedia.Lookup==null||!Encyclopedia.Lookup.ContainsKey("JP_FS20_NA"))&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
            if(Encyclopedia.Lookup==null||!Encyclopedia.Lookup.ContainsKey("JP_FS20_NA"))throw new Exception("Tested FS-20 family did not register");
            var parserType=AccessTools.TypeByName("NuclearOption.BuildScripts.CommandLineArgParser");
            var parser=Object.FindObjectOfType(parserType);
            parserType.GetField("missionName",Flags).SetValue(parser,"FS20NA Flight Test");
            parserType.GetField("port",Flags).SetValue(parser,28998);
            parserType.GetField("state",Flags).SetValue(parser,GameState.SinglePlayer);
            var host=parserType.GetMethod("parseAutoHost",Flags);
            Stage("Starting offline mission; current state="+GameManager.gameState+"; data="+Application.persistentDataPath);
            if(GameManager.gameState!=GameState.SinglePlayer)host.Invoke(parser,new[]{Activator.CreateInstance(host.GetParameters()[0].ParameterType)});
            deadline=Time.realtimeSinceStartup+120;
            while((GameManager.gameState!=GameState.SinglePlayer||!Object.FindObjectsOfType<Airbase>().Any(b=>b.CurrentHQ))&&Time.realtimeSinceStartup<deadline)yield return new WaitForSecondsRealtime(.5f);
            if(GameManager.gameState!=GameState.SinglePlayer)throw new Exception("Private offline mission did not start");
            Stage("Offline mission ready");
            yield return new WaitForSecondsRealtime(5);
            var reference=Object.FindObjectsOfType<Airbase>().First(b=>b.CurrentHQ&&!b.CurrentHQ.preventJoin);
            var spawn=Object.FindObjectOfType<Spawner>();
            string variants=Environment.GetEnvironmentVariable("FS20_AI_NATIVE_VARIANTS")??"B,A,C,T,NA";
            var fuels=(Environment.GetEnvironmentVariable("FS20_AI_NATIVE_FUELS")??"0.35,1").Split(',').Select(f=>float.Parse(f,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if(fuels.Length==0||fuels.Any(f=>f<.1f||f>1f))throw new Exception("Private fuel sweep outside bounded domain");
            foreach(string variant in variants.Split(','))foreach(float fuel in fuels) {
                Time.timeScale=1;
                string key=variant=="B"?"SmallFighter1":variant=="C"?"JP_FS20_CTOL":"JP_FS20_"+variant;
                var definition=(AircraftDefinition)Encyclopedia.Lookup[key];
                var load=new NuclearOption.SavedMission.Loadout();
                foreach(var station in definition.unitPrefab.GetComponent<Aircraft>().weaponManager.hardpointSets)load.weapons.Add(null);
                var origin=reference.transform.GlobalPosition()+new Vector3(1500,1800,1000);
                float speed=float.Parse(Environment.GetEnvironmentVariable("FS20_AI_NATIVE_SPEED")??"180",System.Globalization.CultureInfo.InvariantCulture);
                var plane=spawn.SpawnAircraft(null,definition.unitPrefab,load,fuel,default(LiveryKey),origin,Quaternion.Euler(-3,0,0),Vector3.forward*speed,null,reference.CurrentHQ,"AI control bench",1,.5f);
                Stage("Spawned "+variant+" fuel="+fuel);
                if(!plane)throw new Exception("Native aircraft spawn failed: "+variant);
                plane.SetGear(LandingGear.GearState.LockedRetracted);
                if(!plane.Ignition)plane.CmdToggleIgnition();
                yield return new WaitForSecondsRealtime(.5f);
                var parts=plane.GetComponentsInChildren<UnitPart>(true);
                var pilots=plane.pilots.Where(p=>p).ToArray();
                if(pilots.Length==0)throw new Exception("Native AI pilot unavailable: "+variant);
                float actualFuel=plane.GetFuelLevel();if(Mathf.Abs(actualFuel-fuel)>.03f)throw new Exception("Native fuel initialization differs from requested fuel fraction");
                float gain=float.Parse(Environment.GetEnvironmentVariable("FS20_AI_NATIVE_GAIN")??"1",System.Globalization.CultureInfo.InvariantCulture);
                if(variant!="B" && gain!=1f){
                    var forward=AccessTools.Field(typeof(Autopilot),"forwardFlightController").GetValue(plane.autopilot);
                    foreach(string axis in new[]{"pitchFlightPID","rollFlightPID"}){
                        var factors=AccessTools.Field(forward.GetType(),axis).GetValue(forward);
                        var pid=AccessTools.Field(factors.GetType(),"PID");
                        pid.SetValue(factors,(Vector3)pid.GetValue(factors)*gain);
                    }
                    AccessTools.Method(forward.GetType(),"Initialize").Invoke(forward,null);
                }
                var driver=new BenchState(output,variant,fuel,origin,speed);
                bool? gainContract=null;
                if(PolicyAvailable()){
                    bool saved=pilots[0].playerControlled;
                    try{
                        pilots[0].playerControlled=false;
                        Vector2 value=Policy(plane);
                        bool tuned=policyMethod.ReturnType==typeof(Vector2)?variant!="B"&&variant!="T":variant=="NA";
                        gainContract=tuned?value!=Vector2.one&&value.x>0f&&value.y>0f:value==Vector2.one;
                        pilots[0].playerControlled=true;
                        gainContract=gainContract.Value&&Policy(plane)==Vector2.one&&Policy(null)==Vector2.one;
                    }finally{pilots[0].playerControlled=saved;}
                    if(gainContract!=true)throw new Exception("Native AI/manual gain guard contract failed: "+variant);
                }
                pilots[0].SwitchState(driver);
                driving=true;Time.timeScale=4;
                while(!driver.finished&&plane&&pilots[0]&&pilots[0].currentState==driver&&Time.realtimeSinceStartup-driver.realStart<90)yield return new WaitForSecondsRealtime(.2f);
                driving=false;Time.timeScale=1;
                if(!driver.finished){driver.failed=true;driver.failure="Pilot/airframe stopped delivering guidance before the controlled route completed";}
                driver.Close();
                results.Add(new {variant,fuel,driver.finished,driver.samples,driver.failed,driver.failure,
                    meanHeadingError=driver.headingSum/Math.Max(1,driver.samples),meanAltitudeError=driver.altitudeSum/Math.Max(1,driver.samples),
                    maximumHeadingError=driver.maxHeading,maximumAltitudeError=driver.maxAltitude,
                    minimumSpeed=driver.minSpeed,maximumAoA=driver.maxAoA,maximumRollRate=driver.maxRollRate,
                    actualInitialFuel=actualFuel,mass=plane?plane.GetMass():0,experimentalGainScale=gain,gainContract,driver.pidChecks,
                    aiCornerMps=definition.aircraftParameters.cornerSpeed,aiTurningRadiusM=definition.aircraftParameters.turningRadius,
                    states="controlled native guidance",physics="native"});
                Save(false,null);
                if(plane)Object.Destroy(plane.gameObject);
                foreach(var part in parts)if(part)Object.Destroy(part.gameObject);
                yield return new WaitForSecondsRealtime(.5f);
            }
        }
        static bool policyQueried;
        static MethodInfo policyMethod;
        static bool PolicyAvailable(){
            if(!policyQueried){
                policyQueried=true;var type=AccessTools.TypeByName("Japensou.NuclearOption.VortexConventional.Fs20AiGuidance");
                if(type!=null)policyMethod=AccessTools.Method(type,"Gains")??AccessTools.Method(type,"Gain");
            }
            return policyMethod!=null;
        }
        static Vector2 Policy(Aircraft plane){
            if(!PolicyAvailable())return Vector2.one;
            var result=policyMethod.Invoke(null,new object[]{plane});
            return result is Vector2 axes?axes:Vector2.one*(float)result;
        }
        static BenchState activeDriver;
        [HarmonyPatch(typeof(AeroPID),nameof(AeroPID.GetOutputClampP))]
        static class PidWitness {
            [HarmonyPriority(Priority.Last)]
            static void Postfix(AeroPID __instance,float __1,float __3,float __result){activeDriver?.CheckPid(__instance,__3,__1,__result);}
        }
        sealed class BenchState:PilotBaseState {
            readonly GlobalPosition origin;readonly float wantedSpeed;readonly string variant;
            readonly float fuel; StreamWriter csv;float began,nextSample;
            readonly float bankAllowed=float.Parse(Environment.GetEnvironmentVariable("FS20_AI_NATIVE_BANK")??"45",System.Globalization.CultureInfo.InvariantCulture);
            readonly float lookahead=float.Parse(Environment.GetEnvironmentVariable("FS20_AI_NATIVE_LOOKAHEAD")??"8000",System.Globalization.CultureInfo.InvariantCulture);
            readonly bool aimVelocity=Environment.GetEnvironmentVariable("FS20_AI_NATIVE_AIM_NOSE")!="1";
            public readonly float realStart=Time.realtimeSinceStartup;
            public bool finished,failed;public string failure;public int samples;
            public float headingSum,altitudeSum,maxHeading,maxAltitude,minSpeed=float.PositiveInfinity,maxAoA,maxRollRate;
            AeroPID pitchPid,rollPid;bool pitchChecked,rollChecked;public int pidChecks;
            public BenchState(string output,string variant,float fuel,GlobalPosition origin,float speed){this.variant=variant;this.fuel=fuel;this.origin=origin;wantedSpeed=speed;csv=new StreamWriter(Path.Combine(output,variant+"-fuel"+fuel.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".csv"));csv.AutoFlush=true;csv.WriteLine("time_s,phase,heading_error_deg,height_error_m,speed_mps,aoa_deg,roll_rate_deg_s,pitch,roll,yaw,throttle,nozzle_deg,bank_deg,height_delta_m");}
            public override void EnterState(Pilot p){pilot=p;Initialize(p);stateDisplayName="Private native AI control test";aircraft.SetFlightAssist(true);began=Time.time;
                var forward=AccessTools.Field(typeof(Autopilot),"forwardFlightController").GetValue(aircraft.autopilot);
                pitchPid=(AeroPID)AccessTools.Field(forward.GetType(),"xPID").GetValue(forward);rollPid=(AeroPID)AccessTools.Field(forward.GetType(),"zPID").GetValue(forward);activeDriver=this;}
            public override void UpdateState(Pilot p){}
            public override void LeaveState(){Close();}
            public void Close(){if(activeDriver==this)activeDriver=null;csv?.Dispose();csv=null;}
            public void CheckPid(AeroPID pid,float speed,float clampP,float actual){
                bool x=ReferenceEquals(pid,pitchPid),z=ReferenceEquals(pid,rollPid);
                if((!x&&!z)||(x&&pitchChecked)||(z&&rollChecked))return;
                var factors=Get(pid,"factors");var gains=(Vector3)Get(factors,"PID");
                float reference=(float)Get(pid,"referenceAirspeed");
                float expected=Mathf.Clamp((float)Get(pid,"p"),-clampP,clampP)*gains.x+(float)Get(pid,"i")*gains.y+(float)Get(pid,"d")*gains.z*Mathf.Clamp(reference*reference/Mathf.Max(speed*speed,10f),0f,4f);
                if(Mathf.Abs(expected)<.02f)return;
                var gainsPolicy=Policy(aircraft);
                float scale=x?gainsPolicy.x:gainsPolicy.y;
                if(Mathf.Abs(actual-expected*scale)>.0002f){failed=finished=true;failure="Native attitude PID output did not match the active production AI gain";}
                if(x)pitchChecked=true;if(z)rollChecked=true;pidChecks++;
            }
            public override void FixedUpdateState(Pilot p) {
                if(finished||!aircraft)return;
                float t=Time.time-began;
                if(t>=75){finished=true;return;}
                string phase=t<15?"level":t<35?"right turn":t<55?"left reversal":"climb";
                float heading=t<15?0:t<35?45:t<55?-45:0;
                float altitude=t<55?origin.y:origin.y+150;
                var direction=Quaternion.Euler(0,heading,0)*Vector3.forward;
                var target=aircraft.GlobalPosition()+direction*lookahead;target.y=altitude;
                // Controlled speed hold isolates guidance/FBW from native combat throttle decisions.
                controlInputs.throttle=Mathf.Clamp01(.45f+(wantedSpeed-aircraft.speed)*.025f);
                aircraft.autopilot.AutoAim(target,aimVelocity,false,false,.8f,bankAllowed,false,0,Vector3.zero);
                if(Time.time<nextSample)return;nextSample=Time.time+.1f;
                var velocity=aircraft.rb.velocity-aircraft.GetWindVelocity();var local=aircraft.transform.InverseTransformDirection(velocity);
                float h=Mathf.Atan2(velocity.x,velocity.z)*Mathf.Rad2Deg;
                float he=Mathf.Abs(Mathf.DeltaAngle(h,heading));float ae=Mathf.Abs(aircraft.GlobalPosition().y-altitude);
                float alpha=-Mathf.Atan2(local.y,Mathf.Max(1,local.z))*Mathf.Rad2Deg;
                float rollRate=Vector3.Dot(aircraft.CockpitRB().angularVelocity,aircraft.transform.forward)*Mathf.Rad2Deg;
                samples++;headingSum+=he;altitudeSum+=ae;maxHeading=Mathf.Max(maxHeading,he);maxAltitude=Mathf.Max(maxAltitude,ae);minSpeed=Mathf.Min(minSpeed,aircraft.speed);maxAoA=Mathf.Max(maxAoA,Mathf.Abs(alpha));maxRollRate=Mathf.Max(maxRollRate,Mathf.Abs(rollRate));
                var swivel=aircraft.GetComponent<SwivelDuctSystem>();
                string N(float value)=>value.ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
                csv?.WriteLine(string.Join(",",new[]{N(t),phase,N(he),N(ae),N(aircraft.speed),N(alpha),N(rollRate),N(controlInputs.pitch),N(controlInputs.roll),N(controlInputs.yaw),N(controlInputs.throttle),N(swivel?swivel.GetNozzleAngle():0),N(Mathf.DeltaAngle(0,aircraft.transform.eulerAngles.z)),N(aircraft.GlobalPosition().y-altitude)}));
                if(!float.IsFinite(alpha)||aircraft.speed<40||aircraft.GlobalPosition().y<Datum.LocalSeaY+100){failed=true;failure="Uncontrolled low speed, low altitude or nonfinite state";finished=true;}
            }
        }
    }
}
