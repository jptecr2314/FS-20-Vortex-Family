using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Match the outer attitude loop to each variant's inner G/rate authority.
    // Scaling happens before native command clamping, preserving full travel,
    // native PID history, yaw, forces and player controls.
    internal static class Fs20AiGuidance
    {
        [ThreadStatic] private static AeroPID pitch, roll, yaw;
        [ThreadStatic] private static float pitchScale,rollScale,yawScale;
        private static readonly FieldInfo forward=AccessTools.Field(typeof(Autopilot),"forwardFlightController");
        private static readonly FieldInfo pitchPid=forward==null?null:AccessTools.Field(forward.FieldType,"xPID");
        private static readonly FieldInfo yawPid=forward==null?null:AccessTools.Field(forward.FieldType,"yPID");
        private static readonly FieldInfo rollPid=forward==null?null:AccessTools.Field(forward.FieldType,"zPID");
        private static bool warned;

        internal static Vector2 Gains(Aircraft a)
        {
            if(a==null||!a.LocalSim||a.Player!=null||a.gearDeployed||a.pilots==null||a.pilots.Length==0||
               a.pilots[0]==null||a.pilots[0].playerControlled)return Vector2.one;
            Vector2 gains;
            // Settled chase-loop response reference (120 deg/s in the native
            // half-rate regime). This is a tested damping target, not the B
            // roll-limit value, and does not alter the variant roll limits.
            const float settledRollReference=4.1887903f;
            if(a.GetComponent<Fs20NAMarker>()!=null){
                float eas=Fs20FlightEnvelope.EquivalentSpeed(a.speed,a.airDensity);
                float fast=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(160f,220f,eas));
                gains=Vector2.Lerp(new Vector2(.5f,.5f),new Vector2(9f/11f,5f/4.1887903f),fast);
            }
            else if(a.GetComponent<Fs20AFlightController>()!=null)gains=new Vector2(9f/Fs20AEnergyEnvelope.MaximumG,settledRollReference/4.8f);
            else if(a.GetComponent<Fs20FlightController>()!=null)gains=new Vector2(9f/Fs20FlightEnvelope.MaximumG,settledRollReference/Fs20FlightEnvelope.CombatRollRate);
            else return Vector2.one; // Stock B and installed T are deliberately excluded.
            float blend=Mathf.SmoothStep(0f,1f,Mathf.Min(Mathf.InverseLerp(65f,100f,a.speed),Mathf.InverseLerp(5f,30f,a.radarAlt)));
            return Vector2.Lerp(Vector2.one,gains,blend);
        }
        internal static float YawGain(Aircraft a)
        {
            if(a==null||!a.LocalSim||a.Player!=null||a.gearDeployed||a.pilots==null||a.pilots.Length==0||
               a.pilots[0]==null||a.pilots[0].playerControlled||a.GetComponent<Fs20NAMarker>()==null)return 1f;
            // NA's native allocation maps a unit yaw command to .65 rad/s.
            // Normalize the outer loop's partial commands to that rate contract;
            // existing allocation, authority limits and travel still saturate it.
            float eas=Fs20FlightEnvelope.EquivalentSpeed(a.speed,a.airDensity);
            float fast=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(160f,220f,eas));
            float clear=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(5f,30f,a.radarAlt));
            return Mathf.Lerp(1f,1f/.65f,fast*clear);
        }
                private sealed class CaptureState
        {
            internal readonly Fs20NAAimPolicy Policy = new Fs20NAAimPolicy();
            internal Vector3 Direction;
            internal GlobalPosition GunPoint,EnemyPoint;internal float GunTime;internal bool GunReady;
            internal float Time;
            internal bool Ready;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Aircraft,CaptureState> captures =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Aircraft,CaptureState>();
        private static readonly AccessTools.FieldRef<AeroPID,float> pidError = AccessTools.FieldRefAccess<AeroPID,float>("p");
        private static readonly AccessTools.FieldRef<ControlsFilter.FlyByWire,float> nativeG =
            AccessTools.FieldRefAccess<ControlsFilter.FlyByWire,float>("gLimitPositive");
        [ThreadStatic] private static Fs20NAAimPolicy.Frame aimFrame;
        internal static Fs20NAAimPolicy.Frame CaptureTelemetry(Aircraft a)
        {
            return a!=null && captures.TryGetValue(a,out var state) ? state.Policy.Last : new Fs20NAAimPolicy.Frame{ThrottleCeiling=1f};
        }
        private static Fs20NAAimPolicy.Frame PrepareCapture(Aircraft a,ref GlobalPosition goal,bool aimVelocity,
            bool runwayAlign,float effort,bool followTerrain,Vector3 targetVelocity,AeroPID bankPid)
        {
            var none=new Fs20NAAimPolicy.Frame{ThrottleCeiling=1f};
            if(a==null || a.definition==null || a.definition.code!="FS-20NA")return none;
            var filter=a.GetControlsFilter();
            var station=a.weaponManager.currentWeaponStation;
            bool eligible=a.LocalSim && a.Player==null && !a.disabled && !a.gearDeployed && a.flightAssist &&
                a.pilots!=null && a.pilots.Length>0 && a.pilots[0]!=null && !a.pilots[0].playerControlled &&
                a.pilots[0].currentState is AIPilotCombatModes && a.GetComponent<Fs20NAMarker>()!=null &&
                filter!=null && filter.GetFlyByWire().Enabled && !aimVelocity && !runwayAlign && !followTerrain &&
                effort>=.99f && targetVelocity.sqrMagnitude>900f && station!=null && station.WeaponInfo.gun &&
                station.WeaponInfo.boresight;
            if(!eligible){if(captures.TryGetValue(a,out var previous)){previous.Policy.Reset();previous.Ready=false;}return none;}
            var state=captures.GetValue(a,key=>new CaptureState());
            float now=UnityEngine.Time.fixedTime;
            if(!state.GunReady || state.GunTime!=now){state.Policy.Reset();state.Ready=false;return none;}
            GlobalPosition nativeSteering=goal;goal=state.GunPoint;
            if(state.Ready && now==state.Time){goal=state.Policy.Last.Weight>0f?nativeSteering+(state.GunPoint-nativeSteering)*state.Policy.Last.Weight:nativeSteering;return state.Policy.Last;}
            Vector3 offset=goal-a.GlobalPosition();
            float range=offset.magnitude;
            if(range<100f || float.IsNaN(range) || float.IsInfinity(range)){state.Policy.Reset();state.Ready=false;goal=nativeSteering;return none;}
            var body=a.CockpitRB();
            Vector3 direction=offset/range;
            Vector3 relative=targetVelocity-body.velocity;
            Vector3 targetOmega=Vector3.Cross(offset,relative)/Mathf.Max(10000f,offset.sqrMagnitude);
            var inverse=Quaternion.Inverse(body.rotation);
            Vector3 angular=inverse*body.angularVelocity;
            Vector3 rates=inverse*targetOmega;
            Vector3 flow=inverse*(body.velocity-a.GetWindVelocity());
            float coordinated=Fs20NAYawController.CoordinatedRate(flow,angular,inverse*Physics.gravity);
            Vector3 gunDirection=Vector3.zero;
            foreach(var weapon in station.Weapons)if(weapon)gunDirection+=weapon.transform.forward;
            if(gunDirection.sqrMagnitude<.01f)gunDirection=body.transform.forward;
            float angle=Vector3.Angle(gunDirection,direction);
            bool jumped=state.Ready && (now-state.Time>.2f || Vector3.Angle(state.Direction,direction)>6f);
            float ceiling=Mathf.Max(1f,nativeG(filter.GetFlyByWire()));
            var frame=state.Policy.Step(angle,bankPid==null?0f:pidError(bankPid),angular.z,rates.x,rates.y,
                angular.x,angular.y,coordinated,Fs20Tuning.NativeRatePerG(filter,a,ceiling)*ceiling,
                (state.EnemyPoint-a.GlobalPosition()).magnitude,-Vector3.Dot(relative,((Vector3)(state.EnemyPoint-a.GlobalPosition())).normalized),Fs20FlightEnvelope.EquivalentSpeed(a.speed,a.airDensity),
                a.radarAlt,UnityEngine.Time.fixedDeltaTime,jumped);
            state.Ready=true;state.Time=now;state.Direction=direction;
            goal=frame.Weight>0f?nativeSteering+(state.GunPoint-nativeSteering)*frame.Weight:nativeSteering;
            a.GetInputs().throttle=Mathf.Min(a.GetInputs().throttle,frame.ThrottleCeiling);
            return frame;
        }

        internal struct GunSample {internal bool Active;internal Vector3 Acceleration;}
        [HarmonyPatch(typeof(AIPilotCombatModes),"UseFixedGuns")]
        internal static class GunPoint
        {
            private static void Prefix(Aircraft ___aircraft,Unit ___currentTarget,Vector3 ___targetVelPrev,out GunSample __state)
            {
                __state=default(GunSample);
                if(___aircraft==null||___aircraft.definition.code!="FS-20NA"||!___aircraft.LocalSim||___aircraft.Player!=null||
                   ___aircraft.pilots==null||___aircraft.pilots.Length==0||___aircraft.pilots[0].playerControlled||
                   ___aircraft.GetComponent<Fs20NAMarker>()==null||___currentTarget==null||___currentTarget.rb==null)return;
                __state.Active=true;
                __state.Acceleration=(___currentTarget.rb.velocity-___targetVelPrev)/Mathf.Max(.001f,UnityEngine.Time.fixedDeltaTime);
            }
            private static void Postfix(AIPilotCombatModes __instance,Aircraft ___aircraft,Unit ___currentTarget,Vector3 ___targetVel,
                GlobalPosition ___destination,float ___lastTargetAngle,float ___climbFactor,bool ___targetObscured,GunSample __state)
            {
                if(!__state.Active)return;
                var state=captures.GetValue(___aircraft,key=>new CaptureState());
                state.GunReady=__instance.GetCurrentState()=="Using Guns"&&!___targetObscured;
                if(!state.GunReady)return;
                // Undo only the native steering-only extra lead. The game's
                // ballistic prediction and native firing eligibility are retained.
                float extra=Mathf.Min(___lastTargetAngle*.05f,1f);
                state.GunPoint=___destination-___targetVel*extra-.5f*extra*(__state.Acceleration+Vector3.up*9.81f)-Vector3.up*___climbFactor;
                state.EnemyPoint=___currentTarget.GlobalPosition();
                state.GunTime=UnityEngine.Time.fixedTime;
            }
        }
        internal struct State {internal AeroPID Pitch,Roll,Yaw;internal float PitchScale,RollScale,YawScale;internal Fs20NAAimPolicy.Frame Aim;}

        [HarmonyPatch(typeof(AutopilotPlane),nameof(AutopilotPlane.AutoAim))]
        internal static class GuidanceScope
        {
            private static void Prefix(AutopilotPlane __instance,Aircraft ___aircraft,ref GlobalPosition __0,bool __1,bool __3,float __4,bool __6,Vector3 __8,out State __state)
            {
                __state=new State{Pitch=pitch,Roll=roll,Yaw=yaw,PitchScale=pitchScale,RollScale=rollScale,YawScale=yawScale,Aim=aimFrame};
                pitch=roll=yaw=null;aimFrame=default(Fs20NAAimPolicy.Frame);var gains=Gains(___aircraft);pitchScale=gains.x;rollScale=gains.y;yawScale=YawGain(___aircraft);
                if(pitchScale==1f&&rollScale==1f&&yawScale==1f){
                    if(___aircraft!=null && captures.TryGetValue(___aircraft,out var old)){old.Policy.Reset();old.Ready=false;}
                    return;
                }
                if(forward==null||pitchPid==null||rollPid==null||yawPid==null){
                    if(!warned){warned=true;VortexConventionalPlugin.ReportAssetState("FS-20 AI guidance skipped: native controller contract changed.");}
                    return;
                }
                var controller=forward.GetValue(__instance);
                if(controller==null)return;
                pitch=pitchPid.GetValue(controller) as AeroPID;
                roll=rollPid.GetValue(controller) as AeroPID;
                yaw=yawPid.GetValue(controller) as AeroPID;
                aimFrame=PrepareCapture(___aircraft,ref __0,__1,__3,__4,__6,__8,roll);
            }
            private static Exception Finalizer(Exception __exception,State __state)
            {
                pitch=__state.Pitch;roll=__state.Roll;yaw=__state.Yaw;pitchScale=__state.PitchScale;rollScale=__state.RollScale;yawScale=__state.YawScale;aimFrame=__state.Aim;
                return __exception;
            }
        }
        [HarmonyPatch(typeof(AeroPID),nameof(AeroPID.GetOutputClampP))]
        internal static class AttitudeOutput
        {
            private static void Postfix(AeroPID __instance,float __3,PIDFactors ___factors,float ___d,float ___referenceAirspeed,ref float __result)
            {
                bool pitchAxis=ReferenceEquals(__instance,pitch),yawAxis=ReferenceEquals(__instance,yaw);
                if(pitchAxis)__result*=pitchScale;
                else if(ReferenceEquals(__instance,roll))__result*=rollScale;
                else if(yawAxis)__result*=yawScale;
                if(aimFrame.Weight>0f && (pitchAxis||yawAxis)){
                    float dynamic=Mathf.Clamp(___referenceAirspeed*___referenceAirspeed/Mathf.Max(__3*__3,10f),0f,4f);
                    float coefficient=___factors.D*dynamic*(pitchAxis?pitchScale:yawScale);
                    __result=Fs20NAAimPolicy.Refine(__result,___d*coefficient,coefficient,
                        pitchAxis?aimFrame.PitchErrorRate:aimFrame.YawErrorRate,
                        pitchAxis?aimFrame.PitchFeedforward:aimFrame.YawFeedforward,aimFrame.Weight,
                        pitchAxis?aimFrame.PitchDamping:aimFrame.YawDamping);
                }
            }
        }
    }
}
