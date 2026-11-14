using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NOAircraftTools;
using UnityEngine;
using Object=UnityEngine.Object;
namespace FS20Validation {
 public static partial class CompareVortexAirframes {
  [Serializable] class TOReport {public bool completed;public string error,pluginSha256;public string scope="Reduced longitudinal ground-pivot/free-flight model, production-derived geometry and NOAircraftTools force grids. Native dry/additive afterburner thrust, vectoring sign/gain, gear geometry and aerodynamic pitch torque. Not the native tire/contact solver or pilot takeoff state. Distances are model results, not measured live-game minima.";public List<TOFrame> aircraft=new List<TOFrame>();public float cursorDeckLength;public string cursorReference;}
  [Serializable] class TOFrame {public string variant,tailMesh;public float dryThrust,afterburnerThrust,vectorPitch,vectorGain,gearGroundY,tailAngle;public Vector3 pivot,enginePoint;public List<Vector3> wheelContacts=new List<Vector3>();public List<TOCase> cases=new List<TOCase>();}
  [Serializable] class TOCase {public float fuel,mass,headwind,rotationSpeed,dt,rollingMu;public bool afterburner,lifted,cleared,tailstrike;public float groundRoll,clearanceDistance,liftoffSpeed,rotationStart,time,alphaPeak;public string failure;}
  static TOReport takeoffReport;
  [Serializable] class DynamicDB {public int version=2;public List<DynamicFrame> aircraft=new List<DynamicFrame>();}
  [Serializable] class DynamicFrame {public string variant;public float dryThrust,afterburnerThrust,vectorPitch,vectorGain,tailAngle,referenceDensity=1.167f;public Vector3 pivot,enginePoint,noseContact;public float pitchP,pitchD,pitchI,maxAlpha;public List<DynamicGrid> grids=new List<DynamicGrid>();public List<DynamicLoadout> loadouts=new List<DynamicLoadout>();}
  [Serializable] class DynamicGrid {public float fuel,mass,inertia,damping;public Vector3 center;public Vector3[] cells;}
  [Serializable] class DynamicLoadout {public string key;public List<DynamicStore> stores=new List<DynamicStore>();}
  [Serializable] class DynamicStore {public string key;public float mass,dragArea;public Vector3 position;public int groupIndex;public int[] precludes;}
  static DynamicDB dynamicDB;
  class TOGrid {public Vector3[,,] values=new Vector3[7,23,9];public float mass,inertia,damping; public Vector3 Sample(float v,float a,float p){float sv=Mathf.Clamp((v-20)/20,0,6),sa=Mathf.Clamp((a+12)/2,0,22),sp=Mathf.Clamp((p+1)*4,0,8);int i=Mathf.Min(5,(int)sv),j=Mathf.Min(21,(int)sa),k=Mathf.Min(7,(int)sp);Vector3 At(int n)=>Vector3.Lerp(Vector3.Lerp(values[n,j,k],values[n,j,k+1],sp-k),Vector3.Lerp(values[n,j+1,k],values[n,j+1,k+1],sp-k),sa-j);var f=Vector3.Lerp(At(i),At(i+1),sv-i);return v<20?f*(v*v/400):f;}}
  static void InitTakeoffReport(){takeoffReport=new TOReport{pluginSha256=Hash(Environment.GetEnvironmentVariable("FS20_TOOLS_PLUGIN"))};
   try{var def=UnityEditor.AssetDatabase.LoadAssetAtPath<Object>("Assets/Blueprinter/_donotship/MonoBehaviour/SmallCarrier1_PLACEHOLDER.asset");var root=(GameObject)Member(def,"unitPrefab");var ab=root.GetComponentInChildren(Utilities.game_type("Airbase"),true);foreach(var r in (IEnumerable)Member(ab,"runways")){var start=(Transform)Member(r,"Start");var end=(Transform)Member(r,"End");if(start!=null&&end!=null)takeoffReport.cursorDeckLength=Mathf.Max(takeoffReport.cursorDeckLength,Vector3.Distance(start.position,end.position));}if(takeoffReport.cursorDeckLength<=1)throw new Exception("Imported runway endpoints have no valid separation");takeoffReport.cursorReference="Exported native runway endpoint separation; not necessarily usable takeoff roll. User fuel, stores, wind and ship velocity unknown.";}catch(Exception e){takeoffReport.cursorDeckLength=0;takeoffReport.cursorReference="Native deck references unavailable: "+e.Message+". User observation remains qualitative; no deck length assumed.";}}
  static void FlushTakeoffReport(string error,bool completed){if(takeoffReport==null)return;takeoffReport.error=error;takeoffReport.completed=completed;File.WriteAllText(Path.Combine(output,"takeoff-distances.json"),JsonUtility.ToJson(takeoffReport,true));if(dynamicDB!=null&&completed)File.WriteAllText(Path.Combine(output,"dynamic-grids.json"),JsonUtility.ToJson(dynamicDB));}
  static void MeasureTakeoff(AircraftData data,GameObject root,string variant,bool delta){
   var row=new TOFrame{variant=variant};takeoffReport.aircraft.Add(row);
   var eng=root.GetComponentInChildren(Utilities.game_type("Turbojet"),true);row.dryThrust=(float)Member(eng,"maxThrust");row.vectorPitch=((Vector3)Member(eng,"thrustVectoring")).x;row.vectorGain=((Vector3)Member(eng,"thrustVectoringGain")).x;
   row.enginePoint=root.transform.InverseTransformPoint(eng.transform.position);
   var nozzle=root.GetComponentInChildren(Utilities.game_type("JetNozzle"),true);foreach(var b in (IEnumerable)Member(nozzle,"afterburners"))row.afterburnerThrust+=(float)Member(b,"thrust");
   foreach(Component gear in root.GetComponentsInChildren(Utilities.game_type("LandingGear"),true)){float radius=(float)Member(gear,"wheelRadius");foreach(Transform wheel in (IEnumerable)Member(gear,"wheels")){var point=root.transform.InverseTransformPoint(wheel.position);point.y-=radius;row.wheelContacts.Add(point);}}
   if(row.wheelContacts.Count<3)throw new Exception(variant+": native gear contact references missing");
   float aft=row.wheelContacts.Min(w=>w.z);row.gearGroundY=row.wheelContacts.Where(w=>Mathf.Abs(w.z-aft)<.1f).Average(w=>w.y);row.pivot=new Vector3(0,row.gearGroundY,aft);
   row.tailAngle=22;
   foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)){var renderer=mf.GetComponent<Renderer>();bool visible=renderer!=null&&renderer.enabled;for(var node=mf.transform;node!=null&&node!=root.transform;node=node.parent)visible&=node.gameObject.activeSelf;if(mf.name=="afterburner_flame"||mf.GetComponent<ParticleSystem>()!=null)visible=false;if(!visible||mf.sharedMesh==null||mf.GetComponentInParent(Utilities.game_type("LandingGear"),true)!=null)continue;foreach(var vertex in mf.sharedMesh.vertices){var p=root.transform.InverseTransformPoint(mf.transform.TransformPoint(vertex))-row.pivot;if(p.z>=-.5f)continue;for(float angle=.5f;angle<row.tailAngle;angle+=.5f){float a=angle*Mathf.Deg2Rad;if(p.y*Mathf.Cos(a)+p.z*Mathf.Sin(a)<.02f){row.tailAngle=angle;row.tailMesh=mf.name;break;}}}}
   if(row.tailAngle<3)throw new Exception(variant+": imported gear pose cannot support ground-pivot model; tail clearance="+row.tailAngle);
   bool exporting=Environment.GetEnvironmentVariable("FS20_DYNAMIC_EXPORT")=="1";
   DynamicFrame df=null;if(exporting){if(dynamicDB==null)dynamicDB=new DynamicDB();df=new DynamicFrame{variant=variant,dryThrust=row.dryThrust,afterburnerThrust=row.afterburnerThrust,vectorPitch=row.vectorPitch,vectorGain=row.vectorGain,tailAngle=row.tailAngle,pivot=row.pivot,enginePoint=row.enginePoint,noseContact=row.wheelContacts.OrderByDescending(w=>w.z).First()};dynamicDB.aircraft.Add(df);df.loadouts=NativeLoadouts(root);var cf=root.GetComponentInChildren(NOAircraftTools.Utilities.game_type("ControlsFilter"),true);var fb=cf.GetType().GetMethod("GetFlyByWire").Invoke(cf,null);df.pitchP=(float)Member(fb,"pFactorFast");df.pitchD=(float)Member(fb,"dFactorFast");df.pitchI=(float)Member(fb,"iFactor");df.maxAlpha=(float)Member(fb,"alphaLimiter");}
   foreach(float fuel in exporting?new[]{.2f,.35f,.6f,1f}:new[]{.2f,.35f,1f}){
    data.set_fuel_fraction(fuel);var grid=new TOGrid{mass=data.physics.current_mass};
    for(int s=0;s<7;s++)for(int a=0;a<23;a++)for(int p=0;p<9;p++){var f=TakeoffForce(data,variant,delta,20+s*20,a*2-12,-1+p*.25f);grid.values[s,a,p]=new Vector3(f.total.y,f.total.z,f.torque.x);}
    foreach(var part in data.parts){var r=part.world_cg-data.physics.current_cg;grid.inertia+=part.current_physics.mass*(r.y*r.y+r.z*r.z+.2f);}
    var all=data.parts;float plus=0,minus=0;TakeoffForce(data,variant,delta,80,8,0);
    foreach(var part in all){data.parts=new[]{part};var r=part.world_cg-data.physics.current_cg;foreach(float q in new[]{-.1f,.1f}){var v=Simulation.velocity_vector(8*Mathf.Deg2Rad,0)*80+Vector3.Cross(Vector3.right*q,r);var st=new AircraftState(v.magnitude,500,-Mathf.Atan2(v.y,v.z)*Mathf.Rad2Deg,0);float torque=Simulation.aircraft_forces(data,st).torque.x;if(q>0)plus+=torque;else minus+=torque;}}
    data.parts=all;grid.damping=(plus-minus)/.2f;
    if(exporting){var cells=new Vector3[7*23*9];for(int s=0;s<7;s++)for(int a=0;a<23;a++)for(int p=0;p<9;p++)cells[(s*23+a)*9+p]=grid.values[s,a,p];df.grids.Add(new DynamicGrid{fuel=fuel,mass=grid.mass,center=data.physics.current_cg,inertia=grid.inertia,damping=grid.damping,cells=cells});continue;}
    foreach(bool ab in new[]{false,true})foreach(float wind in new[]{0f,10f,20f})foreach(float vr in new[]{25f,40f,55f,70f})row.cases.Add(TakeoffLoop(data,grid,row,fuel,ab,wind,vr,.02f,.025f));
    // Numerical/rolling-resistance sensitivity, not a tuned fit to the Cursor.
    row.cases.Add(TakeoffLoop(data,grid,row,fuel,true,0,55,.01f,.025f));row.cases.Add(TakeoffLoop(data,grid,row,fuel,true,0,55,.02f,.04f));
   }
   Debug.Log("TAKEOFF_MEASURED "+variant+" cases="+row.cases.Count+" tail="+row.tailAngle);
  }
  static List<DynamicLoadout> NativeLoadouts(GameObject root){
   var result=new List<DynamicLoadout>{new DynamicLoadout{key="clean"}};
   var manager=root.GetComponent(Utilities.game_type("Aircraft")).GetType().GetField("weaponManager",Flags).GetValue(root.GetComponent(Utilities.game_type("Aircraft")));
   var choices=new Dictionary<string,List<DynamicStore>>();
   int groupIndex=-1;
   foreach(var group in (IEnumerable)Member(manager,"hardpointSets")){groupIndex++;foreach(var mount in (IEnumerable)Member(group,"weaponOptions")){
    if(mount==null)continue;var info=Member(mount,"info");if(info==null)continue;float mass=(float)Member(mount,"emptyMass")+(float)Member(info,"massPerRound")*(int)Member(mount,"ammo");float drag=(float)Member(mount,"drag");
    if(mass<=0||drag<=0||(bool)Member(mount,"disabled"))continue;
    string key=(string)Member(mount,"jsonKey");
    foreach(var hp in (IEnumerable)Member(group,"hardpoints")){var doors=Member(hp,"bayDoors") as Array;if(doors!=null&&doors.Length>0)continue;var node=(Transform)Member(hp,"transform");if(node==null)continue;
     if(!choices.TryGetValue(key,out var list)){list=new List<DynamicStore>();choices.Add(key,list);}var point=root.transform.InverseTransformPoint(node.position);
     if(!list.Any(s=>(s.position-point).sqrMagnitude<.01f))list.Add(new DynamicStore{key=key,mass=mass,dragArea=drag,position=point,groupIndex=groupIndex,precludes=((IEnumerable)Member(group,"precludingHardpointSets")).Cast<int>().ToArray()});
    }
   }}
   foreach(var pair in choices){var left=pair.Value.Where(s=>s.position.x<-.1f).OrderBy(s=>Mathf.Abs(s.position.x)).FirstOrDefault();
    var right=left==null?null:pair.Value.FirstOrDefault(s=>s.position.x>.1f&&Mathf.Abs(s.position.x+left.position.x)<.1f&&Mathf.Abs(s.position.z-left.position.z)<.1f&&
        (s.groupIndex==left.groupIndex||(!s.precludes.Contains(left.groupIndex)&&!left.precludes.Contains(s.groupIndex))));
    if(left!=null&&right!=null)result.Add(new DynamicLoadout{key="paired:"+pair.Key,stores=new List<DynamicStore>{left,right}});
   }
   return result;
  }
  static Simulation.SimResults TakeoffForce(AircraftData data,string variant,bool delta,float speed,float alpha,float pitch){
   if(variant!="C")return Forces(data,delta,speed,alpha,pitch,0);
   var state=new AircraftState(speed,500,alpha,0);data.update(state,new NOAircraftTools.ControlInputs{pitch=pitch,throttle=1});
   float gain=(float)Call("Fs20FlightEnvelope","SurfaceGain",speed*Mathf.Sqrt(Simulation.isa_atm(500).rho/1.225f));
   foreach(var surface in data.control_surfaces){var marker=surface.surface_object.GetComponent(P("Fs20Elevon"));if(marker==null)continue;float flap=15*(float)Member(marker,"FlapFraction")*(1-Mathf.InverseLerp(100,190,speed));float angle=(float)Call("Fs20FlightEnvelope","ElevonAngle",pitch,0f,Member(marker,"PitchRange"),Member(marker,"RollRange"),gain,flap);surface.visible_mesh.transform.localRotation=surface.resting_rotation*Quaternion.AngleAxis(angle,Vector3.right);}
   data.physics.recalculate();return Simulation.aircraft_forces(data,state);
  }
  static TOCase TakeoffLoop(AircraftData data,TOGrid g,TOFrame frame,float fuel,bool ab,float wind,float vr,float dt,float mu){
   var row=new TOCase{fuel=fuel,mass=g.mass,afterburner=ab,headwind=wind,rotationSpeed=vr,dt=dt,rollingMu=mu,rotationStart=-1};
   var nose=frame.wheelContacts.OrderByDescending(w=>w.z).First();
   float initialTheta=-Mathf.Atan2(nose.y-frame.pivot.y,nose.z-frame.pivot.z);
   float x=0,y=0,vx=0,vy=0,theta=initialTheta,q=0,input=0;bool flying=false;
   var cg=data.physics.current_cg;var arm=cg-frame.pivot;float ip=g.inertia+g.mass*(arm.y*arm.y+arm.z*arm.z);float targetAngle=Mathf.Min(12,frame.tailAngle-1)*Mathf.Deg2Rad;
   for(float t=0;t<90;t+=dt){float airX=vx+wind,speed=Mathf.Sqrt(airX*airX+vy*vy),path=flying?Mathf.Atan2(vy,Mathf.Max(.1f,airX)):0,alpha=(theta-path)*Mathf.Rad2Deg;row.alphaPeak=Mathf.Max(row.alphaPeak,alpha);
    float desired=airX>=vr?Mathf.Clamp((targetAngle-theta)*2,-.25f,.25f):0;
    if(airX>=vr && row.rotationStart<0)row.rotationStart=x;
    float request=airX>=vr?Mathf.Clamp(input-(desired-q)*dt*4,-1,1):0;input=Mathf.MoveTowards(input,request,2.25f*dt);
    Vector3 f=g.Sample(speed,alpha,input);
    float thrust=frame.dryThrust+(ab?frame.afterburnerThrust:0);
    float vector=-Mathf.Clamp(input*frame.vectorGain,-1,1)*frame.vectorPitch*Mathf.Deg2Rad;
    var thrustBody=new Vector3(0,-Mathf.Sin(vector)*thrust,Mathf.Cos(vector)*thrust);
    var rotation=Quaternion.Euler(-theta*Mathf.Rad2Deg,0,0);var aero=rotation*new Vector3(0,f.x,f.y);var tv=rotation*thrustBody;
    float my=-f.z+g.damping*q*(speed*speed/6400)-Vector3.Cross(rotation*(frame.enginePoint-cg),tv).x;
    float fy=aero.y+tv.y-g.mass*9.81f,fx=aero.z+tv.z;
    float normal=0;
    if(!flying){var rw=rotation*arm;
     // The wheel pivot translates. The m*ax term cancels the net horizontal
     // force's apparent moment; only rolling resistance contributes here.
     float qa=(my+rw.z*fy-rw.y*mu*Mathf.Max(0,-fy))/ip;
     if(theta<=initialTheta && qa<0){qa=0;q=0;}q+=qa*dt;theta=Mathf.Max(initialTheta,theta+q*dt);
     y=rw.y;
     normal=g.mass*(qa*rw.z-q*q*rw.y)-fy;
     if(theta>frame.tailAngle*Mathf.Deg2Rad){row.tailstrike=true;row.failure="tailstrike";break;}
     if(normal<=0 && airX>10){flying=true;row.lifted=true;row.groundRoll=x;row.liftoffSpeed=speed;vy=q*rw.z;}
    }else{q+=my/g.inertia*dt;theta+=q*dt;vy+=fy/g.mass*dt;y+=vy*dt;}
    vx+=Mathf.Max(-5,(fx-mu*Mathf.Max(0,normal))/g.mass)*dt;vx=Mathf.Max(0,vx);x+=vx*dt;row.time=t;
    if(!float.IsFinite(theta)||Mathf.Abs(q)>3||Mathf.Abs(theta)>1){row.failure="rotation divergence";break;}
    float wheelClearance=y-(rotation*arm).y;
    if(flying&&wheelClearance<-.1f){row.failure="settled back after liftoff";break;}
    if(flying&&wheelClearance>=10.7f){row.cleared=true;row.clearanceDistance=x;break;}
   }
   if(!row.cleared&&row.failure==null)row.failure="no 10.7 m clearance within 90 s";return row;
  }
 }
}


