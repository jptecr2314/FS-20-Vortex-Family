using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    [Serializable] public sealed class Fs20TakeoffStore
    {
        public string key;
        public float mass, dragArea;
        public Vector3 position;
    }
    [Serializable] public sealed class Fs20TakeoffGrid
    {
        public float fuel, mass, inertia, damping;
        public Vector3 center;
        public Vector3[] cells;
        public Vector3 Sample(float speed,float alpha,float pitch)
        {
            float v=Mathf.Clamp((speed-20)/20,0,6),a=Mathf.Clamp((alpha+12)/2,0,22),p=Mathf.Clamp((pitch+1)*4,0,8);
            int i=Mathf.Min(5,(int)v),j=Mathf.Min(21,(int)a),k=Mathf.Min(7,(int)p);
            Vector3 At(int n)=>Vector3.Lerp(Vector3.Lerp(cells[(n*23+j)*9+k],cells[(n*23+j)*9+k+1],p-k),
                Vector3.Lerp(cells[(n*23+j+1)*9+k],cells[(n*23+j+1)*9+k+1],p-k),a-j);
            var f=Vector3.Lerp(At(i),At(i+1),v-i);return speed<20?f*(speed*speed/400):f;
        }
    }
    [Serializable] public sealed class Fs20TakeoffAirframe
    {
        public string variant;
        public float dryThrust,afterburnerThrust,vectorPitch,vectorGain,tailAngle,referenceDensity=1.167f;
        public Vector3 pivot,enginePoint,noseContact;
        public float pitchP,pitchD,pitchI,maxAlpha;
        public List<Fs20TakeoffGrid> grids=new List<Fs20TakeoffGrid>();
    }
    [Serializable] public sealed class Fs20TakeoffDatabase {public int version=2;public List<Fs20TakeoffAirframe> aircraft=new List<Fs20TakeoffAirframe>();}
    [Serializable] public sealed class Fs20TakeoffRequest
    {
        public float fuelFraction,headwind,availableRun,density=1.167f;
        public bool afterburner=true;
        public Fs20TakeoffStore[] stores=new Fs20TakeoffStore[0];
        // Actual native body aggregation can replace reconstructed mass properties.
        // The stores' masses are then already included; only their drag is added.
        public bool actualMassProperties;
        public float actualMass,actualPitchInertia;
        public Vector3 actualCenter;
    }
    [Serializable] public sealed class Fs20TakeoffPlan
    {
        public string status,reason;
        public float mass,pitchInertia,rotationSpeed,groundRoll,screenDistance,requiredRun,availableRun;
        public Vector3 center;
        public bool supported,predictedFits;
        public string rejectedSchedules;
    }
    public static class Fs20DynamicTakeoff
    {
        private static Fs20TakeoffDatabase database;
        public static Fs20TakeoffAirframe Profile(string variant)
        {
            if(database==null) {
                using(var stream=typeof(Fs20DynamicTakeoff).Assembly.GetManifestResourceStream("Fs20TakeoffGrids.json")) {
                    if(stream==null)return null;
                    using(var reader=new StreamReader(stream))database=JsonUtility.FromJson<Fs20TakeoffDatabase>(reader.ReadToEnd());
                }
            }
            if(database==null||database.version!=2||database.aircraft==null)return null;
            var profile=database.aircraft.Find(a=>a.variant==variant);
            if(profile==null||profile.grids==null||profile.grids.Count<2)return null;
            foreach(var grid in profile.grids)if(grid==null||grid.cells==null||grid.cells.Length!=7*23*9)return null;
            return profile;
        }
        public static Fs20TakeoffPlan Calculate(string variant,Fs20TakeoffRequest request)=>Calculate(Profile(variant),request);
        public static Fs20TakeoffPlan Calculate(Fs20TakeoffAirframe frame,Fs20TakeoffRequest r)
        {
            var plan=new Fs20TakeoffPlan{status="Not evaluated",reason="Outside the validated model domain."};
            if(frame==null||r==null||frame.grids.Count<2||!Finite(r.fuelFraction)||r.fuelFraction<.2f||r.fuelFraction>1||
                !Finite(r.headwind)||r.headwind<0||r.headwind>20||!Finite(r.availableRun)||r.availableRun<=0||
                !Finite(r.density)||r.density<.95f||r.density>1.25f||r.stores==null)return plan;
            var lo=frame.grids[0];var hi=frame.grids[frame.grids.Count-1];
            for(int i=0;i<frame.grids.Count-1;i++)if(r.fuelFraction>=frame.grids[i].fuel&&r.fuelFraction<=frame.grids[i+1].fuel){lo=frame.grids[i];hi=frame.grids[i+1];break;}
            float blend=Mathf.InverseLerp(lo.fuel,hi.fuel,r.fuelFraction),baseMass=Mathf.Lerp(lo.mass,hi.mass,blend);
            var referenceCenter=Vector3.Lerp(lo.center,hi.center,blend);float baseInertia=Mathf.Lerp(lo.inertia,hi.inertia,blend);
            float mass=baseMass;var moment=referenceCenter*baseMass;
            foreach(var s in r.stores){if(s==null||!Finite(s.mass)||!Finite(s.dragArea)||s.mass<0||s.dragArea<0||!Finite(s.position))return plan;mass+=s.mass;moment+=s.position*s.mass;}
            var center=moment/Mathf.Max(1,mass);float inertia=baseInertia+baseMass*PitchArm(referenceCenter-center);
            foreach(var s in r.stores)inertia+=s.mass*(PitchArm(s.position-center)+.1f);
            if(r.actualMassProperties){mass=r.actualMass;center=r.actualCenter;inertia=r.actualPitchInertia;}
            if(!Finite(mass)||!Finite(inertia)||!Finite(center)||mass<baseMass*.9f||mass>16000||inertia<=1)return plan;
            if(Mathf.Abs(center.x-referenceCenter.x)>.05f){plan.reason="Asymmetric load requires lateral/roll validation.";return plan;}
            if(Mathf.Abs(center.z-referenceCenter.z)>.6f||Mathf.Abs(center.y-referenceCenter.y)>.6f){plan.reason="CG shift outside the longitudinal calibration envelope.";return plan;}
            plan.mass=mass;plan.center=center;plan.pitchInertia=inertia;plan.availableRun=r.availableRun;
            foreach(float vr in new[]{25f,40f,55f,70f}) {
                string failure;
                var trial=Run(frame,r,lo,hi,blend,referenceCenter,mass,center,inertia,vr,out failure);
                if(trial==null)plan.rejectedSchedules+=vr+": "+failure+"; ";
                if(trial!=null&&(!plan.supported||trial.groundRoll<plan.groundRoll)) {
                    plan.supported=true;plan.rotationSpeed=vr;plan.groundRoll=trial.groundRoll;plan.screenDistance=trial.screenDistance;
                }
            }
            if(!plan.supported){plan.status="No viable modeled launch";plan.reason="All tested rotation schedules failed, struck the tail or exceeded the force-grid domain.";return plan;}
            plan.requiredRun=plan.groundRoll*1.2f+5f;
            plan.predictedFits=plan.requiredRun<=r.availableRun;
            plan.status=plan.predictedFits?"Predicted within planning margin":"Insufficient modeled runway margin";
            plan.reason="Reduced-model prediction only. Requires 20% roll reserve plus 5 m; not an autonomous launch clearance.";
            return plan;
        }
        static Fs20TakeoffPlan Run(Fs20TakeoffAirframe f,Fs20TakeoffRequest r,Fs20TakeoffGrid lo,Fs20TakeoffGrid hi,float blend,
            Vector3 refCg,float mass,Vector3 cg,float inertia,float vr,out string failure)
        {
            failure="No screen clearance";
            const float dt=.02f,mu=.025f;var arm=cg-f.pivot;
            float theta0=-Mathf.Atan2(f.noseContact.y-f.pivot.y,f.noseContact.z-f.pivot.z),theta=theta0,q=0,input=0,x=0,y=0,vx=0,vy=0,roll=0,previousError=0,integral=0,previousAlpha=theta0*Mathf.Rad2Deg,previousPath=0;
            float ip=inertia+mass*PitchArm(arm),target=Mathf.Min(12,f.tailAngle-1)*Mathf.Deg2Rad;bool flying=false;
            for(int tick=0;tick<4500;tick++) {
                float ax=vx+r.headwind,speed=Mathf.Sqrt(ax*ax+vy*vy),path=flying?Mathf.Atan2(vy,Mathf.Max(.1f,ax)):0,alpha=(theta-path)*Mathf.Rad2Deg;
                if(speed>140||alpha < -12||alpha>32){failure="Force domain: speed="+speed+", alpha="+alpha;return null;}
                float desired=ax>=vr?Mathf.Clamp((target-theta)*2,-.25f,.25f):0;
                float alphaRate=(alpha-previousAlpha)/dt,trajectory=(path-previousPath)/dt;
                desired=Mathf.Min(desired,trajectory+(f.maxAlpha-alpha-.1f*Mathf.Max(0,alphaRate))*2*Mathf.Deg2Rad);
                previousAlpha=alpha;previousPath=path;
                float error=Mathf.Clamp(desired-q,-.25f,.25f);integral=Mathf.Clamp(integral+error*dt,-.2f,.2f);
                float correction=error*f.pitchP+integral*f.pitchI+(error-previousError)/dt*f.pitchD;previousError=error;
                float requested=ax>=vr?Mathf.Clamp(input-Mathf.Clamp(correction,-2,2)*dt,-1,1):0;
                if(alpha>=f.maxAlpha+.5f)requested=Mathf.Max(0,requested);
                input=Mathf.MoveTowards(input,requested,2.25f*dt);
                var sample=Vector3.Lerp(lo.Sample(speed,alpha,input),hi.Sample(speed,alpha,input),blend)*(r.density/f.referenceDensity);
                var bodyForce=new Vector3(0,sample.x,sample.y);
                float momentX=sample.z+Vector3.Cross(refCg-cg,bodyForce).x;
                foreach(var store in r.stores) {
                    // Conservative upper bound for native added dragArea. The native
                    // parasitic term is q * dragArea * (1 - .5*wingEffectiveness).
                    var drag=-new Vector3(0,-Mathf.Sin(alpha*Mathf.Deg2Rad),Mathf.Cos(alpha*Mathf.Deg2Rad))*
                        (.5f*r.density*speed*speed*store.dragArea);
                    bodyForce+=drag;momentX+=Vector3.Cross(store.position-cg,drag).x;
                }
                float thrust=f.dryThrust+(r.afterburner?f.afterburnerThrust:0),angle=-Mathf.Clamp(input*f.vectorGain,-1,1)*f.vectorPitch*Mathf.Deg2Rad;
                var thrustBody=new Vector3(0,-Mathf.Sin(angle)*thrust,Mathf.Cos(angle)*thrust);
                var rotation=Quaternion.Euler(-theta*Mathf.Rad2Deg,0,0);var aero=rotation*bodyForce;var tv=rotation*thrustBody;
                float upMoment=-momentX+Mathf.Lerp(lo.damping,hi.damping,blend)*q*(speed*speed/6400)-Vector3.Cross(rotation*(f.enginePoint-cg),tv).x;
                float fy=aero.y+tv.y-mass*9.81f,fx=aero.z+tv.z,normal=0;
                if(!flying){var rw=rotation*arm;float qa=(upMoment+rw.z*fy-rw.y*mu*Mathf.Max(0,-fy))/ip;
                    if(theta<=theta0&&qa<0){qa=0;q=0;}q+=qa*dt;theta=Mathf.Max(theta0,theta+q*dt);y=rw.y;
                    normal=mass*(qa*rw.z-q*q*rw.y)-fy;if(theta>f.tailAngle*Mathf.Deg2Rad){failure="Tailstrike";return null;}
                    if(normal<=0&&ax>10){flying=true;roll=x;vy=q*rw.z;}
                }else{q+=upMoment/inertia*dt;theta+=q*dt;vy+=fy/mass*dt;y+=vy*dt;}
                vx=Mathf.Max(0,vx+Mathf.Max(-5,(fx-mu*Mathf.Max(0,normal))/mass)*dt);x+=vx*dt;
                if(!Finite(theta)||Mathf.Abs(q)>3||Mathf.Abs(theta)>1){failure="Divergence";return null;}
                float height=y-(rotation*arm).y;if(flying&&height<-.1f)return null;
                if(flying&&height>=10.7f)return new Fs20TakeoffPlan{groundRoll=roll,screenDistance=x};
            }
            return null;
        }
        static float PitchArm(Vector3 r)=>r.y*r.y+r.z*r.z;
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
    }
}

