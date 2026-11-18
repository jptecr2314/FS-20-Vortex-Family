using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
namespace Japensou.NuclearOption.VortexConventional {
internal static class Fs20PreparedLiveries {
 const string Prefix="FS20 prepared ",Provider="FS20.PreparedPaint.v1";
 [Serializable] internal sealed class Pack {public int schemaVersion;public string packageId,version,author,basisFile,basisSha256;public Paint[] paints;}
 [Serializable] internal sealed class Paint {
  public string id,variant,displayName,faction,body,wing,bundleFile,bundleSha256;public int scheme;public float glossiness;public int[] averageColor;
  [NonSerialized] internal bool builtin,loading;[NonSerialized] internal int references;[NonSerialized] internal Fs20PaintCache.Group group;
  [NonSerialized] internal Texture2D bodyTexture,wingTexture;[NonSerialized] internal LiveryData data;
  internal string Guid=>Key(id);
  internal Color32 Tint=>averageColor!=null&&averageColor.Length==3?new Color32((byte)averageColor[0],(byte)averageColor[1],(byte)averageColor[2],255):new Color32(80,90,100,255);
  internal void Load()=>Fs20PaintCache.LoadNow(this);
  internal LiveryData Data {get{Load();if(data==null){data=ScriptableObject.CreateInstance<LiveryData>();data.name=Prefix+id;data.Texture=bodyTexture;data.Glossiness=glossiness;data.Colors=new[]{new LiveryData.TextureColor{Color=Tint,Count=1}};}return data;}}
 }
 sealed class Selection {internal Paint paint;}
 static readonly ConditionalWeakTable<Aircraft,Selection> selections=new ConditionalWeakTable<Aircraft,Selection>();
 static readonly Dictionary<string,Paint> paints=new Dictionary<string,Paint>(StringComparer.Ordinal);
 static bool initialized;static ResourceLocationMap map;static Texture2D wingBasis;static Fs20PaintCache.Group basis;
 internal static Texture2D WingBasis {get{Ensure();if(wingBasis==null)wingBasis=Fs20PaintCache.Basis(basis);return wingBasis;}}
 static string Key(string id){using(var md5=MD5.Create())return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes("FS20.PreparedPaint.v1/"+id)).Select(b=>b.ToString("x2")));}
 static string SafePath(string folder,string name){if(string.IsNullOrWhiteSpace(name)||Path.GetFileName(name)!=name||name.Contains(".."))throw new InvalidDataException("Invalid FS-20 bundle filename");return Path.Combine(folder,name);}
 static void Add(Pack pack,bool builtin,string directory,string customFile=null,string customHash=null){
  if(pack==null||pack.paints==null||pack.paints.Length==0||pack.schemaVersion!=(builtin?2:1))throw new InvalidDataException("Unsupported FS-20 paint catalog");
  if(!builtin&&paints.Values.Count(p=>!p.builtin)+pack.paints.Length>8)throw new InvalidDataException("At most eight custom FS-20 paints can be loaded");
  var pending=new HashSet<string>(StringComparer.Ordinal);
  foreach(var p in pack.paints)if(string.IsNullOrEmpty(p.id)||p.id.Length>100||p.id.Any(c=>!char.IsLetterOrDigit(c)&&c!='.'&&c!='-'&&c!='_')||(p.variant!="A"&&p.variant!="T"&&p.variant!="NA")||(p.faction!="BDF"&&p.faction!="PALA")||p.glossiness<0||p.glossiness>1||paints.ContainsKey(p.id)||!pending.Add(p.id))throw new InvalidDataException("Invalid/duplicate FS-20 paint entry");
  foreach(var p in pack.paints){p.builtin=builtin;string file=builtin?SafePath(directory,p.bundleFile):customFile;if(!File.Exists(file))throw new FileNotFoundException("FS-20 paint file missing",file);p.group=Fs20PaintCache.Register(file,builtin?p.bundleSha256:customHash,builtin&&p.scheme==0);p.group.paints.Add(p);paints.Add(p.id,p);}
 }
 internal static void Ensure(){
  if(initialized)return;
  string directory=Path.Combine(Path.GetDirectoryName(typeof(Fs20PreparedLiveries).Assembly.Location),"BuiltinPaints");
  if(Application.isEditor&&!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FS20_PAINT_DIRECTORY")))directory=Environment.GetEnvironmentVariable("FS20_PAINT_DIRECTORY");
  Pack pack;using(var stream=typeof(Fs20PreparedLiveries).Assembly.GetManifestResourceStream("Fs20PaintCatalog.json"))using(var reader=new StreamReader(stream??throw new InvalidDataException("FS-20 paint catalog missing")))pack=Newtonsoft.Json.JsonConvert.DeserializeObject<Pack>(reader.ReadToEnd());
  Add(pack,true,directory);basis=Fs20PaintCache.Register(SafePath(directory,pack.basisFile),pack.basisSha256,true);
  foreach(var path in Directory.GetFiles(Paths.PluginPath,"*.fs20paint",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Take(64)){
   AssetBundle custom=null;try{
    custom=AssetBundle.LoadFromFile(path);if(custom==null)throw new InvalidDataException("Invalid paint pack");var manifest=custom.LoadAsset<TextAsset>("fs20manifest");if(manifest==null)throw new InvalidDataException("Paint manifest missing");
    string hash;using(var file=File.OpenRead(path))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
    Add(Newtonsoft.Json.JsonConvert.DeserializeObject<Pack>(manifest.text),false,null,path,hash);
   }catch(Exception e){VortexConventionalPlugin.ReportAssetState("FS-20 paint pack skipped: "+Path.GetFileName(path)+": "+e.Message);}finally{if(custom!=null)custom.Unload(true);}
  }
  map=new ResourceLocationMap("FS-20 demand-loaded UV liveries",paints.Count);Addressables.ResourceManager.ResourceProviders.Add(new PaintProvider());
  foreach(var p in paints.Values)Location(p.id,p.id);
  string[] removed={"t.naval-hex","t.forest","na.naval-hex","na.night-legacy","na.forest","a.air-defence-digital","a.woodland"};
  string[] targets={"t.naval","t.naval","na.naval","na.night","na.naval","a.air-superiority","a.desert"};
  for(int i=0;i<removed.Length;i++)Location(removed[i],targets[i]);
  for(int i=0;i<7;i++){string key="fa206e6100000000000000000000000"+i;var p=FindScheme("NA",i);map.Add(key,new ResourceLocationBase(key,key,Provider,typeof(LiveryData)){Data=p.id});}
  Addressables.AddResourceLocator(map);initialized=true;var runner=Fs20PaintCache.Runner;
 }
 static void Location(string id,string target){string key=Key(id);map.Add(key,new ResourceLocationBase(key,key,Provider,typeof(LiveryData)){Data=target});}
 internal static bool TryPaint(string id,out Paint paint)=>paints.TryGetValue(id,out paint);
 internal static bool OwnsTexture(Texture texture){if(texture==null)return false;foreach(var p in paints.Values)if(p.bodyTexture==texture||p.wingTexture==texture)return true;return false;}
 static Paint FindScheme(string variant,int index){var p=paints.Values.FirstOrDefault(p=>p.builtin&&p.variant==variant&&p.scheme==index);return p??paints.Values.First(p=>p.builtin&&p.variant==variant&&p.scheme==(index==5?4:index==6&&variant=="A"?3:0));}
 internal static Paint Scheme(string variant,int index){Ensure();return FindScheme(variant,index);}
 internal static Paint Current(Aircraft aircraft,string variant,int index=0){Ensure();if(aircraft!=null&&selections.TryGetValue(aircraft,out var selection)&&selection.paint!=null)return selection.paint;return FindScheme(variant,index);}
 internal static void Select(Aircraft aircraft,Paint paint){if(aircraft!=null)selections.GetOrCreateValue(aircraft).paint=paint;}
 internal static Paint Resolve(string variant,LiveryData source,int fallback=0){
  Ensure();if(source!=null&&source.name.StartsWith(Prefix,StringComparison.Ordinal)&&paints.TryGetValue(source.name.Substring(Prefix.Length),out var own)&&own.variant==variant)return own;
  string name=source==null?"":source.name;if(name.StartsWith("FS20NA native ",StringComparison.Ordinal)&&Enum.TryParse(name.Substring("FS20NA native ".Length),out Fs20NANightScheme old))return FindScheme(variant,(int)old);
  int index=name.StartsWith("SmallFighter1_BDF_hex",StringComparison.OrdinalIgnoreCase)?1:name.StartsWith("SmallFighter1_PALA_desert",StringComparison.OrdinalIgnoreCase)?3:name.StartsWith("SmallFighter1_PALA_black",StringComparison.OrdinalIgnoreCase)?4:name.StartsWith("SmallFighter1_BDF_navy",StringComparison.OrdinalIgnoreCase)?0:fallback;return FindScheme(variant,index);
 }
 internal static void Configure(string variant,AircraftParameters parameters){
  Ensure();var original=parameters.liveries.ToList();var bdf=original.FirstOrDefault(p=>p.assetReference!=null&&p.assetReference.AssetGUID=="6f1aee58aecf7e4459bbb9bd5af2295e");var pala=original.FirstOrDefault(p=>p.assetReference!=null&&p.assetReference.AssetGUID=="a44bcb67795df70498f7e3e1b51afce5");if(bdf.assetReference==null||pala.assetReference==null)throw new InvalidDataException("Native factions unavailable for FS-20 paint registration");
  var order=variant=="NA"?new[]{0,1,2,6,3,4}:new[]{0,1,3,4,2,6};parameters.liveries=paints.Values.Where(p=>p.variant==variant).OrderBy(p=>p.builtin?Array.IndexOf(order,p.scheme):100).ThenBy(p=>p.id,StringComparer.Ordinal).Select(p=>new AircraftParameters.Livery{name=p.displayName+" - "+variant,faction=p.faction=="PALA"?pala.faction:bdf.faction,assetReference=new AssetReferenceLiveryData(p.Guid)}).ToList();
  // Only the three prefab fallback coats are prepared during definition loading.
  FindScheme(variant,0).Load();
 }
 internal static string Signature(){Ensure();return string.Join("\n",paints.Values.OrderBy(p=>p.id,StringComparer.Ordinal).Select(p=>p.id+":"+p.variant+":"+p.faction+":"+p.group.hash));}
 static void Lease(UnitPart part,LiveryData data){
  if(part==null||data==null||!data.name.StartsWith(Prefix,StringComparison.Ordinal)||!paints.TryGetValue(data.name.Substring(Prefix.Length),out var paint))return;
  foreach(var renderer in part.GetComponentsInChildren<MeshRenderer>(true)){
   bool lod=Fs20PaintLods.Painted(renderer);
   if(!lod&&!renderer.sharedMaterials.Any(m=>m!=null&&(m.name.Contains("SmallFighter1_skin")||m.name.Contains("FS20T delta paint"))))continue;
   if(lod)Fs20PaintLods.Apply(renderer,paint);
   ApplyExteriorAtlases(renderer,paint);
   var lease=renderer.GetComponent<Fs20PaintLease>()??renderer.gameObject.AddComponent<Fs20PaintLease>();lease.Bind(paint);
  }
 }
 // Native UnitPart.SetLivery works with the body's LiveryData even on
 // extension meshes. Bind the right atlas by material role after that write.
 internal static bool WingMaterial(Material material) =>
  material != null && material.name.IndexOf("FS20T delta paint", StringComparison.Ordinal) >= 0;
 internal static bool BodyMaterial(Material material) =>
  material != null && material.name.IndexOf("SmallFighter1_skin", StringComparison.Ordinal) >= 0;
 static void ApplyExteriorAtlases(MeshRenderer renderer, Paint paint)
 {
  if (paint.variant == "A") return;
  bool na = paint.variant == "NA";
  if (na)
   for (var node = renderer.transform; node != null; node = node.parent)
    if (node.name == "nozzle" || node.name.StartsWith("nozzle_", StringComparison.Ordinal) ||
        node.name.StartsWith("swivelDuct", StringComparison.Ordinal)) return;
  var materials = renderer.sharedMaterials;
  MaterialPropertyBlock block = null;
  for (int slot = 0; slot < materials.Length; slot++)
  {
   var material = materials[slot];
   bool wing = WingMaterial(material);
   if (!wing && !(na && BodyMaterial(material))) continue;
   var atlas = wing ? paint.wingTexture : paint.bodyTexture;
   if (block == null) block = new MaterialPropertyBlock();
   renderer.GetPropertyBlock(block, slot);
   block.SetTexture("_Livery", atlas);
   if (na)
   {
    block.SetTexture("_Basecolor", atlas);
    block.SetTexture("_BasecolorDmg", atlas);
   }
   renderer.SetPropertyBlock(block, slot);
   block.Clear();
  }
 }
 [HarmonyPatch(typeof(UnitPart),"SetLivery")]
 internal static class Lifetime {[HarmonyPostfix,HarmonyPriority(Priority.Last)] static void Postfix(UnitPart __instance,LiveryData __0)=>Lease(__instance,__0);}
 sealed class PaintProvider:ResourceProviderBase {
  public override string ProviderId=>Provider;public override Type GetDefaultType(IResourceLocation location)=>typeof(LiveryData);
  public override void Provide(ProvideHandle handle){var operation=new Operation(handle,paints[(string)handle.Location.Data]);handle.SetWaitForCompletionCallback(operation.Wait);Fs20PaintCache.Runner.StartCoroutine(operation.Run());}
  public override void Release(IResourceLocation location,object asset){if(asset is LiveryData)Fs20PaintCache.Release(paints[(string)location.Data]);}
  sealed class Operation {
   readonly ProvideHandle handle;readonly Paint paint;bool completed;
   internal Operation(ProvideHandle handle,Paint paint){this.handle=handle;this.paint=paint;Fs20PaintCache.Acquire(paint);}
   internal bool Wait(){if(completed)return true;try{paint.Load();Finish(null);}catch(Exception e){Finish(e);}return true;}
   void Finish(Exception error){if(completed)return;completed=true;if(error==null)handle.Complete(paint.Data,true,(Exception)null);else{Fs20PaintCache.Release(paint);handle.Complete<LiveryData>(null,false,error);}}
   internal IEnumerator Run(){var load=Fs20PaintCache.LoadAsync(paint);while(!completed){object current=null;bool next=false;Exception error=null;try{next=load.MoveNext();if(next)current=load.Current;}catch(Exception e){error=e;}if(error!=null){Finish(error);break;}if(!next){Finish(null);break;}yield return current;}var disposable=load as IDisposable;if(disposable!=null)disposable.Dispose();}
  }
 }
}
}
