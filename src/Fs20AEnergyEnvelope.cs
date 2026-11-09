using System;

namespace Japensou.NuclearOption.VortexConventional
{
    // Original-wing A: up to 11 G, reduce high-alpha drag demand and allow
    // immediate unloading. Candidate control laws, not measured turn performance.
    internal sealed class Fs20AEnergyEnvelope
    {
        internal const float MaximumG=11f;
        internal const float BaselineG=9f;
        internal const float ReferenceMass=13500f;
        internal float LimitG {get;private set;}=BaselineG;
        internal float Pitch {get;private set;}
        internal float Roll {get;private set;}
        private float pull;

        internal void Step(float pitch,float roll,float mass,float alpha,float normalG,bool assisted,float dt)
        {
            if(!assisted){LimitG=BaselineG;Pitch=pitch;Roll=roll;pull=0;return;}
            float step=Math.Max(0f,Math.Min(.1f,dt));
            float ceiling=MaximumG*ReferenceMass/Math.Max(ReferenceMass,mass);
            LimitG=Math.Min(ceiling,LimitG+2f*step);
            float alphaReserve=1f-Smooth((alpha-18f)/6f);
            float overloadReserve=1f-Smooth((normalG-LimitG-.15f)/.85f);
            float requested=Math.Max(0f,Math.Min(1f,-pitch))*alphaReserve*overloadReserve;
            pull=Math.Min(requested,pull+4f*step);
            Pitch=pitch>=0f ? pitch*Math.Min(1f,BaselineG/LimitG) : -pull;
            Roll=roll*(.75f+.25f*alphaReserve);
        }
        private static float Smooth(float x){x=Math.Max(0f,Math.Min(1f,x));return x*x*(3f-2f*x);}
    }
}
