using System;
using Japensou.NuclearOption.VortexConventional;

internal static class Fs20AEnergyChecks
{
    private static int checks;
    private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
    private static void Tick(Fs20AEnergyEnvelope law,float p=-1,float roll=1,float mass=13100,
        float alpha=10,float g=4,bool active=true,float dt=.02f)
        =>law.Step(p,roll,mass,alpha,g,active,dt);
    private static Fs20AEnergyEnvelope Settled()
    {var law=new Fs20AEnergyEnvelope();for(int i=0;i<50;i++)Tick(law);return law;}
    private static void Main()
    {
        var law=new Fs20AEnergyEnvelope();Tick(law);
        Check(law.Pitch<0 && law.Pitch>=-.0801f,"bounded positive-G onset");
        law=Settled();Check(Near(law.LimitG,11) && Near(law.Pitch,-1),"full internal fuel reaches 11 G command ceiling");
        Tick(law,p:0);Check(Near(law.Pitch,0),"immediate stick release");
        Tick(law,p:.7f,alpha:30,g:15);Check(Near(law.Pitch*law.LimitG,.7f*9),"push-over escapes protection without increasing negative-G reference");
        law=Settled();Tick(law,alpha:24);Check(Near(law.Pitch,0),"high AoA removes wasteful pull demand");
        Check(Near(law.Roll,.75f),"high AoA retains roll authority");
        Tick(law,alpha:10);Check(law.Pitch>=-.0801f,"AoA recovery does not rebound to full pull");
        law=Settled();Tick(law,g:12.1f);Check(Near(law.Pitch,0),"overload unloads pull");
        Tick(law,g:4);Check(law.Pitch>=-.0801f,"overload recovery is progressive");
        law=Settled();Tick(law,mass:16000);Check(Near(law.LimitG,9.28125f),"heavy gross mass reduces ceiling");
        Tick(law,p:-.9f,roll:-.6f,alpha:30,g:15,active:false);
        Check(Near(law.Pitch,-.9f) && Near(law.Roll,-.6f),"assist-off leaves direct inputs unchanged");
        foreach(float mass in new[]{10000f,12500f,13100f,13500f,16000f})
        foreach(float alpha in new[]{-5f,0f,10f,18f,21f,24f,30f})
        foreach(float g in new[]{0f,4f,9f,12f})
        foreach(float pitch in new[]{-1f,0f,1f})
        {
            law=new Fs20AEnergyEnvelope();
            for(int i=0;i<30;i++)
            {
                Tick(law,p:pitch,mass:mass,alpha:alpha,g:g);
                Check(law.LimitG>0 && law.LimitG<=11,"finite 11 G maximum");
                Check(law.Pitch>=-1 && law.Pitch<=1 && !float.IsNaN(law.Pitch),"bounded finite pitch");
                Check(law.Roll>=.75f && law.Roll<=1,"bounded roll reserve");
                if(pitch>=0)Check(Near(law.Pitch*law.LimitG,pitch*Math.Min(9,law.LimitG)),"release/push remains immediate with native negative-G reference");
            }
        }
        float liquidVolume=Fs20AUpgradeSpec.ExtraFuel/Fs20AUpgradeSpec.ModeledFuelDensity;
        Check(liquidVolume<=.85f*Fs20AUpgradeSpec.TankVolume,"rated fuel fits volume with at least 15 percent allowance");
        Console.WriteLine("FS20A_ENERGY_PASS assertions="+checks+"; offline control laws and volume budget; no game launched");
    }
}
