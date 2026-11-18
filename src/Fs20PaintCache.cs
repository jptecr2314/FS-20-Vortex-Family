using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional {
 // Bundle ownership follows native livery handles and renderer lifetimes.
 // No frame-time texture conversion, global unused-asset sweep or forced GC.
 internal static class Fs20PaintCache {
  internal sealed class Group {
   internal string path,hash;internal AssetBundle bundle;internal AssetBundleCreateRequest request;
   internal Task<bool> validation;internal bool validated,pinned;internal float idleSince;
   internal readonly List<Fs20PreparedLiveries.Paint> paints=new List<Fs20PreparedLiveries.Paint>();
   internal readonly List<AssetBundleRequest> pendingAssets=new List<AssetBundleRequest>();
   internal bool Used=>pinned||paints.Any(p=>p.references>0||p.loading)||pendingAssets.Any(p=>!p.isDone);
   internal bool Loaded=>paints.Any(p=>p.bodyTexture!=null)||bundle!=null;
  }
  static readonly List<Group> groups=new List<Group>();static readonly List<Fs20PaintLease> leases=new List<Fs20PaintLease>();static Driver driver;static AssetBundleRequest upload;
  internal static void Track(Fs20PaintLease lease){leases.Add(lease);}
  internal static void Forget(Fs20PaintLease lease){leases.Remove(lease);}
  internal static int SyncColdLoads,AsyncColdLoads,Evictions;internal static float IdleSeconds=15f;
  internal static Driver Runner {get{if(driver==null){var go=new GameObject("FS20 paint cache"){hideFlags=HideFlags.HideAndDontSave};if(Application.isPlaying)UnityEngine.Object.DontDestroyOnLoad(go);driver=go.AddComponent<Driver>();}return driver;}}
  internal sealed class Driver:MonoBehaviour {float next;void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;Collect(false);}}
  internal static Group Register(string path,string hash,bool pinned=false){var group=groups.FirstOrDefault(g=>g.path==path);if(group!=null)return group;group=new Group{path=path,hash=hash,pinned=pinned,idleSince=Time.realtimeSinceStartup};groups.Add(group);return group;}
  static bool Validate(Group group){if(!File.Exists(group.path))throw new FileNotFoundException("FS-20 paint bundle missing",group.path);if(!string.IsNullOrEmpty(group.hash)){using(var file=File.OpenRead(group.path))using(var sha=SHA256.Create()){string actual=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();if(!string.Equals(actual,group.hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("FS-20 paint bundle hash mismatch: "+Path.GetFileName(group.path));}}return true;}
  static void ContainerNow(Group group){if(!group.validated){if(group.validation!=null){group.validation.GetAwaiter().GetResult();}else Validate(group);group.validated=true;}if(group.bundle==null){group.bundle=group.request!=null?group.request.assetBundle:AssetBundle.LoadFromFile(group.path);group.request=null;if(group.bundle==null)throw new InvalidDataException("Invalid FS-20 paint bundle: "+Path.GetFileName(group.path));}}
  internal static Texture2D Basis(Group group){ContainerNow(group);var basis=group.bundle.LoadAsset<Texture2D>("fs20wingbasis");if(basis==null)throw new InvalidDataException("FS-20 shading basis missing");return basis;}
  internal static void Acquire(Fs20PreparedLiveries.Paint p){p.references++;p.group.idleSince=Time.realtimeSinceStartup;}
  internal static void Release(Fs20PreparedLiveries.Paint p){if(p.references<=0)throw new InvalidOperationException("FS-20 paint reference underflow");p.references--;p.group.idleSince=Time.realtimeSinceStartup;}
  internal static void LoadNow(Fs20PreparedLiveries.Paint p){if(p.bodyTexture!=null&&p.wingTexture!=null)return;SyncColdLoads++;ContainerNow(p.group);p.bodyTexture=p.group.bundle.LoadAsset<Texture2D>(p.body);p.wingTexture=p.variant=="A"?p.bodyTexture:p.group.bundle.LoadAsset<Texture2D>(p.wing);Verify(p);}
  static void Verify(Fs20PreparedLiveries.Paint p){if(p.bodyTexture==null||p.wingTexture==null)throw new InvalidDataException("Missing FS-20 paint textures: "+p.id);if(p.builtin&&(p.bodyTexture.width!=2048||p.wingTexture.width!=2048||p.bodyTexture.format!=TextureFormat.BC7||p.wingTexture.format!=TextureFormat.BC7||p.bodyTexture.isReadable||p.wingTexture.isReadable))throw new InvalidDataException("Invalid prepared FS-20 texture format: "+p.id);}
  internal static IEnumerator LoadAsync(Fs20PreparedLiveries.Paint p){
   if(p.bodyTexture!=null&&p.wingTexture!=null)yield break;
   if(p.loading){while(p.loading)yield return null;if(p.bodyTexture==null)throw new InvalidDataException("Concurrent FS-20 paint load failed: "+p.id);yield break;}
   p.loading=true;AsyncColdLoads++;
   try{
    var group=p.group;
    if(!group.validated){if(group.validation==null)group.validation=Task.Run(()=>Validate(group));while(!group.validation.IsCompleted)yield return null;group.validation.GetAwaiter().GetResult();group.validated=true;}
    if(group.bundle==null){if(group.request==null)group.request=AssetBundle.LoadFromFileAsync(group.path);var pending=group.request;yield return pending;if(group.bundle==null)group.bundle=pending.assetBundle;group.request=null;if(group.bundle==null)throw new InvalidDataException("Invalid FS-20 bundle: "+p.id);}
    while(upload!=null&&!upload.isDone)yield return null;
    var body=group.bundle.LoadAssetAsync<Texture2D>(p.body);body.priority=-10;upload=body;group.pendingAssets.Add(body);yield return body;p.bodyTexture=body.asset as Texture2D;group.pendingAssets.Remove(body);
    if(p.variant=="A")p.wingTexture=p.bodyTexture;
    else {while(upload!=null&&!upload.isDone)yield return null;var wing=group.bundle.LoadAssetAsync<Texture2D>(p.wing);wing.priority=-10;upload=wing;group.pendingAssets.Add(wing);yield return wing;p.wingTexture=wing.asset as Texture2D;group.pendingAssets.Remove(wing);}
    Verify(p);
   }finally{p.loading=false;p.group.idleSince=Time.realtimeSinceStartup;}
  }
  internal static void Collect(bool force){
   // Unity omits OnDestroy for objects that have never been active. Damage
   // states include such renderers; their managed lease must still be released.
   for(int i=leases.Count-1;i>=0;i--)if(leases[i]==null){leases[i].ReleaseHeld();leases.RemoveAt(i);}
   foreach(var group in groups)group.pendingAssets.RemoveAll(p=>p.isDone);
   var idle=groups.Where(g=>!g.Used&&g.Loaded&&g.request==null).OrderByDescending(g=>g.idleSince).ToArray();
   for(int i=0;i<idle.Length;i++){
    var group=idle[i];if(!force&&(i==0||Time.realtimeSinceStartup-group.idleSince<IdleSeconds))continue;
    if(group.bundle!=null)group.bundle.Unload(true);group.bundle=null;
    foreach(var p in group.paints){Fs20PaintLods.Release(p);if(p.data!=null)Destroy(p.data);if(p.bodyTexture!=null)Destroy(p.bodyTexture);if(p.wingTexture!=null&&p.wingTexture!=p.bodyTexture)Destroy(p.wingTexture);p.data=null;p.bodyTexture=null;p.wingTexture=null;}
    Evictions++;
   }
  }
  static void Destroy(UnityEngine.Object item){if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
  internal static object Snapshot()=>new{registered=groups.Sum(g=>g.paints.Count),loaded=groups.Sum(g=>g.paints.Count(p=>p.bodyTexture!=null)),references=groups.Sum(g=>g.paints.Sum(p=>p.references)),idle=groups.Count(g=>!g.Used&&g.Loaded),loading=groups.Sum(g=>g.paints.Count(p=>p.loading)),syncColdLoads=SyncColdLoads,asyncColdLoads=AsyncColdLoads,evictions=Evictions,textureBytes=groups.SelectMany(g=>g.paints).SelectMany(p=>new[]{p.bodyTexture,p.wingTexture}).Where(t=>t!=null).Distinct().Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t))};
 }

 // Intentionally no OnDisable: LOD switching and damage-state hiding must not
 // release textures that a live renderer or detached part can still reference.
 public sealed class Fs20PaintLease:MonoBehaviour {
  [SerializeField] string paintId;[NonSerialized] Fs20PreparedLiveries.Paint held;
  internal void Bind(Fs20PreparedLiveries.Paint paint){if(held==paint)return;if(held!=null)Fs20PaintCache.Release(held);else Fs20PaintCache.Track(this);paintId=paint.id;held=paint;Fs20PaintCache.Acquire(held);}
  void OnEnable(){if(held==null&&!string.IsNullOrEmpty(paintId)&&Fs20PreparedLiveries.TryPaint(paintId,out var p))Bind(p);}
  internal void ReleaseHeld(){if(held!=null){Fs20PaintCache.Release(held);held=null;}}
  void OnDestroy(){ReleaseHeld();Fs20PaintCache.Forget(this);}
 }
}
