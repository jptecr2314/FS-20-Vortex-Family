using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
public static class BuildFs20DemandPaints {
 [Serializable] public class Catalog {public int schemaVersion=2;public string packageId,version,author,basisFile,basisSha256;public Entry[] paints;}
 [Serializable] public class Entry {public string id,variant,displayName,faction,body,wing,bundleFile,bundleSha256;public int scheme;public float glossiness;public int[] averageColor;}
 static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 public static void Build(){
  string input=Environment.GetEnvironmentVariable("FS20_PAINT_INPUT"),output=Environment.GetEnvironmentVariable("FS20_PAINT_OUTPUT");Directory.CreateDirectory(output);
  var old=JsonUtility.FromJson<BuildFs20PreparedPaints.Pack>(File.ReadAllText(Path.Combine(input,"manifest.json")));
  var catalog=new Catalog{packageId=old.packageId,version="0.7.0",author=old.author,basisFile="fs20-basis.bundle"};var entries=new List<Entry>();var builds=new List<AssetBundleBuild>();
  foreach(var p in old.paints){
   var names=new List<string>();var aliases=new List<string>();
   foreach(string role in p.variant=="A"?new[]{"body"}:new[]{"body","wing"}){
    string path="Assets/FS20OwnPaints/"+p.id+"."+role+".png";var ti=AssetImporter.GetAtPath(path) as TextureImporter;
    if(ti==null||ti.isReadable||ti.GetPlatformTextureSettings("Standalone").format!=TextureImporterFormat.BC7)throw new Exception("Unprepared texture "+path);
    var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(tex.width!=2048||tex.height!=2048)throw new Exception("Expected 2K "+path);
    names.Add(path);aliases.Add(p.id+"."+role);
   }
   var e=new Entry{id=p.id,variant=p.variant,displayName=p.displayName,faction=p.faction,scheme=p.scheme,glossiness=p.glossiness,averageColor=p.averageColor,body=p.id+".body",wing=p.variant=="A"?null:p.id+".wing",bundleFile="fs20-"+p.id+".bundle"};entries.Add(e);
   builds.Add(new AssetBundleBuild{assetBundleName=e.bundleFile,assetNames=names.ToArray(),addressableNames=aliases.ToArray()});
  }
  builds.Add(new AssetBundleBuild{assetBundleName=catalog.basisFile,assetNames=new[]{"Assets/FS20OwnPaints/fs20wingbasis.png"},addressableNames=new[]{"fs20wingbasis"}});
  if(BuildPipeline.BuildAssetBundles(output,builds.ToArray(),BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64)==null)throw new Exception("Demand bundle build failed");
  foreach(var e in entries)e.bundleSha256=Hash(Path.Combine(output,e.bundleFile));catalog.basisSha256=Hash(Path.Combine(output,catalog.basisFile));catalog.paints=entries.ToArray();File.WriteAllText(Path.Combine(output,"catalog.json"),JsonUtility.ToJson(catalog,true));Debug.Log("FS20_DEMAND_PAINT_BUILD_PASS "+entries.Count);
 }
}
