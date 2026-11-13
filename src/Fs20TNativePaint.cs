using System;
using System.IO;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Transfer the native paint field, preserving its RGB/alpha and physical scale.
    // The reference UV map is generated from retained skin, not camouflage noise.
    internal static class Fs20TNativePaint
    {
        private static Color32[] lookup, baseline;
        private static int lookupWidth, lookupHeight, baseWidth, baseHeight;
        private static Material donor;
        private static Texture2D metallic;
        internal static void Configure(Material native)
        {
            if (donor != null) return;
            donor = native;
            baseline = Read((Texture2D)native.GetTexture("_Basecolor"), false, out baseWidth, out baseHeight);
        }
        private static void LoadLookup()
        {
            if (lookup != null) return;
            using (var stream = typeof(Fs20TNativePaint).Assembly.GetManifestResourceStream("Fs20TNativePaintUv.png"))
            using (var bytes = new MemoryStream())
            {
                if (stream == null) throw new InvalidDataException("Missing native FS-20T paint projection.");
                stream.CopyTo(bytes);
                var image = new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                try
                {
                    if (!ImageConversion.LoadImage(image,bytes.ToArray())) throw new InvalidDataException("Invalid native paint projection.");
                    lookupWidth=image.width; lookupHeight=image.height; lookup=image.GetPixels32();
                }
                finally { Release(image); }
            }
        }
        private static Color32[] Read(Texture2D source, bool linear, out int width, out int height)
        {
            if (source == null) throw new InvalidDataException("Native paint source missing.");
            width=source.width; height=source.height;
            var buffer=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,linear?RenderTextureReadWrite.Linear:RenderTextureReadWrite.sRGB);
            var old=RenderTexture.active; bool oldSrgb=GL.sRGBWrite;
            var image=new Texture2D(width,height,TextureFormat.RGBA32,false,linear);
            try
            {
                GL.sRGBWrite=!linear && QualitySettings.activeColorSpace==ColorSpace.Linear;
                Graphics.Blit(source,buffer); RenderTexture.active=buffer;
                image.ReadPixels(new Rect(0,0,width,height),0,0,false);
                return image.GetPixels32();
            }
            finally { RenderTexture.active=old; GL.sRGBWrite=oldSrgb; RenderTexture.ReleaseTemporary(buffer); Release(image); }
        }
        private static int Index(Color32 uv,int width,int height) =>
            Math.Min(height-1,((uv.b|uv.a<<8)*height)>>16)*width+Math.Min(width-1,((uv.r|uv.g<<8)*width)>>16);

        internal static Texture2D Transfer(Texture2D source, byte[] shade, int size, bool paint)
        {
            LoadLookup();
            var pixels=Read(source,!paint,out int width,out int height);
            var output=new Color32[size*size];
            for(int y=0;y<size;y++)
                for(int x=0;x<size;x++)
                {
                    int i=y*size+x;
                    var uv=lookup[(y*lookupHeight/size)*lookupWidth+x*lookupWidth/size];
                    var color=pixels[Index(uv,width,height)];
                    if(paint)
                    {
                        var basis=baseline[Index(uv,baseWidth,baseHeight)];
                        float tone=103f+shade[i]-128f;
                        color=new Color32(Channel(color.r,basis.r,tone),Channel(color.g,basis.g,tone),Channel(color.b,basis.b,tone),color.a);
                    }
                    output[i]=color;
                }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,!paint) {
                name=paint?"FS20T native livery transfer":"FS20T native surface response",
                wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=8
            };
            texture.SetPixels32(output); texture.Apply(true,false);
            if(SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)) texture.Compress(true);
            texture.Apply(true,true); return texture;
        }
        private static byte Channel(byte paint,byte basis,float tone) => (byte)Mathf.Clamp(Mathf.RoundToInt(paint*tone/Mathf.Max(24f,basis)),0,255);
        internal static Material Material(Texture2D neutral,Texture2D normal,byte[] shade,int size)
        {
            if(metallic==null) metallic=Transfer((Texture2D)donor.GetTexture("_Metallic"),shade,size,false);
            var result=new Material(donor){name="FS20T delta paint"};
            result.SetTexture("_Basecolor",neutral); result.SetTexture("_Livery",neutral);
            result.SetTexture("_Normal",normal); result.SetTexture("_NormalDmg",normal);
            result.SetTexture("_BasecolorDmg",neutral); result.SetTexture("_Metallic",metallic);
            result.SetTexture("_AO",Texture2D.whiteTexture);
            return result;
        }
        private static void Release(UnityEngine.Object obj)
        { if(Application.isPlaying) UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj); }
    }
}
