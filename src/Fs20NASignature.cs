using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Positive game signatures. This never hides a Unit from registries or clears enemy intelligence.
    public sealed class Fs20NASignature : MonoBehaviour
    {
        [SerializeField] private Aircraft aircraft;
        [SerializeField] private BayDoor[] doors;
        [SerializeField] private AeroPart[] bayOwners;
        [SerializeField] private LandingGear[] gears;
        [SerializeField] private UnitPart[] skinParts;
        [SerializeField] private float[] initialHealth;
        [SerializeField] private Turbojet engine;
        [SerializeField] private NavLights navLights;
        private float appliedRadar,appliedOptical,nextUpdate;
        private bool radarInitialized;
        private static readonly AccessTools.FieldRef<Unit,float> Visibility=AccessTools.FieldRefAccess<Unit,float>("visibility");
        private static readonly AccessTools.FieldRef<UnitPart,float> Health=AccessTools.FieldRefAccess<UnitPart,float>("hitPoints");
        private static readonly AccessTools.FieldRef<BayDoor,float> BayAmount=AccessTools.FieldRefAccess<BayDoor,float>("openAmount");
        private static readonly AccessTools.FieldRef<LandingGear,float> GearAmount=AccessTools.FieldRefAccess<LandingGear,float>("foldAmount");
        private static readonly AccessTools.FieldRef<Turbojet,float> Condition=AccessTools.FieldRefAccess<Turbojet,float>("condition");
        private static readonly AccessTools.FieldRef<Turbojet,bool> Fire=AccessTools.FieldRefAccess<Turbojet,bool>("engineFire");
        private static readonly AccessTools.FieldRef<Turbojet,bool> Burner=AccessTools.FieldRefAccess<Turbojet,bool>("afterburnerOn");
        private static readonly AccessTools.FieldRef<NavLights,bool> Lights=AccessTools.FieldRefAccess<NavLights,bool>("isOn");
        private static readonly AccessTools.FieldRef<LevelInfo,bool> Day=AccessTools.FieldRefAccess<LevelInfo,bool>("isDayLight");
        public float AddedRadarSignature=>appliedRadar;
        public float ThermalMultiplier=>engine==null?1f:Fire(engine)?1f:Mathf.Lerp(Burner(engine)?.45f:.20f,1f,1f-Mathf.Clamp01(Condition(engine)));
        internal void Configure(Transform root)
        {
            aircraft=root.GetComponent<Aircraft>();engine=root.GetComponentInChildren<Turbojet>(true);navLights=root.GetComponentInChildren<NavLights>(true);
            doors=root.GetComponentsInChildren<BayDoor>(true).Where(d=>d.name=="FS20 continuous bay door").ToArray();
            bayOwners=doors.Select(d=>d.GetComponentInParent<AeroPart>(true)).ToArray();
            gears=root.GetComponentsInChildren<LandingGear>(true);
            skinParts=root.GetComponentsInChildren<UnitPart>(true);
            initialHealth=skinParts.Select(p=>Mathf.Max(.001f,Health(p))).ToArray();
            foreach(var radar in root.GetComponentsInChildren<Radar>(true))Fs20Tuning.Set(radar,"activated",false);
            foreach(var locator in root.GetComponentsInChildren<RadarLocator>(true))Fs20Tuning.Set(locator,"onlySurface",false);
            foreach(var nozzle in root.GetComponentsInChildren<JetNozzle>(true))nozzle.gameObject.AddComponent<Fs20NAThermalSource>().Configure(this,nozzle);
        }
        internal bool RadarInitialized=>radarInitialized;
        internal void StartPassive(Radar radar){Fs20Tuning.Set(radar,"activated",false);radarInitialized=true;}
        private void OnEnable(){appliedRadar=appliedOptical=0f;nextUpdate=0f;radarInitialized=false;}
        private void Update()
        {
            if(aircraft==null||aircraft.disabled||Time.time<nextUpdate)return;nextUpdate=Time.time+.2f;
            StepSignature(LevelInfo.i==null?1f:Day(LevelInfo.i)?1f:0f,navLights!=null&&Lights(navLights));
        }
        internal void StepSignature(float daylight,bool navigationLights)
        {
            if(aircraft==null||aircraft.definition==null)return;
            float extra=0f,damage=0f;
            for(int i=0;i<doors.Length;i++) {
                bool exposed=bayOwners[i]==null||bayOwners[i].IsDetached()||Fs20NARecon.Lost(bayOwners[i]);
                extra+=.00025f*(exposed?1f:doors[i]==null?1f:Mathf.Clamp01(BayAmount(doors[i])));
            }
            for(int i=0;i<gears.Length;i++)if(gears[i]!=null&&!LostGear(gears[i]))extra+=.0003f*(1f-Mathf.Clamp01(GearAmount(gears[i])))/Mathf.Max(1,gears.Length);
            for(int i=0;i<skinParts.Length;i++)if(skinParts[i]!=null)damage=Mathf.Max(damage,Fs20NARecon.Lost(skinParts[i])?1f:1f-Mathf.Clamp01(Health(skinParts[i])/initialHealth[i]));
            extra+=damage*damage*damage*.0004f;
            aircraft.ModifyRCS(extra-appliedRadar);appliedRadar=extra;
            float optical=Mathf.Lerp(350f,1250f,Mathf.Clamp01(daylight));
            optical+=navigationLights?2500f:0f;optical+=damage*1000f;
            if(engine!=null){if(Burner(engine))optical+=1000f;if(Fire(engine))optical+=4000f;}
            float modifier=optical-aircraft.definition.visibleRange;
            Visibility(aircraft)+=modifier-appliedOptical;appliedOptical=modifier;
        }
        private static bool LostGear(LandingGear gear){var part=gear.GetComponent<UnitPart>();return part!=null&&Fs20NARecon.Lost(part);}
        private void OnDisable()
        {
            if(aircraft!=null){aircraft.ModifyRCS(-appliedRadar);Visibility(aircraft)-=appliedOptical;}
            appliedRadar=appliedOptical=0f;
        }
        internal static void BindFormationCompatibility(Harmony harmony)
        {
            // QoL's slimelights option skips the native Toggle and installs the
            // luminous material even for Toggle(false). Its night gear rule also
            // requests Toggle(true). Bypass only those two QoL prefixes for NA
            // fuselage strips, leaving native state/manual control and all other
            // aircraft and lights under the user's existing QoL settings.
            foreach(var entry in new[]{new[]{"QOLPlugin+NavLightsPatch2","Prefix_Toggle"},new[]{"QOLPlugin+NavLightsPatch","Prefix_NavLight_OnSetGear"}}){
                var type=AccessTools.TypeByName(entry[0]);if(type==null)continue;
                var target=AccessTools.Method(type,entry[1]);
                if(target==null||target.ReturnType!=typeof(bool)||target.GetParameters().Length==0||target.GetParameters()[0].ParameterType!=typeof(object))continue;
                harmony.Patch(target,prefix:new HarmonyMethod(typeof(FormationLightQolCompatibility),"AllowNative"){priority=Priority.First});
                VortexConventionalPlugin.ReportAssetState("FS20NA formation-light compatibility bound: "+entry[0]+"."+entry[1]+"; other lights retain QoL behavior.");
            }
        }
        private static class FormationLightQolCompatibility
        {
            private static readonly Type LightType=typeof(NavLights).GetNestedType("NavLight",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            private static readonly System.Reflection.FieldInfo RendererField=AccessTools.Field(LightType,"renderer");
            private static readonly System.Reflection.FieldInfo AircraftField=AccessTools.Field(LightType,"aircraft");
            private static bool AllowNative(object __0,ref bool __result)
            {
                var renderer=__0==null?null:RendererField.GetValue(__0) as Renderer;
                if(renderer==null||!renderer.name.StartsWith("slimeLights_",StringComparison.Ordinal))return true;
                var owner=AircraftField.GetValue(__0) as Aircraft;
                if(owner==null||owner.GetComponent<Fs20NASignature>()==null)return true;
                __result=true;return false;
            }
        }
        [HarmonyPatch]
        internal static class FormationLightDefaults
        {
            private static readonly Type LightType=typeof(NavLights).GetNestedType("NavLight",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            private static readonly System.Reflection.FieldInfo RendererField=AccessTools.Field(LightType,"renderer");
            private static readonly System.Reflection.FieldInfo ControlField=AccessTools.Field(LightType,"controlState");
            private static System.Reflection.MethodBase TargetMethod()=>AccessTools.Method(LightType,"Initialize");
            private static void Prefix(object __instance,Aircraft __0)
            {
                if(__0==null||__0.GetComponent<Fs20NASignature>()==null)return;
                var renderer=(Renderer)RendererField.GetValue(__instance);
                if(renderer==null||!renderer.name.StartsWith("slimeLights_",StringComparison.Ordinal))return;
                // Fresh NA instances start in native Auto, even if the donor or
                // pooled light inherited ForceOn. Subsequent manual commands and
                // gear callbacks are entirely native; no recurring blackout gate.
                ControlField.SetValue(__instance,Enum.ToObject(ControlField.FieldType,1));
            }
        }
        [HarmonyPatch(typeof(TargetDetector),"TargetDetector_OnInitialize")]
        internal static class PassiveStart
        {
            private static void Postfix(TargetDetector __instance)
            {
                if(__instance is Radar radar)__instance.GetComponentInParent<Fs20NASignature>(true)?.StartPassive(radar);
            }
        }
        [HarmonyPatch(typeof(Radar),"TargetSearch")]
        internal static class NoStartupPulse
        {
            private static bool Prefix(Radar __instance){var marker=__instance.GetComponentInParent<Fs20NASignature>(true);return marker==null||marker.RadarInitialized;}
        }
    }
    public sealed class Fs20NAThermalSource : MonoBehaviour
    {
        [SerializeField] private Fs20NASignature signature;
        [SerializeField] private UnitPart part;
        private static readonly AccessTools.FieldRef<JetNozzle,IRSource> Source=AccessTools.FieldRefAccess<JetNozzle,IRSource>("irSource");
        private static readonly AccessTools.FieldRef<IRSource,float> Intensity=AccessTools.FieldRefAccess<IRSource,float>("intensity");
        internal void Configure(Fs20NASignature owner,JetNozzle nozzle){signature=owner;part=(UnitPart)Fs20Tuning.Field(typeof(JetNozzle),"part").GetValue(nozzle);}
        internal float Multiplier=>signature==null||part==null||Fs20NARecon.Lost(part)?1f:signature.ThermalMultiplier;
        internal void Apply(JetNozzle nozzle){var source=Source(nozzle);if(source!=null)Intensity(source)=Mathf.Max(0f,Intensity(source))*Multiplier;}
        [HarmonyPatch(typeof(JetNozzle),"Thrust")]
        internal static class ThermalOutput
        {
            private static void Postfix(JetNozzle __instance){var marker=__instance.GetComponent<Fs20NAThermalSource>();if(marker!=null)marker.Apply(__instance);}
        }
    }
}
