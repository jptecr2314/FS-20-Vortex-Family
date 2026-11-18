using System;
using System.IO;
using UnityEngine;
using Japensou.NuclearOption.VortexConventional;
public static class BakeFs20PreparedStencilCleanup {
 public static void Run(){
  string root=Environment.GetEnvironmentVariable("FS20_PAINT_INPUT");
  var pack=JsonUtility.FromJson<BuildFs20PreparedPaints.Pack>(File.ReadAllText(Path.Combine(root,"manifest.json")));int changed=0;
  foreach(var paint in pack.paints){if(paint.variant!="A")continue;
   string path=Path.Combine(root,paint.body);var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
   try {if(!ImageConversion.LoadImage(tex,File.ReadAllBytes(path)))throw new Exception("Invalid A paint");
    var pixels=tex.GetPixels32();var bytes=new byte[pixels.Length*4];for(int i=0;i<pixels.Length;i++){bytes[i*4]=pixels[i].r;bytes[i*4+1]=pixels[i].g;bytes[i*4+2]=pixels[i].b;bytes[i*4+3]=pixels[i].a;}
    var cleaned=Fs20PaintPixels.Clean(bytes,tex.width,tex.height);for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(cleaned[i*4],cleaned[i*4+1],cleaned[i*4+2],cleaned[i*4+3]);tex.SetPixels32(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());changed++;
   } finally {UnityEngine.Object.DestroyImmediate(tex);}
  }
  File.WriteAllText(Path.Combine(root,"offline-cleanup.json"),"{\"preparedA\":"+changed+",\"otherPaintsChanged\":false}");Debug.Log("FS20_OFFLINE_STENCIL_PASS "+changed);
 }
}
