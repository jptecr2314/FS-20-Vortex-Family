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
        private static readonly Dictionary<Fs20NANightScheme,Texture2D> NativeSources=new Dictionary<Fs20NANightScheme,Texture2D>();
        private static readonly Dictionary<Texture2D,Color32> TextureTints=new Dictionary<Texture2D,Color32>();
        private static readonly Dictionary<Fs20NANightScheme,Color32> CoatTints=new Dictionary<Fs20NANightScheme,Color32>();
        private static readonly Dictionary<Texture2D,Texture2D> NativeWings=new Dictionary<Texture2D,Texture2D>();
        internal static void SetNativeReference(Fs20NANightScheme choice,Texture2D texture){if(texture!=null){if(!NativeSources.TryGetValue(choice,out var old)||old!=texture){NativeSources[choice]=texture;CoatTints.Remove(choice);}}}
        internal static bool TryNativeReference(Fs20NANightScheme choice,out Texture2D texture)=>NativeSources.TryGetValue(choice,out texture)&&texture!=null;
        private static bool AcceptNative(LiveryData data,out Fs20NANightScheme choice){
            choice=Fs20NANightScheme.NightGraphite;if(data==null)return false;
            string[] names={"SmallFighter1_BDF_navy","SmallFighter1_BDF_hex","","SmallFighter1_PALA_desert","SmallFighter1_PALA_black"};
            for(int i=0;i<names.Length;i++)if(names[i].Length>0&&data.name.StartsWith(names[i],StringComparison.OrdinalIgnoreCase)){
                choice=(Fs20NANightScheme)i;SetNativeReference(choice,data.Texture);return true;
            }
            return false;
        }
        private const string Owned="FS20NA finish ";
        private static Texture2D matte;
        private static readonly Dictionary<string,Texture2D> Cache=new Dictionary<string,Texture2D>();
        private static readonly Dictionary<Texture2D,Texture2D> Originals=new Dictionary<Texture2D,Texture2D>();
        private static Texture2D bodyNeutral;
        private static readonly Dictionary<string,LiveryData> NativeLiveries=new Dictionary<string,LiveryData>();
        private static readonly Dictionary<string,Color32[]> Masks=new Dictionary<string,Color32[]>();
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
        internal void Select(Fs20NANightScheme value){selection=value;hasSelection=true;Refresh();Queue();}
        internal static Texture2D Palette(Fs20NANightScheme scheme)=>Convert(bodyNeutral!=null?bodyNeutral:Fs20TDeltaPaint.NeutralAtlas,scheme,"body");
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
                        if(!CoatTints.TryGetValue(CurrentScheme,out var tint)){Palette(CurrentScheme);tint=CoatTints[CurrentScheme];}
                        foreach(string colorKey in new[]{"_Color","_BaseColor"})if(material.HasProperty(colorKey)){material.SetColor(colorKey,((Color)tint).linear);block.SetColor(colorKey,((Color)tint).linear);}
                        Finish(material,block);renderer.SetPropertyBlock(block,slot);block.Clear();renderer.sharedMaterials=slots;continue;
                    }
                    renderer.GetPropertyBlock(block,slot);
                    if(!wing && bodyNeutral==null)bodyNeutral=material.GetTexture("_Basecolor") as Texture2D;
                    var neutral=wing?Fs20TDeltaPaint.NeutralAtlas:bodyNeutral;
                    if(neutral==null)continue;
                    var texture=Convert(neutral,CurrentScheme,wing?"wing":"body");
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
            if(source==null)return null;
            var neutral=bodyNeutral!=null?bodyNeutral:source.Texture;
            if(neutral==null)return source;
            string key=source.GetInstanceID()+":"+CurrentScheme;
            if(NativeLiveries.TryGetValue(key,out var result)&&result!=null)return result;
            result=UnityEngine.Object.Instantiate(source);result.name="FS20NA native "+CurrentScheme;
            result.Texture=Convert(neutral,CurrentScheme,"body");result.Glossiness=.05f;
            NativeLiveries.Add(key,result);return result;
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
                    if(__0!=null&&__0.name.StartsWith("FS20NA native ",StringComparison.Ordinal)&&Enum.TryParse(__0.name.Substring("FS20NA native ".Length),out Fs20NANightScheme choice))paint.Select(choice);
                    if(AcceptNative(__0,out Fs20NANightScheme nativeChoice))paint.Select(nativeChoice);
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
                if(paint!=null&&!(__0!=null&&__0.name.StartsWith("FS20NA native ",StringComparison.Ordinal)))__0=paint.NativeLivery(__0);
            }
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(UnitPart __instance)
            {
                var paint=Owner(__instance);
                if(paint!=null){paint.ApplyTo(__instance.GetComponentsInChildren<Renderer>(true));paint.Queue();}
            }
        }
        private static Color32[] Mask(string role)
        {
            if(Masks.TryGetValue(role,out var colors))return colors;
            using(var stream=typeof(Fs20NANightPaint).Assembly.GetManifestResourceStream("Fs20NA_pattern_"+role+".png"))
            using(var bytes=new System.IO.MemoryStream()) {
                if(stream==null)throw new InvalidOperationException("Missing NA spatial paint mask.");
                stream.CopyTo(bytes);var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                try{ImageConversion.LoadImage(image,bytes.ToArray());if(image.width!=1024||image.height!=1024)throw new InvalidOperationException("NA paint mask size changed.");colors=image.GetPixels32();Masks.Add(role,colors);return colors;}
                finally{UnityEngine.Object.DestroyImmediate(image);}
            }
        }
        private static Texture2D Convert(Texture2D source,Fs20NANightScheme scheme,string role)
        {
            if(Originals.TryGetValue(source,out var original))source=original;
            var nativeChoice=(int)scheme==5?Fs20NANightScheme.PalaMidnightBlue:scheme;
            bool native=NativeSources.TryGetValue(nativeChoice,out var reference)&&reference!=null;
            if(native){
                if(role=="body")source=reference;
                else {if(!NativeWings.TryGetValue(reference,out var projected)){projected=Fs20TDeltaPaint.ProjectNativeLivery(reference);NativeWings.Add(reference,projected);}source=projected;}
            }
            string key=source.GetInstanceID()+":"+scheme+":"+role;if(Cache.TryGetValue(key,out var cached)&&cached!=null){if(role=="body"&&TextureTints.TryGetValue(cached,out var savedTint))CoatTints[scheme]=savedTint;return cached;}
            bool oldSrgb=GL.sRGBWrite;
            var previous=RenderTexture.active;var buffer=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            Texture2D result=null;
            try {
                if(!source.isReadable){GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(source,buffer);RenderTexture.active=buffer;}
                result=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,false){name="FS20NA "+scheme+" "+source.name,wrapMode=source.wrapMode,filterMode=FilterMode.Trilinear,anisoLevel=8};
                Color32[] colors;
                if(source.isReadable)colors=source.GetPixels32();
                else {result.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);colors=result.GetPixels32();}
                var mask=Mask(role);var marks=MarkMask(scheme,role);
                Color32[] nativeBasis=null;int basisWidth=0,basisHeight=0;
                if(native&&role=="body")nativeBasis=Fs20TNativePaint.CoatBasis(out basisWidth,out basisHeight);
                for(int i=0;i<colors.Length;i++) {
                    var c=colors[i];float l=(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;
                    var m=mask[((i/source.width)*1024/source.height)*1024+(i%source.width)*1024/source.width];
                    float detail=Mathf.Clamp((l-.42f)*4f,-2f,2f);
                    Color32 paint;
                    if(native){
                        // Match the wing's native/base transfer on retained skin too.
                        // Otherwise donor baked albedo darkens the spine/radome twice.
                        var basis=nativeBasis==null?new Color32(103,103,103,255):nativeBasis[((i/source.width)*basisHeight/source.height)*basisWidth+(i%source.width)*basisWidth/source.width];
                        var coat=new Color32((byte)Mathf.Min(255f,c.r*103f/Mathf.Max(24,basis.r)),(byte)Mathf.Min(255f,c.g*103f/Mathf.Max(24,basis.g)),(byte)Mathf.Min(255f,c.b*103f/Mathf.Max(24,basis.b)),c.a);
                        if(scheme==Fs20NANightScheme.BdfForestDigital){
                            int high=Math.Max(coat.r,Math.Max(coat.g,coat.b));
                            paint=high<35?new Color32(17,21,24,255):high<95?new Color32(15,23,17,255):high<150?new Color32(23,34,25,255):high<205?new Color32(35,46,34,255):new Color32(63,70,66,255);
                        }else paint=new Color32((byte)Mathf.RoundToInt(coat.r*.42f),(byte)Mathf.RoundToInt(coat.g*.42f),(byte)Mathf.RoundToInt(coat.b*.42f),c.a);
                        detail=0f;
                    }else if(scheme==Fs20NANightScheme.BdfForestDigital){
                        int band=Mathf.Clamp(Mathf.RoundToInt(m.g/85f),0,3);
                        paint=band==0?new Color32(15,23,17,255):band==1?new Color32(23,34,25,255):band==2?new Color32(35,46,34,255):new Color32(17,21,24,255);
                    }else {
                        // Authored broad-edged blocks inspired by the cited F-35
                        // photographs; BDF insignia, no US service marks copied.
                        int band=Mathf.Clamp(Mathf.RoundToInt(m.b/85f),0,3);
                        paint=band==0?new Color32(28,34,40,255):band==1?new Color32(13,17,21,255):band==2?new Color32(46,53,60,255):new Color32(21,27,32,255);
                    }
                    var mark=marks[((i/source.width)*2048/source.height)*2048+(i%source.width)*2048/source.width];
                    float alpha=native&&role=="body"?0f:mark.a/255f;
                    colors[i]=new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(Mathf.Clamp(paint.r+detail,0,255),mark.r,alpha)),(byte)Mathf.RoundToInt(Mathf.Lerp(Mathf.Clamp(paint.g+detail,0,255),mark.g,alpha)),(byte)Mathf.RoundToInt(Mathf.Lerp(Mathf.Clamp(paint.b+detail,0,255),mark.b,alpha)),c.a);
                }
                if(native&&scheme==Fs20NANightScheme.NightGraphite)LevelStandardCoat(colors,source.width,source.height);
                if(role=="body"){
                    long red=0,green=0,blue=0;int count=0;
                    for(int i=0;i<colors.Length;i+=1024){var sample=colors[i];int high=Math.Max(sample.r,Math.Max(sample.g,sample.b));if(high<12||high>220)continue;red+=sample.r;green+=sample.g;blue+=sample.b;count++;}
                    CoatTints[scheme]=count>0?new Color32((byte)(red/count),(byte)(green/count),(byte)(blue/count),255):new Color32(30,30,30,255);TextureTints[result]=CoatTints[scheme];
                }
                result.SetPixels32(colors);result.Apply(true,false);result.Compress(true);result.Apply(true,false);Cache.Add(key,result);Originals.Add(result,source);return result;
            }catch{if(result!=null)UnityEngine.Object.DestroyImmediate(result);throw;}
            finally{GL.sRGBWrite=oldSrgb;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(buffer);}
        }
        // Remove broad donor panel-tone islands from the plain Navy coat while
        // retaining fine native stencils/edges and coloured identification.
        // Weighted separable box filter: texture load/cache time only, never a
        // renderer/light/emission compensation and never part of the flight loop.
        private static void LevelStandardCoat(Color32[] pixels,int width,int height){
            int radius=Math.Max(3,width*6/2048);var sums=new float[pixels.Length];var weights=new float[pixels.Length];
            for(int y=0;y<height;y++){
                float total=0,count=0;int row=y*width;
                for(int x=-radius;x<width;x++){
                    int add=x+radius;if(add<width){var p=pixels[row+add];if(Math.Max(p.r,Math.Max(p.g,p.b))>=6){total+=.2126f*p.r+.7152f*p.g+.0722f*p.b;count++;}}
                    int remove=x-radius-1;if(remove>=0){var p=pixels[row+remove];if(Math.Max(p.r,Math.Max(p.g,p.b))>=6){total-=.2126f*p.r+.7152f*p.g+.0722f*p.b;count--;}}
                    if(x>=0){sums[row+x]=total;weights[row+x]=count;}
                }
            }
            for(int x=0;x<width;x++){
                float total=0,count=0;
                for(int y=-radius;y<height;y++){
                    int add=y+radius;if(add<height){total+=sums[add*width+x];count+=weights[add*width+x];}
                    int remove=y-radius-1;if(remove>=0){total-=sums[remove*width+x];count-=weights[remove*width+x];}
                    if(y<0||count<.5f)continue;int i=y*width+x;var p=pixels[i];float local=total/count;
                    pixels[i]=new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(43+p.r-local),0,107),(byte)Mathf.Clamp(Mathf.RoundToInt(45+p.g-local),0,107),(byte)Mathf.Clamp(Mathf.RoundToInt(48+p.b-local),0,107),p.a);
                }
            }
        }
        // Authored UV overlays are baked into the cached albedo on selection.
        // No extra renderer, material pass, emission or per-frame texture work.
        private static Color32[] MarkMask(Fs20NANightScheme scheme,string role)
        {
            string key="marks_"+((int)scheme>=3&&(int)scheme<=5?"pala":"bdf")+"_"+role;
            if(Masks.TryGetValue(key,out var colors))return colors;
            using(var stream=typeof(Fs20NANightPaint).Assembly.GetManifestResourceStream("Fs20NA_"+key+".png"))
            using(var bytes=new System.IO.MemoryStream()) {
                if(stream==null)throw new InvalidOperationException("Missing NA faction markings.");
                stream.CopyTo(bytes);var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                try{if(!ImageConversion.LoadImage(image,bytes.ToArray())||image.width!=2048||image.height!=2048)throw new InvalidOperationException("NA marking map size changed.");colors=image.GetPixels32();Masks.Add(key,colors);return colors;}
                finally{UnityEngine.Object.DestroyImmediate(image);}
            }
        }
    }
}
