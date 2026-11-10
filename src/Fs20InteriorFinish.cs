using System;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20InteriorFinish
    {
        internal const float TileMeters = .50f;
        internal const float NativeTileWidthMeters = 1.2f, NativeTileHeightMeters = .9f;
        // A rectangular structural-liner region of the licensed native atlas,
        // away from its turbine, exposed pipes and transparent cutouts.
        internal static readonly Rect NativeRegion = new Rect(32f / 2048, 896f / 2048, 512f / 2048, 384f / 2048);
        private static Material lining;

        internal static Vector2 Coordinates(Vector3 point, Vector3 normal)
        {
            int axis = Mathf.Abs(normal.x) > Mathf.Abs(normal.y)
                ? (Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? 0 : 2)
                : (Mathf.Abs(normal.y) > Mathf.Abs(normal.z) ? 1 : 2);
            return new Vector2(axis == 0 ? point.z : point.x, axis == 1 ? point.z : point.y) / TileMeters;
        }

        internal static Material Material(Transform root)
        {
            if (lining != null) return lining;
            var donor = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .First(m => m != null && m.name.StartsWith("jet_mechanical", StringComparison.Ordinal));
            var result = new Material(donor) { name = "FS20 native mechanical bay lining" };
            try
            {
                foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                {
                    var source = donor.HasProperty(property) ? donor.GetTexture(property) as Texture2D : null;
                    if (source == null)
                    {
                        if (property == "_BaseMap") throw new InvalidOperationException("Native bay liner albedo unavailable.");
                        continue;
                    }
                    var cropped = Crop(source, property != "_BaseMap");
                    result.SetTexture(property, cropped);
                    result.SetTextureScale(property, new Vector2(TileMeters / NativeTileWidthMeters, TileMeters / NativeTileHeightMeters));
                    result.SetTextureOffset(property, Vector2.zero);
                    if (property == "_BaseMap" && result.HasProperty("_MainTex"))
                    {
                        result.SetTexture("_MainTex", cropped);
                        result.SetTextureScale("_MainTex", result.GetTextureScale(property));
                        result.SetTextureOffset("_MainTex", Vector2.zero);
                    }
                }
                foreach (string property in new[] { "_Color", "_BaseColor" })
                    if (result.HasProperty(property)) result.SetColor(property, Color.white);
                if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", Color.black);
                result.DisableKeyword("_EMISSION");result.DisableKeyword("_ALPHATEST_ON");
                if (result.HasProperty("_AlphaClip")) result.SetFloat("_AlphaClip", 0);
                if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", .35f);
                if (result.HasProperty("_OcclusionStrength")) result.SetFloat("_OcclusionStrength", .65f);
                result.renderQueue = 2000;
                lining = result;
                VortexConventionalPlugin.ReportAssetState("FS20 liner: native jet_mechanical albedo/normal/metal/AO region; continuous metric UVs; no generated checker pattern.");
                return lining;
            }
            catch { UnityEngine.Object.Destroy(result);throw; }
        }

        private static Texture2D Crop(Texture2D source, bool linear)
        {
            RenderTexture buffer = null;var previous = RenderTexture.active;bool srgb = GL.sRGBWrite;
            Texture2D result = null;
            try
            {
                buffer = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                    linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = !linear && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, buffer);RenderTexture.active = buffer;
                int x = Mathf.RoundToInt(source.width * NativeRegion.x), y = Mathf.RoundToInt(source.height * NativeRegion.y);
                int width = Mathf.RoundToInt(source.width * NativeRegion.width), height = Mathf.RoundToInt(source.height * NativeRegion.height);
                result = new Texture2D(width, height, TextureFormat.RGBA32, true, linear)
                    { name = "FS20 native liner " + source.name, wrapMode = TextureWrapMode.Repeat,
                      filterMode = FilterMode.Trilinear, anisoLevel = 8 };
                result.ReadPixels(new Rect(x, y, width, height), 0, 0, false);result.Apply(true, true);return result;
            }
            catch { if (result != null) UnityEngine.Object.Destroy(result);throw; }
            finally
            {
                RenderTexture.active = previous;GL.sRGBWrite = srgb;
                if (buffer != null) RenderTexture.ReleaseTemporary(buffer);
            }
        }
    }
}
