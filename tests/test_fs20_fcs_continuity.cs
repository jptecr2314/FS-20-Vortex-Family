using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

// Executable behavior checks against the built candidate. No copied control laws
// or Unity stubs. This checks reference contracts, not free-flight dynamics.
class Fs20FcsContinuityTests
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    const string NS="Japensou.NuclearOption.VortexConventional.";
    static Assembly mod;static int checks;static StreamWriter trace;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static object New(string type)=>Activator.CreateInstance(mod.GetType(NS+type,true),true);
    static float Value(object o,string name)=>(float)o.GetType().GetProperty(name,F).GetValue(o);
    static bool Flag(object o,string name)=>(bool)o.GetType().GetProperty(name,F).GetValue(o);
    static object Step(object env,string variant,string scenario,int frame,float g=5,float alpha=10,bool on=true,float pitch=-1,float q=.2f,float mass=0,float dt=.02f,float speed=300){
        if(mass==0)mass=variant=="A"?13500:variant=="C"?12000:16000;
        var values=new Dictionary<string,object>{{"pitch",pitch},{"roll",1f},{"mass",mass},{"alpha",alpha},{"normalG",g},{"assisted",on},{"enabled",on},{"highAuthority",true},{"gearDown",false},{"speed",speed},{"density",1.225f},{"dt",dt},{"nativeRatePerG",9.81f/speed},{"pitchRateUp",q},{"gravityNormalG",1f},{"approachBlend",0f}};
        var method=env.GetType().GetMethod("Step",F);var args=method.GetParameters().Select(p=>values.ContainsKey(p.Name)?values[p.Name]:p.HasDefaultValue?p.DefaultValue:throw new Exception("Unknown required argument "+p.Name)).ToArray();method.Invoke(env,args);
        trace.WriteLine(string.Join(",",variant,scenario,frame,g.ToString(CultureInfo.InvariantCulture),alpha.ToString(CultureInfo.InvariantCulture),pitch.ToString(CultureInfo.InvariantCulture),Value(env,"Pitch").ToString("R",CultureInfo.InvariantCulture),Value(env,"LimitG").ToString("R",CultureInfo.InvariantCulture),Flag(env,"HardProtection")));
        return env;
    }
    static int Main(string[] args){try{
        mod=Assembly.LoadFile(Path.GetFullPath(args[0]));Directory.CreateDirectory(args[1]);
        using(trace=new StreamWriter(Path.Combine(args[1],"reference-contracts.csv"))){
            trace.WriteLine("variant,scenario,frame,g,alpha,rawPitch,referencePitch,gLimit,hard");
            foreach(string variant in new[]{"A","C","T","NA"}){
                string type=variant=="A"?"Fs20AEnergyEnvelope":variant=="C"?"Fs20FlightEnvelope":"Fs20TLoadEnvelope";
                var e=Step(New(type),variant,"entry",0);
                Check(Math.Abs(Value(e,"Pitch")+1)<.0001f,variant+": full safe demand from first frame");
                for(int i=0;i<100;i++)Step(e,variant,"boundary-warm",i,11.49f,q:.35f);
                float last=Value(e,"Pitch"),maxStep=0,minPull=1;
                for(int i=0;i<100;i++){Step(e,variant,"boundary-alternating",i,i%2==0?11.51f:11.49f,q:.35f);float p=Value(e,"Pitch");maxStep=Math.Max(maxStep,Math.Abs(p-last));minPull=Math.Min(minPull,-p);last=p;}
                Check(minPull>.5f&&maxStep<.09f,variant+": 0.02G threshold noise preserves continuous pull");
                Step(e,variant,"off",0,on:false);Step(e,variant,"on",0);
                Check(Math.Abs(Value(e,"Pitch")+1)<.0001f&&Math.Abs(Value(e,"LimitG")-11)<.0001f,variant+": reenable does not restart stick or downgrade G");
                Step(e,variant,"release",0,pitch:0);Check(Value(e,"Pitch")==0,variant+": immediate release");
                Step(e,variant,"push",0,pitch:.7f);Check(Math.Abs(Value(e,"Pitch")-.7f)<.0001f,variant+": full recovery push retained");
                e=New(type);for(int i=0;i<40;i++)Step(e,variant,"roll-reversal-recovery",i,g:-3,alpha:-1,pitch:-.5f,q:-.7f);
                Check(Math.Abs(Value(e,"Pitch")+.5f)<.0001f,variant+": a negative body rate below the load budget cannot cut a recovery pull");
                e=New(type);for(int i=0;i<100;i++)Step(e,variant,"high-alpha-authority",i,alpha:32,q:.6f);
                Check(Math.Abs(Value(e,"Pitch")+1)<.0001f&&!Flag(e,"HardProtection"),variant+": 32deg below useful polar margin remains available");
                if(variant=="A"||variant=="C")Check(Value(e,"Roll")==1,variant+": high-alpha roll demand is not attenuated");
                for(int i=0;i<150;i++)Step(e,variant,"post-envelope",i,alpha:50,q:0);
                Check(Value(e,"Pitch")>=0&&Flag(e,"HardProtection"),variant+": genuine excessive incidence still requests recovery");
                for(int i=0;i<20;i++)Step(e,variant,"recovered",i);
                Check(!Flag(e,"HardProtection"),variant+": emergency state releases after recovery hysteresis");
                e=New(type);Step(e,variant,"heavy-on",0,mass:20000);float cap=Value(e,"LimitG");Step(e,variant,"heavy-off",0,mass:20000,on:false);
                Check(cap<=11&&cap>1&&Math.Abs(cap-Value(e,"LimitG"))<.0001f,variant+": structural load budget stable across assist modes");
                foreach(float dt in new[]{.01f,.02f,.05f}){
                    e=New(type);float prev=-1;for(int i=0;i<(int)(2/dt);i++){float g=5+8*Math.Min(1,i*dt);Step(e,variant,"load-ramp-dt"+dt,i,g:g,dt:dt);float p=Value(e,"Pitch");Check(!float.IsNaN(p)&&!float.IsInfinity(p)&&p>=-1&&p<=1,variant+": finite bounded rate-step demand");if(i>0)Check(Math.Abs(p-prev)<=4.01f*dt,variant+": rate changes have no event discontinuity");prev=p;}
                }
            }
            var c=mod.GetType(NS+"Fs20FlightEnvelope",true);var t=mod.GetType(NS+"Fs20TFlightPolicy",true);
            float gain=(float)c.GetMethod("SurfaceGain",F).Invoke(null,new object[]{550f});Check(gain==1,"C: no duplicated high-speed surface nerf");
            foreach(float pitch in new[]{-1f,-.5f,0f,.5f,1f})foreach(float roll in new[]{-1f,0f,1f})foreach(bool outer in new[]{false,true}){
                var mixer=t.GetMethod("ElevonAngle",F);float left=(float)mixer.Invoke(null,new object[]{pitch,roll,1f,outer}),right=(float)mixer.Invoke(null,new object[]{pitch,roll,-1f,outer});
                Check(Math.Abs(left)<=40.001f&&Math.Abs(right)<=40.001f,"Delta mixer: physical surface travel");Check(Math.Abs((left-right)*.5f-roll*(outer?30:18))<.001f,"Delta mixer: full differential retained in combined pull");
            }
        }
        File.WriteAllText(Path.Combine(args[1],"reference-contracts.txt"),checks+" behavior checks passed against "+mod.Location+"\nScope: reference contracts; no free-flight guarantee.\n");
        if(args.Length>2){
            var baseline=Assembly.LoadFile(Path.GetFullPath(args[2]));var names=baseline.GetManifestResourceNames().OrderBy(x=>x).ToArray();
            if(!names.SequenceEqual(mod.GetManifestResourceNames().OrderBy(x=>x)))throw new Exception("Embedded resource catalog changed");
            using(var resourceTrace=new StreamWriter(Path.Combine(args[1],"resource-contracts.csv")))using(var sha=SHA256.Create()){
                resourceTrace.WriteLine("resource,sha256,unchanged");foreach(var name in names){
                    byte[] before,after;using(var stream=baseline.GetManifestResourceStream(name))before=sha.ComputeHash(stream);using(var stream=mod.GetManifestResourceStream(name))after=sha.ComputeHash(stream);
                    bool equal=before.SequenceEqual(after);resourceTrace.WriteLine(name+","+BitConverter.ToString(after).Replace("-","")+","+equal);if(!equal)throw new Exception("Embedded asset changed: "+name);
                }
            }
            Console.WriteLine(names.Length+" embedded resource hashes unchanged");
        }
        Console.WriteLine(checks+" reference behavior checks passed");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
