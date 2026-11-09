using System;
using Japensou.NuclearOption.VortexConventional;
internal static class LandingChecks
{
    static int count;
    static void Check(bool condition,string why){count++;if(!condition)throw new Exception(why);}
    static void Tick(Fs20HookDepartureLock l,int ticks,bool air=true,bool gear=true,float alt=50,float vs=-2,float throttle=.5f,bool hooked=false)
    {for(int i=0;i<ticks;i++)l.Step(air,gear,alt,vs,throttle,hooked,.02f);}
    static void Main()
    {
        var hook=new Fs20HookDepartureLock();
        Tick(hook,200,air:false,throttle:1);Check(hook.Locked,"stationary launch must stay locked");
        Tick(hook,250,vs:8,throttle:1);Tick(hook,100,vs:-3,throttle:.8f);
        Check(hook.Locked,"initial deck dip must not arm hook");
        Tick(hook,200,gear:false,vs:5);Check(!hook.Locked,"gear-up established flight arms hook");
        Tick(hook,200,alt:80);Check(!hook.Locked,"normal approach remains armed");
        Tick(hook,100,air:false);Check(!hook.Locked,"landing rollout retains hook");
        Tick(hook,100,air:false,throttle:1,hooked:true);Check(!hook.Locked,"guard must not release caught wire");
        Tick(hook,1,air:false,throttle:1);Check(hook.Locked,"new ground departure relocks");
        hook=new Fs20HookDepartureLock();Tick(hook,800);Check(!hook.Locked,"low circuit approach eventually arms");
        Tick(hook,1,vs:4,throttle:1);Check(hook.Locked,"go-around relocks");
        hook=new Fs20HookDepartureLock();Tick(hook,450,alt:200,vs:4,throttle:.7f);
        Check(!hook.Locked,"airborne spawn at altitude arms after grace period");
        var landing=new Fs20LandingEnvelope();
        for(int i=0;i<50;i++)landing.Step(-.2f,.2f,75,true,true,true,.02f);
        Check(landing.Blend>.999f && Math.Abs(landing.Pitch)<.2f && landing.Pitch<0,"fine approach input retains sign and softens");
        landing.Step(-1,1,75,true,true,true,.02f);
        Check(Math.Abs(landing.Pitch+1)<.00001 && Math.Abs(landing.Roll-1)<.00001,"full stick retained for flare/correction");
        landing.Step(-.4f,.6f,75,true,true,false,.02f);
        Check(landing.Blend==0 && landing.Pitch==-.4f && landing.Roll==.6f,"assist off remains direct");
        for(int i=0;i<50;i++)landing.Step(-.4f,.6f,200,true,true,true,.02f);
        Check(landing.Blend==0,"high speed restores combat schedule");
        for(int i=0;i<50;i++)landing.Step(-.4f,.6f,75,true,true,true,.02f);
        for(int i=0;i<50;i++)landing.Step(-.4f,.6f,75,false,true,true,.02f);
        Check(landing.Blend==0,"gear up restores combat schedule");
        float[] metric={-2,-1, -1,-1, -2,1, 0,0, .01f,0, 0,.01f};
        var packed=Fs20HookUv.Pack(metric);
        foreach(float v in packed)Check(v>0 && v<1,"UV island stays inside padded unit tile");
        for(int i=0;i<2;i++)
        {
            int j=i*6;
            float area=(packed[j+2]-packed[j])*(packed[j+5]-packed[j+1])-(packed[j+4]-packed[j])*(packed[j+3]-packed[j+1]);
            Check(area>0,"UV triangle preserves area and winding");
        }
        Check(packed[2]<packed[6],"adjacent UV islands do not overlap");
        Console.WriteLine("PASS "+count+" landing/departure/UV checks");
    }
}
