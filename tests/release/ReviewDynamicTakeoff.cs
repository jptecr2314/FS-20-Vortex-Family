using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
namespace FS20Validation {
 public static partial class CompareVortexAirframes {
  [Serializable] class DynRequest {public float fuelFraction,headwind,availableRun,density=1.167f;public bool afterburner=true,actualMassProperties;public float actualMass,actualPitchInertia;public Vector3 actualCenter;public DynamicStore[] stores=new DynamicStore[0];}
  [Serializable] class DynPlan {public string status,reason,rejectedSchedules;public bool supported,predictedFits;public float mass,pitchInertia,rotationSpeed,groundRoll,screenDistance,requiredRun,availableRun;public Vector3 center;}
  [Serializable] class DynCase {public string variant,loadout;public float fuel,wind,available;public bool afterburner;public DynamicStore[] stores;public DynPlan plan;}
  [Serializable] class DynSensitivity {public string variant;public DynPlan clean,drag,fore,aft;}
  [Serializable] class DynCursorSample {public float wind,roll,required;public bool supported,fits;}
  [Serializable] class DynCursorLimit {public string loadout;public float fuel,mass,firstFittingWind=-1;public bool monotonic=true;public List<DynCursorSample> samples=new List<DynCursorSample>();}
  [Serializable] class DynReport {public bool completed;public string pluginSha256,error;public int checks;public List<DynCase> cases=new List<DynCase>();public List<DynCursorLimit> cursorLimits=new List<DynCursorLimit>();public List<DynSensitivity> sensitivities=new List<DynSensitivity>();public string scope="Production embedded-grid calculator on native legal symmetric external mounts. Reduced longitudinal model, conservative external drag bound; no automatic launch veto or full mission/tire validation.";}
  public static void ReviewDynamicTakeoff(){
   output=Environment.GetEnvironmentVariable("FS20_TOOLS_OUTPUT");Directory.CreateDirectory(output);var report=new DynReport{pluginSha256=Hash(Environment.GetEnvironmentVariable("FS20_TOOLS_PLUGIN"))};var harmony=new Harmony("private.fs20.dynamic-takeoff");int exit=1;
   void Check(bool value,string text){report.checks++;if(!value)throw new Exception(text);}
   try{
    AppDomain.CurrentDomain.AssemblyResolve+=Resolve;plugin=LoadPlugin(Environment.GetEnvironmentVariable("FS20_TOOLS_PLUGIN"));
    harmony.PatchAll(P("Fs20TakeoffPlanner+NativePreflight"));Check(true,"Native preflight callback binds");
    string gridsPath=Environment.GetEnvironmentVariable("FS20_DYNAMIC_GRIDS");var db=JsonUtility.FromJson<DynamicDB>(File.ReadAllText(gridsPath));
    var method=P("Fs20DynamicTakeoff").GetMethod("Calculate",new[]{typeof(string),P("Fs20TakeoffRequest")});
    DynPlan Eval(string variant,DynRequest request){var r=JsonUtility.FromJson(JsonUtility.ToJson(request),P("Fs20TakeoffRequest"));return JsonUtility.FromJson<DynPlan>(JsonUtility.ToJson(method.Invoke(null,new[]{(object)variant,r})));}
    foreach(var a in db.aircraft){
     Check(a.loadouts.Count>1,"Native external stores not exported: "+a.variant);
     foreach(var load in a.loadouts)foreach(float fuel in new[]{.2f,.35f,.6f,1f})foreach(float wind in new[]{0f,10f,20f}) {
      var r=new DynRequest{fuelFraction=fuel,headwind=wind,availableRun=a.variant=="C"?61.3f:2000,stores=load.stores.ToArray()};
      var plan=Eval(a.variant,r);report.cases.Add(new DynCase{variant=a.variant,loadout=load.key,fuel=fuel,wind=wind,available=r.availableRun,afterburner=true,stores=r.stores,plan=plan});
      Check(!plan.predictedFits||(plan.supported&&plan.requiredRun<=r.availableRun),"Acceptance without runway reserve");
      Check(!plan.supported||(plan.mass>0&&float.IsFinite(plan.groundRoll)&&plan.screenDistance>=plan.groundRoll),"Invalid modeled distance");
      if(plan.mass>0)Check(Mathf.Abs(plan.mass-(a.grids.First(g=>Mathf.Abs(g.fuel-fuel)<.001f).mass+load.stores.Sum(s=>s.mass)))<.05f,"Store mass counted incorrectly");
      if(a.variant=="C"&&load.key=="clean"){
       r.afterburner=false;var dry=Eval(a.variant,r);report.cases.Add(new DynCase{variant=a.variant,loadout=load.key,fuel=fuel,wind=wind,available=r.availableRun,afterburner=false,stores=r.stores,plan=dry});
       Check(!dry.predictedFits||dry.requiredRun<=r.availableRun,"Dry-thrust margin bypass");
      }
     }
     if(a.variant=="C")foreach(var load in a.loadouts)foreach(float fuel in new[]{.2f,.35f,.6f,1f}) {
      var limit=new DynCursorLimit{loadout=load.key,fuel=fuel};bool seenFit=false;
      for(int wind=0;wind<=20;wind++) {
       var plan=Eval(a.variant,new DynRequest{fuelFraction=fuel,headwind=wind,availableRun=61.3f,stores=load.stores.ToArray()});
       limit.mass=plan.mass;limit.samples.Add(new DynCursorSample{wind=wind,roll=plan.groundRoll,required=plan.requiredRun,supported=plan.supported,fits=plan.predictedFits});
       if(plan.predictedFits&&!seenFit){seenFit=true;limit.firstFittingWind=wind;}
       if(seenFit&&!plan.predictedFits)limit.monotonic=false;
       Check(!plan.predictedFits||(plan.supported&&plan.requiredRun<=61.3f),"Cursor boundary bypasses reserve");
      }
      report.cursorLimits.Add(limit);
     }
     var clean=new DynRequest{fuelFraction=.35f,headwind=10,availableRun=2000};var baseline=Eval(a.variant,clean);
     Check(baseline.supported,"Clean reference failed");
     clean.availableRun=1;Check(!Eval(a.variant,clean).predictedFits,"Tiny runway accepted");clean.availableRun=2000;
     clean.fuelFraction=.1f;Check(!Eval(a.variant,clean).supported,"Extrapolation below fuel envelope");clean.fuelFraction=.5f;Check(Eval(a.variant,clean).mass>baseline.mass,"Fuel interpolation does not change mass");clean.fuelFraction=.35f;
     clean.stores=new[]{new DynamicStore{key="synthetic asymmetric sensitivity",mass=1000,position=baseline.center+Vector3.right*3}};Check(!Eval(a.variant,clean).supported,"Asymmetry silently accepted");
     clean.stores=new[]{new DynamicStore{key="synthetic fore CG sensitivity",mass=1000,position=baseline.center+Vector3.forward*2}};var fore=Eval(a.variant,clean);
     clean.stores[0].position=baseline.center-Vector3.forward*2;var aft=Eval(a.variant,clean);Check(fore.center.z>aft.center.z,"CG not updated by store placement");Check(!fore.supported||!aft.supported||Mathf.Abs(fore.groundRoll-aft.groundRoll)>.01f,"CG has no dynamic effect");
     clean.stores=new[]{new DynamicStore{key="synthetic drag sensitivity",mass=0,dragArea=1,position=baseline.center}};var drag=Eval(a.variant,clean);
     report.sensitivities.Add(new DynSensitivity{variant=a.variant,clean=baseline,drag=drag,fore=fore,aft=aft});
     // Ground roll alone is not monotonic with drag in a rotating aircraft:
     // attitude and the lift-off event change too. Retain the full trajectories'
     // endpoints for review; verify the high-drag limiting case separately.
     Check(!drag.supported||Mathf.Abs(drag.screenDistance-baseline.screenDistance)>.01f,"External drag has no dynamic effect");
     clean.stores[0].dragArea=100;var brake=Eval(a.variant,clean);Check(!brake.supported||brake.requiredRun>baseline.requiredRun,"Extreme drag does not limit launch");
     clean.actualMassProperties=true;clean.actualMass=baseline.mass+1000;clean.actualCenter=baseline.center;clean.actualPitchInertia=baseline.pitchInertia+1000;clean.stores=new[]{new DynamicStore{key="already included native store",mass=1000,position=baseline.center}};
     Check(Mathf.Abs(Eval(a.variant,clean).mass-clean.actualMass)<.01f,"Native body mass double-counts payload");clean.actualMass=18000;Check(!Eval(a.variant,clean).supported,"Overweight request accepted");
     var edge=new DynRequest{fuelFraction=.35f,availableRun=2000,headwind=10};
     edge.density=.95f;Check(Eval(a.variant,edge).supported,"Low-density domain edge failed");edge.density=1.25f;Check(Eval(a.variant,edge).supported,"High-density domain edge failed");
     edge.headwind=-1;Check(!Eval(a.variant,edge).supported,"Tailwind silently accepted");edge.headwind=21;Check(!Eval(a.variant,edge).supported,"Wind extrapolation accepted");edge.headwind=10;edge.density=float.NaN;Check(!Eval(a.variant,edge).supported,"NaN density accepted");
    }
    report.completed=true;exit=0;
   }catch(Exception e){while(e is System.Reflection.TargetInvocationException&&e.InnerException!=null)e=e.InnerException;report.error=e.ToString();Debug.LogException(e);}
   finally{harmony.UnpatchSelf();File.WriteAllText(Path.Combine(output,"dynamic-takeoff.json"),JsonUtility.ToJson(report,true));}EditorApplication.Exit(exit);
  }
 }
}
