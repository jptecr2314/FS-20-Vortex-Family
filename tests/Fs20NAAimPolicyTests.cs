using System;
using Japensou.NuclearOption.VortexConventional;

internal static class Fs20NAAimPolicyTests
{
    private static int checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        checks++;
    }
    private static Fs20NAAimPolicy.Frame Step(Fs20NAAimPolicy p,float angle=1f,float targetRate=.1f,float bodyRate=.1f,
        float bank=0f,float roll=0f,float range=500f,float closing=0f,float eas=240f,bool jump=false,float dt=.02f)
        => p.Step(angle,bank,roll,targetRate,0f,bodyRate,0f,0f,.4f,range,closing,eas,1500f,dt,jump);
    private static Fs20NAAimPolicy.Frame Settle(Fs20NAAimPolicy p)
    {
        Fs20NAAimPolicy.Frame f=default(Fs20NAAimPolicy.Frame);
        for(int i=0;i<100;i++)f=Step(p);
        return f;
    }
    private static float Simulation(bool capture)
    {
        var p=new Fs20NAAimPolicy();float angle=0f,rate=0f,lastError=0f;double sum=0;int count=0;
        const float dt=.02f,full=.4f,toDegrees=57.2957795f;
        for(int i=0;i<3000;i++){
            float t=i*dt,goal=.03f*(float)Math.Sin(.5f*t)+.02f*t;
            float desiredRate=.015f*(float)Math.Cos(.5f*t)+.02f;
            float error=(goal-angle)*toDegrees,d=(error-lastError)/dt;lastError=error;
            float u=.05f*error+.005f*d;
            var f=p.Step(Math.Abs(error),0f,0f,desiredRate,0f,rate,0f,0f,full,1200f,0f,240f,1500f,dt,false);
            if(capture)u=Fs20NAAimPolicy.Refine(u,.005f*d,.005f,f.PitchErrorRate,f.PitchFeedforward,f.Weight,f.PitchDamping);
            u=Math.Max(-1f,Math.Min(1f,u));rate+=(u*full-rate)*dt/(.18f+dt);angle+=rate*dt;
            if(t>5f){sum+=error*error;count++;}
        }
        return (float)Math.Sqrt(sum/count);
    }
    private static int Main()
    {
        var p=new Fs20NAAimPolicy();var f=Settle(p);
        Check(f.Weight>.99f,"Small steady error must capture gradually");
        Check(Math.Abs(f.PitchErrorRate)<.001f,"Matching target motion is not excess rotation");
        Check(f.PitchFeedforward>0f,"A moving target retains positive tracking rate");
        float matching=Fs20NAAimPolicy.Refine(.2f,0f,.005f,0f,.1f,1f,.01f);
        float excess=Fs20NAAimPolicy.Refine(.2f,0f,.005f,-10f,.1f,1f,.01f);
        float lag=Fs20NAAimPolicy.Refine(.2f,0f,.005f,10f,.1f,1f,.01f);
        Check(excess<matching&&matching<lag,"Feedback brakes only excess relative motion");
        f=Step(p,angle:45f);Check(f.Weight==0f,"Large error immediately releases capture");
        Check(Fs20NAAimPolicy.Refine(-2f,.3f,.02f,80f,.3f,f.Weight)==-2f,"Full maneuver command is unchanged outside capture");
        Settle(p);f=Step(p,jump:true);Check(f.Weight==0f&&f.ThrottleCeiling==1f,"Abrupt target change releases steering and approach shaping");
        p.Reset();for(int i=0;i<100;i++)f=Step(p,bank:60f);Check(f.Weight==0f,"Bank reversal keeps native maneuver authority");
        p.Reset();for(int i=0;i<100;i++)f=Step(p,roll:2f);Check(f.Weight==0f,"Fast roll is not damped by gun capture");
        p.Reset();f=Step(p,closing:100f);Check(f.ThrottleCeiling<1f,"Fast closing, aligned, high-energy approach is managed");
        p.Reset();f=Step(p,closing:100f,eas:150f);Check(f.ThrottleCeiling==1f,"Low energy preserves throttle authority");
        p.Reset();f=Step(p,closing:-50f);Check(f.ThrottleCeiling==1f,"Opening range preserves native throttle");
        p.Reset();f=Step(p,closing:100f,range:2000f);Check(f.ThrottleCeiling==1f,"Distant pursuit stays native");
        Settle(p);f=Step(p,dt:0f);Check(f.Weight==0f&&f.ThrottleCeiling==1f,"Pause does not retain stale capture");
        Settle(p);f=Step(p,targetRate:float.NaN);Check(f.Weight==0f,"Nonfinite tracking data fails closed");
        Check(Fs20NAAimPolicy.Refine(.2f,0f,float.NaN,0f,.1f,1f)==.2f,"Invalid refinement preserves native command");
        p.Reset();float previous=0f;
        for(int i=0;i<30;i++){f=Step(p);Check(f.Weight>=previous&&f.Weight-previous<.15f,"Capture enters continuously");previous=f.Weight;}
        float baseline=Simulation(false),candidate=Simulation(true);
        Check(candidate<baseline*.85f,"Moving-target tracking must improve in the actuator model");
        Console.WriteLine("PASS checks="+checks+" modelRmsDeg="+baseline.ToString("F4")+" -> "+candidate.ToString("F4"));
        return 0;
    }
}
