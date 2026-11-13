using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Fs20TPitchGovernor
{
    private static int checks;
    private static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    private static void Main(string[] args)
    {
        var folders=new[]{Path.GetDirectoryName(args[0]),Path.Combine(args[1],"BepInEx/core"),Path.Combine(args[1],"NuclearOption_Data/Managed")};
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,ev)=>{string file=folders.Select(p=>Path.Combine(p,new AssemblyName(ev.Name).Name+".dll")).FirstOrDefault(File.Exists);return file==null?null:Assembly.LoadFrom(file);};
        var type=Assembly.LoadFrom(args[0]).GetType("Japensou.NuclearOption.VortexConventional.Fs20TLoadEnvelope",true);
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object model=Activator.CreateInstance(type,true);
        float Value(string name)=>(float)type.GetProperty(name,flags).GetValue(model);
        void Step(float pitch,float g,float alpha,float q,bool enabled=true,float dt=.02f,float mass=10000f,float blend=0f){
            type.GetMethod("Step",flags).Invoke(model,new object[]{pitch,220f,1.225f,mass,g,alpha,enabled,true,dt,9.81f/220f,q,1f,blend});
        }
        for(int i=0;i<100;i++)Step(-1,1,10,0);
        Check(Math.Abs(Value("LimitG")-11f)<.001f,"Scheduled 11 G ceiling lost");
        float previous=Value("Pitch");
        for(int i=0;i<=60;i++){
            float g=8f+i*.05f;Step(-1,g,10,(g-1)*9.81f/220f);
            Check(Value("Pitch")<-.05f,"Approaching the G ceiling cuts the pull to zero");
            Check(Math.Abs(Value("Pitch")-previous)<=.1201f,"Soft reference has a discontinuous down-step");previous=Value("Pitch");
        }
        Step(-1,10.8f,25f,(10.8f-1)*9.81f/220f,false);
        for(int i=0;i<100;i++)Step(-1,10.8f,25f,(10.8f-1)*9.81f/220f);
        Check(Value("Pitch")<-.1f,"Holding the AoA boundary requires a continuing turn rate, not zero pitch rate");
        Step(0,10.8f,25f,.4f);Check(Value("Pitch")==0,"Stick release must be immediate");
        Step(-.7f,20,40,1,false);Check(Math.Abs(Value("Pitch")+.7f)<.0001f,"Manual controls must remain untouched");
        for(int i=0;i<100;i++)Step(-1,1,10,0);
        Step(-1,12,10,.5f);Check(Value("Pitch")==0&&(bool)type.GetProperty("HardProtection",flags).GetValue(model),"Actual overload must retain hard protection");
        Step(-1,1,29,0);Check(Value("Pitch")==0,"Excess AoA must retain hard protection");
        Step(1,12,29,0);Check(Value("Pitch")>0&&Value("Pitch")<=1,"Unloading must remain available");
        for(int i=0;i<100;i++)Step(-1,1,10,0,true,.02f,18000f);
        Check(Value("LimitG")<10,"Heavy configuration must not receive unrestricted 11 G");
        float[] steady=new float[2];int index=0;
        foreach(float dt in new[]{.01f,.02f}){
            model=Activator.CreateInstance(type,true);
            for(float t=0;t<4;t+=dt)Step(-1,10.7f,12f,9.7f*9.81f/220f,true,dt);
            steady[index++]=Value("Pitch");
        }
        Check(Math.Abs(steady[0]-steady[1])<.005f,"Steady governor authority depends on tick rate");
        Console.WriteLine("FS20T_PITCH_GOVERNOR_PASS checks="+checks+"; compiled envelope state contracts, no game launched");
    }
}
