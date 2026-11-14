using System;
using System.Collections.Generic;
using HarmonyLib;
using NuclearOption.Jobs;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // One scheduler for EO and IR; only successful native LoS jobs publish contacts.
    public sealed class Fs20NARecon : MonoBehaviour
    {
        [SerializeField] private Aircraft aircraft;
        [SerializeField] private Fs20NAOpticalArray optical;
        [SerializeField] private Fs20NADAS infrared;
        [SerializeField] private Transform ventral;
        [SerializeField] private UnitPart ventralOwner;
        [SerializeField] private Fs20NAEotsView view;
        private readonly List<Unit> candidates=new List<Unit>(128);
        private readonly List<Contact> contacts=new List<Contact>(128);
        private int cursor;
        private float nextScan;
        private bool narrow;
        [SerializeField] private Vector3 focalDirection;
        private static readonly AccessTools.FieldRef<UnitPart,bool> Fragmented=AccessTools.FieldRefAccess<UnitPart,bool>("fragmented");
        private static readonly AccessTools.FieldRef<TargetDetector,Transform> Scanner=AccessTools.FieldRefAccess<TargetDetector,Transform>("scanner");
        private static readonly RaycastHit[] bodyHits=new RaycastHit[16];
        public int LastCandidates {get;private set;}
        public int LastExamined {get;private set;}
        public int LastLosRequests {get;private set;}
        public int ContactCount=>contacts.Count;
        public bool Narrow=>narrow;
        public Vector3 FocalDirection=>focalDirection;
        public bool VentralAvailable=>ventral!=null&&!Lost(ventralOwner);
        internal Transform Ventral=>ventral;
        internal static bool Lost(UnitPart part)=>part==null||part.IsDetached()||Fragmented(part);
        internal static bool ClearOwnBody(Transform hull,Transform sensor,Vector3 target)
        {
            var offset=target-sensor.position;float distance=offset.magnitude;if(distance<.05f)return false;
            var direction=offset/distance;
            int count=Physics.RaycastNonAlloc(sensor.position+direction*.025f,direction,bodyHits,Mathf.Min(30f,distance),~0,QueryTriggerInteraction.Ignore);
            if(count==bodyHits.Length)return false;
            for(int i=0;i<count;i++)if(bodyHits[i].collider!=null&&bodyHits[i].collider.transform.IsChildOf(hull))return false;
            return true;
        }
        internal void Configure(Transform root)
        {
            aircraft=root.GetComponent<Aircraft>();optical=root.GetComponentInChildren<Fs20NAOpticalArray>(true);infrared=root.GetComponentInChildren<Fs20NADAS>(true);
            foreach(var port in optical.Apertures)if(port.name=="FS20NA EO Ventral"){ventral=port;ventralOwner=port.GetComponentInParent<UnitPart>(true);}
            optical.Bind(this);infrared.Bind(this);focalDirection=ventral.forward;
            view=root.gameObject.AddComponent<Fs20NAEotsView>();view.Configure(this,aircraft);
        }
        internal void SetFocus(bool value,Vector3 direction)
        {
            if(narrow!=value)Fs20Tuning.Set(optical,"magnification",value?6f:1.5f);
            narrow=value;if(direction.sqrMagnitude>.0001f)focalDirection=direction.normalized;
        }
        internal bool CanPoint(Vector3 direction)
        {
            if(!VentralAvailable||direction.sqrMagnitude<.0001f)return false;
            direction.Normalize();
            return Vector3.Dot(ventral.forward,direction)>.173648f&&aircraft.transform.InverseTransformDirection(direction).y<.15f;
        }
        internal bool OpticalVisible(Unit target)
        {
            if(target==null||target==aircraft||target.disabled)return false;
            Vector3 direction=target.transform.position-aircraft.transform.position;Transform sensor=null;
            if(narrow) {
                if(!CanPoint(focalDirection)||Vector3.Dot(direction.normalized,focalDirection)<.994522f)return false;
                sensor=ventral;
            }else {
                float best=.05f;var ports=optical.Apertures;var owners=optical.Owners;direction.Normalize();
                for(int i=0;i<ports.Length;i++)if(ports[i]!=null&&!Lost(owners[i])) {
                    float score=Vector3.Dot(ports[i].forward,direction);if(score>best){sensor=ports[i];best=score;}
                }
                if(sensor==null)return false;
            }
            float range=Mathf.Min(narrow?20000f:12000f,target.GetVisibility()*(narrow?6f:1.5f));
            if(range<=0f||(target.transform.position-sensor.position).sqrMagnitude>range*range)return false;
            if(!ClearOwnBody(aircraft.transform,sensor,target.transform.position))return false;
            Scanner(optical)=sensor;return true;
        }
        internal void Search()
        {
            if(aircraft==null||aircraft.disabled||Time.time<nextScan)return;
            nextScan=Time.time+.5f;
            BattlefieldGrid.GetUnitsInRangeNonAlloc(aircraft.GlobalPosition(),narrow?20000f:12000f,candidates);
            ScanCandidates(candidates,true);
            for(int i=contacts.Count-1;i>=0;i--)if(contacts[i].target==null||Time.time-contacts[i].LastTime>20f)contacts.RemoveAt(i);
        }
        internal void ScanCandidates(IList<Unit> units,bool submit)
        {
            LastCandidates=units.Count;LastExamined=LastLosRequests=0;
            int budget=Mathf.Min(64,units.Count);
            for(int i=0;i<budget&&LastLosRequests<16;i++) {
                if(cursor>=units.Count)cursor=0;var target=units[cursor++];LastExamined++;
                if(target==null||target==aircraft||target.disabled||target.NetworkHQ==aircraft.NetworkHQ)continue;
                TargetDetector selected=OpticalVisible(target)?(TargetDetector)optical:infrared.ThermalVisible(target)?infrared:null;
                if(selected==null)continue;LastLosRequests++;
                if(submit)DetectorManager.RequestLoSCheck(selected,target);
            }
        }
        internal void Confirm(Unit target,bool thermal)
        {
            ConfirmSource(target,thermal?2:1);
        }
        internal void ConfirmSource(Unit target,int source)
        {
            if(target==null)return;int index=-1;
            for(int i=0;i<contacts.Count;i++)if(contacts[i].target==target){index=i;break;}
            var contact=index<0?new Contact{target=target,eo=float.NegativeInfinity,ir=float.NegativeInfinity,em=float.NegativeInfinity}:contacts[index];
            if(source==1)contact.eo=Time.time;else if(source==2)contact.ir=Time.time;else if(source==4)contact.em=Time.time;else return;
            if(index>=0)contacts[index]=contact;else{if(contacts.Count==128)contacts.RemoveAt(0);contacts.Add(contact);}
        }
        internal string ContactSummary()
        {
            int eo=0,ir=0,em=0;float now=Time.time;
            for(int i=0;i<contacts.Count;i++){if(contacts[i].target==null)continue;if(now-contacts[i].eo<=20f)eo++;if(now-contacts[i].ir<=20f)ir++;if(now-contacts[i].em<=20f)em++;}
            return "EO "+eo+" | IR "+ir+" | ESM "+em;
        }
        private struct Contact {internal Unit target;internal float eo,ir,em;internal float LastTime=>Mathf.Max(eo,Mathf.Max(ir,em));}
        [HarmonyPatch(typeof(RadarLocator),"RadarLocator_OnRadarWarning")]
        internal static class EmitterContact
        {
            private static void Postfix(RadarLocator __instance,Aircraft.OnRadarWarning __0)
            {
                var owner=(Aircraft)Fs20Tuning.Field(typeof(RadarLocator),"aircraft").GetValue(__instance);
                if(owner==null||!owner.IsServer||owner.disabled||!__instance.enabled)return;
                var parts=(UnitPart[])Fs20Tuning.Field(typeof(RadarLocator),"essentialParts").GetValue(__instance);
                if(parts!=null)for(int i=0;i<parts.Length;i++)if(Lost(parts[i]))return;
                owner.GetComponent<Fs20NARecon>()?.ConfirmSource(__0.emitter,4);
            }
        }
        [HarmonyPatch(typeof(TargetDetector),"InVisualRange")]
        internal static class OpticalGate
        {
            private static bool Prefix(TargetDetector __instance,Unit __0,ref bool __result)
            {
                if(!(__instance is Fs20NAOpticalArray))return true;
                var recon=__instance.GetComponentInParent<Fs20NARecon>(true);__result=recon!=null&&recon.OpticalVisible(__0);return false;
            }
        }
        [HarmonyPatch(typeof(TargetDetector),"VisualCheck")]
        internal static class SingleSearch
        {
            // Native radar remains available, but its duplicate unbounded visual scan is replaced.
            private static bool Prefix(TargetDetector __instance)=>__instance.GetComponentInParent<Fs20NARecon>(true)==null;
        }
        [HarmonyPatch(typeof(TargetDetector),"DetectTarget")]
        internal static class ConfirmedContact
        {
            private static bool Prefix(TargetDetector __instance,Unit __0)
            {
                if(__instance is Fs20NAOpticalArray){var recon=__instance.GetComponentInParent<Fs20NARecon>(true);return recon!=null&&recon.OpticalVisible(__0);}
                if(__instance is Fs20NADAS das)return das.ThermalVisible(__0);return true;
            }
            private static void Postfix(TargetDetector __instance,Unit __0,bool __runOriginal)
            {
                if(!__runOriginal)return;
                if(!(__instance is Fs20NAOpticalArray)&&!(__instance is Fs20NADAS))return;
                var accepted=(List<Unit>)Fs20Tuning.Field(typeof(TargetDetector),"detectedTargets").GetValue(__instance);
                if(accepted!=null&&accepted.Contains(__0))__instance.GetComponentInParent<Fs20NARecon>(true)?.Confirm(__0,__instance is Fs20NADAS);
            }
        }
    }
    // Reuses the native target-camera render/IR path: no additional Camera or RenderTexture.
    public sealed class Fs20NAEotsView : MonoBehaviour
    {
        [SerializeField] private Fs20NARecon recon;
        [SerializeField] private Aircraft aircraft;
        private TargetCam native;
        private Camera feed;
        private bool open,oldIR;
        private bool desiredIR,savedEnabled,savedTargetCanvas,savedLandingCanvas,poseReady;
        private Transform savedParent,savedMount;
        private object savedMode;
        private Unit tracked;
        private Vector3 savedPosition;
        private Quaternion savedRotation,stableRotation;
        private float savedFov,savedNear,savedFar,savedTimeout;
        private string caption="FS-20NA EOTS";
        private float captionAt;
        private static readonly AccessTools.FieldRef<TargetCam,Camera> Feed=AccessTools.FieldRefAccess<TargetCam,Camera>("cam");
        private static readonly AccessTools.FieldRef<TargetCam,List<Unit>> Targets=AccessTools.FieldRefAccess<TargetCam,List<Unit>>("targetList");
        private static readonly AccessTools.FieldRef<TargetCam,float> Timeout=AccessTools.FieldRefAccess<TargetCam,float>("camTimeout");
        private static readonly System.Reflection.MethodInfo SwitchIR=AccessTools.Method(typeof(TargetCam),"SwitchIRState");
        internal void Configure(Fs20NARecon owner,Aircraft unit){recon=owner;aircraft=unit;}
        private bool Local()=>GameManager.GetLocalAircraft(out var local)&&local==aircraft;
        private void Update()
        {
            if(!Local()||aircraft.disabled){Close();return;}
            bool alt=Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
            if(alt&&Input.GetKeyDown(KeyCode.O)){if(open)Close();else Open();}
            if(!open)return;
            if(!recon.VentralAvailable){Close();return;}
            if(alt&&Input.GetKeyDown(KeyCode.Z))recon.SetFocus(!recon.Narrow,recon.FocalDirection);
            if(alt&&Input.GetKeyDown(KeyCode.I)){desiredIR=!desiredIR;SetIR(desiredIR);}
            if(alt&&Input.GetKeyDown(KeyCode.T)){var targets=Targets(native);tracked=tracked!=null?null:targets!=null&&targets.Count>0?targets[0]:null;}
            if(tracked!=null){var direction=tracked.transform.position-recon.Ventral.position;if(tracked.disabled||direction.sqrMagnitude>400000000f||!recon.CanPoint(direction))tracked=null;else recon.SetFocus(recon.Narrow,direction);}
            if(alt){
                float yaw=(Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0);
                float pitch=(Input.GetKey(KeyCode.DownArrow)?1:0)-(Input.GetKey(KeyCode.UpArrow)?1:0);
                if(yaw!=0f||pitch!=0f)tracked=null;
                var dir=Quaternion.AngleAxis(yaw*35f*Time.unscaledDeltaTime,Vector3.up)*recon.FocalDirection;
                var right=Vector3.Cross(Vector3.up,dir).normalized;
                dir=Quaternion.AngleAxis(pitch*35f*Time.unscaledDeltaTime,right)*dir;
                recon.SetFocus(recon.Narrow,dir);
            }
            if(Time.unscaledTime>=captionAt){captionAt=Time.unscaledTime+.5f;caption="EOTS | "+(recon.Narrow?"NARROW":"WIDE")+" | "+(native.UsingIR()?"IR":"EO")+" | "+recon.ContactSummary();}
        }
        private void Open()
        {
            native=aircraft.targetCam;if(native==null||!recon.VentralAvailable)return;
            feed=Feed(native);if(feed==null||feed.targetTexture==null||(UnityEngine.Object)Fs20Tuning.Field(typeof(TargetCam),"screenVolume").GetValue(native)==null)return;
            savedParent=feed.transform.parent;savedPosition=feed.transform.localPosition;savedRotation=feed.transform.localRotation;savedFov=feed.fieldOfView;oldIR=native.UsingIR();
            savedNear=feed.nearClipPlane;savedFar=feed.farClipPlane;savedEnabled=feed.enabled;savedTimeout=Timeout(native);
            savedMode=Fs20Tuning.Field(typeof(TargetCam),"currentMode").GetValue(native);savedMount=(Transform)Fs20Tuning.Field(typeof(TargetCam),"currentMount").GetValue(native);
            savedTargetCanvas=CanvasActive("canvasObjectTarget");savedLandingCanvas=CanvasActive("canvasObjectLanding");
            native.SetTargetCam();
            desiredIR=native.UsingIR();poseReady=false;tracked=null;
            var observer=Camera.main;var direction=observer==null?recon.Ventral.forward:observer.transform.forward;
            if(!recon.CanPoint(direction))direction=recon.Ventral.forward;
            recon.SetFocus(false,direction);open=true;
        }
        internal bool ApplyFeed(Camera camera)
        {
            if(!open||camera==null||!recon.CanPoint(recon.FocalDirection))return false;
            if(native!=null){Timeout(native)=3f;if(native.UsingIR()!=desiredIR)SetIR(desiredIR);}
            var up=Mathf.Abs(Vector3.Dot(recon.FocalDirection,Vector3.up))>.98f?Vector3.forward:Vector3.up;
            var desired=Quaternion.LookRotation(recon.FocalDirection,up);
            stableRotation=poseReady?Quaternion.RotateTowards(stableRotation,desired,75f*Time.unscaledDeltaTime):desired;poseReady=true;
            camera.transform.position=recon.Ventral.position+recon.FocalDirection*.025f;
            camera.transform.rotation=stableRotation;recon.SetFocus(recon.Narrow,camera.transform.forward);
            camera.nearClipPlane=.05f;camera.farClipPlane=20000f;camera.enabled=true;
            camera.fieldOfView=recon.Narrow?12f:45f;return true;
        }
        private void Close()
        {
            if(!open)return;open=false;recon.SetFocus(false,recon.FocalDirection);
            if(feed!=null){feed.transform.SetParent(savedParent,false);feed.transform.localPosition=savedPosition;feed.transform.localRotation=savedRotation;feed.fieldOfView=savedFov;feed.nearClipPlane=savedNear;feed.farClipPlane=savedFar;feed.enabled=savedEnabled;}
            if(native!=null){Timeout(native)=savedTimeout;if(savedMode!=null)Fs20Tuning.Set(native,"currentMode",savedMode);Fs20Tuning.Set(native,"currentMount",savedMount);RestoreCanvas("canvasObjectTarget",savedTargetCanvas);RestoreCanvas("canvasObjectLanding",savedLandingCanvas);if(native.UsingIR()!=oldIR)SetIR(oldIR);}
            tracked=null;
        }
        private bool CanvasActive(string field){var canvas=(GameObject)Fs20Tuning.Field(typeof(TargetCam),field).GetValue(native);return canvas!=null&&canvas.activeSelf;}
        private void RestoreCanvas(string field,bool active){var canvas=(GameObject)Fs20Tuning.Field(typeof(TargetCam),field).GetValue(native);if(canvas!=null)canvas.SetActive(active);}
        private void SetIR(bool value)
        {
            if(native!=null&&(UnityEngine.Object)Fs20Tuning.Field(typeof(TargetCam),"screenVolume").GetValue(native)!=null)SwitchIR.Invoke(native,new object[]{value});
        }
        private void OnDisable(){Close();}
        private void OnGUI()
        {
            if(!open||feed==null||!Local())return;
            float width=Mathf.Min(420f,Screen.width*.34f),height=width*.65f;var panel=new Rect(Screen.width-width-24f,72f,width,height+90f);
            GUI.Box(panel,caption);
            if(recon.CanPoint(recon.FocalDirection))GUI.DrawTexture(new Rect(panel.x+6,panel.y+25,width-12,height),feed.targetTexture,ScaleMode.ScaleToFit,false);
            else GUI.Label(new Rect(panel.x+10,panel.y+50,width-20,40),"EOTS FIELD OF REGARD EXCEEDED");
            GUI.Label(new Rect(panel.x+8,panel.y+height+28,width-12,58),"Alt+O close | Alt+Z zoom | Alt+I EO/IR\nAlt+arrows: sensor direction\nAlt+T: track native selected contact");
        }
        [HarmonyPatch(typeof(TargetCam),"Update")]
        internal static class StabilizedNativeFeed
        {
            private static void Postfix(TargetCam __instance)
            {
                var owner=(Aircraft)Fs20Tuning.Field(typeof(TargetCam),"aircraft").GetValue(__instance);
                if(owner!=null)owner.GetComponent<Fs20NAEotsView>()?.ApplyFeed(Feed(__instance));
            }
        }
    }
}
