using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class BuildFs20PreparedPaints {
 [Serializable] public class Pack {public int schemaVersion;public string packageId,version,author;public Paint[] paints;}
 [Serializable] public class Paint {public string id,variant,displayName,faction,body,wing;public int scheme;public float glossiness;public int[] averageColor;}
 public static void Build(){
  var input=Environment.GetEnvironmentVariable("FS20_PAINT_INPUT");var output=Environment.GetEnvironmentVariable("FS20_PAINT_OUTPUT");
  int wingSize=4096;var requested=Environment.GetEnvironmentVariable("FS20_PAINT_WING_SIZE");
  if(!string.IsNullOrEmpty(requested)&&(!int.TryParse(requested,out wingSize)||(wingSize!=2048&&wingSize!=4096)))throw new Exception("Wing output must be 2048 or 4096");
  var pack=JsonUtility.FromJson<Pack>(File.ReadAllText(Path.Combine(input,"manifest.json")));
  if(pack.schemaVersion!=1||pack.paints==null)throw new Exception("Invalid manifest");
  var root="Assets/FS20OwnPaints";Directory.CreateDirectory(root);var names=new List<string>();var aliases=new List<string>();var ids=new HashSet<string>();
  foreach(var p in pack.paints){
   if(string.IsNullOrEmpty(p.id)||!ids.Add(p.id)||!(p.variant=="T"||p.variant=="NA"||p.variant=="A")||!(p.faction=="BDF"||p.faction=="PALA")||p.glossiness<0||p.glossiness>1||p.averageColor==null||p.averageColor.Length!=3)throw new Exception("Invalid paint definition or duplicate ID");
   foreach(var role in (p.variant=="A"?new[]{"body"}:new[]{"body","wing"})){
    var source=Path.Combine(input,role=="body"?p.body:p.wing);var path=root+"/"+p.id+"."+role+".png";
    var header=new byte[24];using(var stream=File.OpenRead(source))if(stream.Read(header,0,24)!=24)throw new Exception("Invalid PNG: "+source);
    int width=(header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19],height=(header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23],expected=role=="body"?2048:4096;
    if(header[0]!=137||header[1]!=80||header[2]!=78||header[3]!=71||width!=expected||height!=expected)throw new Exception(role+" must be an opaque sRGB "+expected+" x "+expected+" PNG");
    File.Copy(source,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;importer.alphaSource=TextureImporterAlphaSource.None;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.maxTextureSize=role=="body"?2048:wingSize;
    importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=importer.maxTextureSize,format=TextureImporterFormat.BC7,compressionQuality=100,crunchedCompression=false});importer.SaveAndReimport();
    var alias=p.id+"."+role;names.Add(path);aliases.Add(alias);if(role=="body")p.body=alias;else p.wing=alias;
   }
  }
  var basis=Path.Combine(input,"wing-basis.png");if(File.Exists(basis)){
   var path=root+"/fs20wingbasis.png";File.Copy(basis,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.sRGBTexture=true;ti.mipmapEnabled=true;ti.isReadable=false;ti.wrapMode=TextureWrapMode.Clamp;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=8;ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=wingSize,format=TextureImporterFormat.BC7,compressionQuality=100});ti.SaveAndReimport();names.Add(path);aliases.Add("fs20wingbasis");
  }
  var manifest=root+"/fs20manifest.json";File.WriteAllText(manifest,JsonUtility.ToJson(pack,true));AssetDatabase.ImportAsset(manifest,ImportAssetOptions.ForceSynchronousImport);names.Add(manifest);aliases.Add("fs20manifest");Directory.CreateDirectory(output);
  var bundleName=Environment.GetEnvironmentVariable("FS20_PAINT_BUNDLE")??"Fs20PreparedPaints.bundle";
  var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName=bundleName,assetNames=names.ToArray(),addressableNames=aliases.ToArray()}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
  if(result==null)throw new Exception("Paint bundle build failed");Debug.Log("FS20_PREPARED_PAINTS_OK "+pack.paints.Length);
 }
}
