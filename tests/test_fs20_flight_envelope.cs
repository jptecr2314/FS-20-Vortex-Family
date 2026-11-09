using System;
using Japensou.NuclearOption.VortexConventional;

internal static class Fs20EnvelopeChecks
{
    private static int checks;
    private static void Check(bool condition,string label)
    {checks++;if(!condition)throw new Exception(label);}
    private static bool Near(float x,float y,float tolerance=0.0001f) => Math.Abs(x-y)<tolerance;
    private static void Tick(Fs20FlightEnvelope law,float pitch=-1,float roll=0,float speed=200,
        float density=1.225f,float mass=12000,float alpha=10,float g=4,bool gear=false,
        bool assist=true,float dt=.02f)
    {law.Step(pitch,roll,speed,density,mass,alpha,g,gear,assist,dt);}
    private static Fs20FlightEnvelope Settled()
    {var law=new Fs20FlightEnvelope();for(int i=0;i<100;i++)Tick(law);return law;}

    private static void Main()
    {
        var law=new Fs20FlightEnvelope();Tick(law);
        Check(law.Pitch<0 && law.Pitch>=-.061f,"abrupt full pull has bounded onset");
        Check(law.LimitG>9 && law.LimitG<=9.041f,"G authority ramps from baseline");
        law=Settled();Check(Near(law.LimitG,11),"light combat reaches 11 G");
        Check(Near(law.Pitch,-1),"full authority remains available inside envelope");
        Tick(law,pitch:0);Check(Near(law.Pitch,0),"stick release immediately removes pull");
        Tick(law,pitch:1);Check(law.Pitch>0 && Near(law.Pitch*law.LimitG,9),"push-over retains native command rather than becoming -11 G");
        Tick(law,mass:16000);Check(Near(law.LimitG,8.25f),"gross-mass ceiling reduces immediately");
        law=Settled();Tick(law,gear:true,g:11);
        Check(Near(law.LimitG,9) && Near(law.FlapScale,1),"gear-down retains old ceiling and takeoff flap bias");
        law=Settled();Tick(law,alpha:26);Check(Near(law.Pitch,0),"AoA limit unloads positive pull");
        Tick(law,alpha:10);Check(law.Pitch>=-.061f,"authority recovers gradually after AoA protection");
        law=Settled();Tick(law,g:12);Check(Near(law.Pitch,0),"measured overload removes positive pull");
        Tick(law,g:4);Check(law.Pitch>=-.061f,"overload recovery avoids full-stick rebound");
        Tick(law,pitch:.7f,roll:-.8f,alpha:35,g:15,assist:false);
        Check(Near(law.Pitch,.7f) && Near(law.Roll,-.8f) && Near(law.FlapScale,1),"assist-off preserves direct inputs");
        Check(Near(law.LimitG,9),"assist-off restores native G setting");
        Check(Near(Fs20FlightEnvelope.TargetLimit(200,1.225f,12000,false),
            Fs20FlightEnvelope.TargetLimit(400,.30625f,12000,false)),"density schedule uses equivalent airspeed");

        foreach(float density in new[]{.15f,.3f,.6f,1.225f})
        foreach(float speed in new[]{0f,45f,90f,100f,130f,160f,220f,400f,550f})
        foreach(float mass in new[]{9500f,12000f,14000f,16000f,20000f})
        foreach(bool gear in new[]{false,true})
        {
            law=new Fs20FlightEnvelope();
            for(int i=0;i<70;i++)
            {
                float before=law.LimitG;
                Tick(law,roll:.9f,speed:speed,density:density,mass:mass,gear:gear);
                Check(law.LimitG>=1 && law.LimitG<=11,"scheduled G remains inside envelope");
                Check(law.LimitG<=before+.0401f,"schedule has no upward step");
                Check(law.LimitG*mass<=132000.1f || mass<12000,"candidate force budget is respected");
                Check(law.Pitch>=-1 && law.Pitch<=0 && !float.IsNaN(law.Pitch),"finite pull command");
                Check(law.Roll<=.9f && law.Roll>=.539f,"roll authority is bounded");
                Check(law.FlapScale>=.25f && law.FlapScale<=1,"flap relief remains bounded");
            }
        }
        foreach(float dt in new[]{.01f,.02f,.0333333f})
        {
            law=new Fs20FlightEnvelope();float time=0;
            while(time<.2f){Tick(law,dt:dt);time+=dt;}
            Check(-law.Pitch<=3*time+.0001f,"onset rate is stable across timestep sizes");
        }
        Console.WriteLine("FS20C_ENVELOPE_PASS assertions="+checks+"; offline control laws only; no game launched");
    }
}
