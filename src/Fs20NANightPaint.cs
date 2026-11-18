using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Preserve serialized IDs: standard, hex, photo, desert, black, legacy black, forest hex.
    public enum Fs20NANightScheme { NightGraphite, MidnightBlue, LowContrastCharcoal, PalaNightGraphite, PalaMidnightBlue, PalaLowContrastCharcoal, BdfForestDigital }
    public sealed class Fs20NANightPaint : MonoBehaviour
    {
        private const string Owned="FS20NA finish ";
        private static Texture2D matte;
        private readonly HashSet<Material> ownedMaterials=new HashSet<Material>();
        private enum SurfaceRole { Protected,Body,Wing,Lod }
        [Serializable] private sealed class SurfaceSlot {
            public Renderer renderer;public int slot;public SurfaceRole role;public UnitPart owner;
            public bool hasLivery,hasGloss;public Texture originalLivery;public float originalGloss;
        }
        [SerializeField] private SurfaceSlot[] surfaces;
        private int pending=3;
        private bool reported;
        internal static Fs20NANightScheme Scheme=Fs20NANightScheme.NightGraphite;
        [SerializeField] private bool hasSelection;
        [SerializeField] private Fs20NANightScheme selection;
        internal Fs20NANightScheme CurrentScheme=>hasSelection?selection:Scheme;
        internal void Select(Fs20NANightScheme value){selection=value;hasSelection=true;Fs20PreparedLiveries.Select(GetComponent<Aircraft>(),Fs20PreparedLiveries.Scheme("NA",(int)value));Refresh();Queue();}
        internal static Texture2D Palette(Fs20NANightScheme scheme){var p=Fs20PreparedLiveries.Scheme("NA",(int)scheme);p.Load();return p.bodyTexture;}
        private void Start(){Refresh();Queue();}
        internal void Queue(){if(pending==0)pending=3;}
        private void LateUpdate(){if(pending>0 && --pending==0)Refresh();}
        internal void Refresh()
        {
            ApplyTo(null);
        }
        private static bool Exhaust(Transform node,Transform root)
        {
            for(var t=node;t!=null&&t!=root;t=t.parent)
                if(t.name=="nozzle"||t.name.StartsWith("nozzle_",StringComparison.Ordinal)||t.name.StartsWith("swivelDuct",StringComparison.Ordinal))return true;
            return false;
        }
        private void BindSurfaces()
        {
            if(surfaces!=null)return;
            var entries=new List<SurfaceSlot>();
            foreach(var renderer in GetComponentsInChildren<Renderer>(true)){
                var slots=renderer.sharedMaterials;var owner=renderer.GetComponentInParent<UnitPart>(true);
                bool exhaust=Exhaust(renderer.transform,transform);
                for(int i=0;i<slots.Length;i++){
                    var material=slots[i];if(material==null)continue;
                    string key=material.name;while(key.StartsWith(Owned,StringComparison.Ordinal))key=key.Substring(Owned.Length);
                    SurfaceRole role=SurfaceRole.Protected;
                    if(!exhaust){
                        if(key.StartsWith("FS20T delta paint",StringComparison.Ordinal))role=SurfaceRole.Wing;
                        else if(key.StartsWith("SmallFighter1_skin",StringComparison.Ordinal)||key.StartsWith("FS20 CTOL paint SmallFighter1_skin",StringComparison.Ordinal))role=SurfaceRole.Body;
                        else if(key=="LOD"||key.StartsWith("LOD ",StringComparison.Ordinal))role=SurfaceRole.Lod;
                    }
                    bool livery=material.HasProperty("_Livery"),gloss=material.HasProperty("_Glossiness");
                    entries.Add(new SurfaceSlot{renderer=renderer,slot=i,role=role,owner=owner,hasLivery=livery,hasGloss=gloss,originalLivery=livery?material.GetTexture("_Livery"):null,originalGloss=gloss?material.GetFloat("_Glossiness"):0f});
                }
            }
            surfaces=entries.ToArray();
        }
        private void ApplyTo(Renderer[] renderers)
        {
            BindSurfaces();
            var prepared=Fs20PreparedLiveries.Current(GetComponent<Aircraft>(),"NA",(int)CurrentScheme);prepared.Load();
            var requested=renderers==null?null:new HashSet<Renderer>(renderers);
            var block=new MaterialPropertyBlock();
            foreach(var surface in surfaces) {
                var renderer=surface.renderer;
                if(renderer==null||(surface.owner!=null&&surface.owner.IsDetached())||(requested!=null&&!requested.Contains(renderer)))continue;
                var slots=renderer.sharedMaterials;
                int slot=surface.slot;if(slot<0||slot>=slots.Length)continue;
                {
                    var material=slots[slot];if(material==null)continue;
                    string key=material.name.StartsWith(Owned,StringComparison.Ordinal)?material.name.Substring(Owned.Length):material.name;
                    if(surface.role==SurfaceRole.Protected){
                        // UnitPart.SetLivery also writes technical renderers. Restore
                        // only those native livery channels, preserving heat/emission,
                        // screen textures and all other runtime material properties.
                        if(surface.hasLivery||surface.hasGloss){
                            renderer.GetPropertyBlock(block,slot);
                            if(surface.hasLivery){material.SetTexture("_Livery",surface.originalLivery);if(surface.originalLivery!=null)block.SetTexture("_Livery",surface.originalLivery);}
                            if(surface.hasGloss){material.SetFloat("_Glossiness",surface.originalGloss);block.SetFloat("_Glossiness",surface.originalGloss);}
                            renderer.SetPropertyBlock(block,slot);block.Clear();
                        }
                        continue;
                    }
                    bool lod=surface.role==SurfaceRole.Lod;
                    bool wing=surface.role==SurfaceRole.Wing;
                    // Native SetLivery writes renderer.material; MPBs alone did not make the
                    // night finish durable through material instantiation and deferred liveries.
                    if(!ownedMaterials.Contains(material)){
                        material=new Material(material){name=Owned+key};ownedMaterials.Add(material);slots[slot]=material;
                    }
                    if(lod) {
                        renderer.GetPropertyBlock(block,slot);
                        var tint=prepared.Tint;
                        foreach(string colorKey in new[]{"_Color","_BaseColor"})if(material.HasProperty(colorKey)){material.SetColor(colorKey,((Color)tint).linear);block.SetColor(colorKey,((Color)tint).linear);}
                        Finish(material,block);renderer.SetPropertyBlock(block,slot);block.Clear();renderer.sharedMaterials=slots;continue;
                    }
                    renderer.GetPropertyBlock(block,slot);
                    var texture=wing?prepared.wingTexture:prepared.bodyTexture;
                    foreach(string map in new[]{"_Basecolor","_BaseMap","_Livery","_BasecolorDmg"})
                        if(material.HasProperty(map)||block.GetTexture(map)!=null){material.SetTexture(map,texture);block.SetTexture(map,texture);}
                    Finish(material,block);renderer.SetPropertyBlock(block,slot);block.Clear();
                }
                renderer.sharedMaterials=slots;
            }
        }
        private void OnDestroy(){foreach(var material in ownedMaterials)if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}ownedMaterials.Clear();}
        private static void Finish(Material material,MaterialPropertyBlock block)
        {
            // Stock baked AO describes the donor's seams and VTOL hardware,
            // while the delta extensions already use neutral AO. Use one coat
            // response on painted exterior slots; protected cavities are skipped
            // by ApplyTo and retain native AO/heat/glass. Runtime shadows remain.
            if(material.HasProperty("_AO")){material.SetTexture("_AO",Texture2D.whiteTexture);block.SetTexture("_AO",Texture2D.whiteTexture);}

            int metalIndex=material.shader.FindPropertyIndex("_Metallic");
            if(metalIndex>=0&&material.shader.GetPropertyType(metalIndex)==UnityEngine.Rendering.ShaderPropertyType.Texture) {
                if(matte==null){matte=new Texture2D(1,1,TextureFormat.RGBA32,false,true){name="FS20NA matte surface response"};matte.SetPixel(0,0,new Color32(0,0,0,13));matte.Apply(false,true);}
                material.SetTexture("_Metallic",matte);block.SetTexture("_Metallic",matte);
            }
            foreach(string name in new[]{"_Glossiness","_Smoothness","_Metallic"}) {
                int index=material.shader.FindPropertyIndex(name);if(index<0)continue;
                var type=material.shader.GetPropertyType(index);
                if(type!=UnityEngine.Rendering.ShaderPropertyType.Float&&type!=UnityEngine.Rendering.ShaderPropertyType.Range)continue;
                float value=name=="_Metallic"?0f:.05f;material.SetFloat(name,value);block.SetFloat(name,value);
            }
        }
        private static Fs20NANightPaint Owner(UnitPart part)
        {
            var owner=part.parentUnit;
            return owner!=null?owner.GetComponent<Fs20NANightPaint>():part.GetComponentInParent<Fs20NANightPaint>(true);
        }
        private LiveryData NativeLivery(LiveryData source)
        {
            var aircraft=GetComponent<Aircraft>();var prepared=Fs20PreparedLiveries.Resolve("NA",source,(int)CurrentScheme);
            hasSelection=true;selection=(Fs20NANightScheme)prepared.scheme;Fs20PreparedLiveries.Select(aircraft,prepared);return prepared.Data;
        }
        // Supply the native livery itself, rather than relying on a renderer name
        // and a later MPB write. UnitPart can live outside the aircraft hierarchy.
        [HarmonyPatch(typeof(LiveryBehaviour),"SetLivery")]
        internal static class NativePaint
        {
            private static void Prefix(Aircraft ___aircraft,ref LiveryData __0)
            {
                var paint=___aircraft==null?null:___aircraft.GetComponent<Fs20NANightPaint>();
                if(paint!=null){
                    __0=paint.NativeLivery(__0);
                }
            }
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Aircraft ___aircraft)
            {
                var paint=___aircraft==null?null:___aircraft.GetComponent<Fs20NANightPaint>();
                if(paint!=null){paint.Refresh();paint.Queue();if(!paint.reported){paint.reported=true;VortexConventionalPlugin.ReportAssetState("FS20NA native night livery applied: "+Scheme+"; native part lookup and renderer finish reconciled.");}}
            }
        }
        [HarmonyPatch(typeof(UnitPart),"SetLivery")]
        internal static class PartLivery
        {
            private static void Prefix(UnitPart __instance,ref LiveryData __0)
            {
                var paint=Owner(__instance);
                if(paint!=null&&!(__0!=null&&__0.name.StartsWith("FS20 prepared ",StringComparison.Ordinal)))__0=paint.NativeLivery(__0);
            }
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(UnitPart __instance)
            {
                var paint=Owner(__instance);
                if(paint!=null){paint.ApplyTo(__instance.GetComponentsInChildren<Renderer>(true));paint.Queue();}
            }
        }
    }
}
