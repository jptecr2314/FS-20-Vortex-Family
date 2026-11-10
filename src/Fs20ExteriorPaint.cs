using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20ExteriorPaint : MonoBehaviour
    {
        private const string Prefix = "FS20 CTOL paint ";
        private static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        private static readonly string[] Maps = { "_Basecolor", "_Livery" };
        private int pendingFrames;

        internal static void Install(Transform root)
        {
            var paint = root.gameObject.AddComponent<Fs20ExteriorPaint>();
            paint.Refresh();
        }
        private void Start() { Refresh(); QueueRefresh(); }
        private void QueueRefresh() => pendingFrames = 2;
        private void LateUpdate()
        {
            if (pendingFrames <= 0) return;
            pendingFrames--;Refresh();
        }

        private void Refresh()
        {
            // A bad or unavailable optional paint map must not abort aircraft
            // registration. Keep the native material if its readback fails.
            try
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;bool changed = false;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = slots[i];
                        if (source == null || !source.name.Contains("SmallFighter1_skin")) continue;
                        Material target;
                        if (source.name.StartsWith(Prefix, StringComparison.Ordinal)) target = source;
                        else if (!materials.TryGetValue(source.GetInstanceID(), out target) || target == null)
                        {
                            target = new Material(source) { name = Prefix + source.name };
                            materials[source.GetInstanceID()] = target;
                        }
                        else target.CopyPropertiesFromMaterial(source);
                        foreach (string map in Maps)
                        {
                            if (!target.HasProperty(map)) continue;
                            var texture = target.GetTexture(map) as Texture2D;
                            if (texture != null) target.SetTexture(map, Clean(texture));
                        }
                        slots[i] = target;changed = true;
                    }
                    if (changed) renderer.sharedMaterials = slots;
                }
            }
            catch (Exception error) { Debug.LogWarning("FS-20 conventional paint retained native fallback: " + error.Message); }
        }

        private static Texture2D Clean(Texture2D source)
        {
            if (source.width < 64 || source.height < 64 || source.name.StartsWith(Prefix, StringComparison.Ordinal)) return source;
            int key = source.GetInstanceID();
            Texture2D result = null;
            if (textures.TryGetValue(key, out result) && result != null) return result;
            RenderTexture buffer = null;var previous = RenderTexture.active;bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                buffer = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, buffer);RenderTexture.active = buffer;
                result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, false)
                    { name = Prefix + source.name, wrapMode = source.wrapMode, filterMode = source.filterMode,
                      anisoLevel = source.anisoLevel, mipMapBias = source.mipMapBias };
                result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                var colors = result.GetPixels32();var bytes = new byte[colors.Length * 4];
                for (int i = 0; i < colors.Length; i++)
                { bytes[i*4]=colors[i].r;bytes[i*4+1]=colors[i].g;bytes[i*4+2]=colors[i].b;bytes[i*4+3]=colors[i].a; }
                var cleaned = Fs20PaintPixels.Clean(bytes, source.width, source.height);int changed = 0;
                for (int i = 0; i < colors.Length; i++)
                    if (bytes[i*4] != cleaned[i*4] || bytes[i*4+1] != cleaned[i*4+1] || bytes[i*4+2] != cleaned[i*4+2]) changed++;
                bytes = cleaned;
                for (int i = 0; i < colors.Length; i++) colors[i]=new Color32(bytes[i*4],bytes[i*4+1],bytes[i*4+2],bytes[i*4+3]);
                result.SetPixels32(colors);result.Apply(true, true);textures[key] = result;
                VortexConventionalPlugin.ReportAssetState("FS20 CTOL stencil atlas: " + source.name + " " + source.width + "x" + source.height + ", changed pixels=" + changed + ".");
                return result;
            }
            catch
            {
                if (result != null) Destroy(result);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgbWrite;
                if (buffer != null) RenderTexture.ReleaseTemporary(buffer);
            }
        }

        [HarmonyPatch(typeof(LiveryBehaviour), "SetLivery")]
        internal static class LiveryChanged
        {
            private static void Postfix(Aircraft ___aircraft)
            {
                var paint = ___aircraft == null ? null : ___aircraft.GetComponent<Fs20ExteriorPaint>();
                if (paint != null) { paint.Refresh(); paint.QueueRefresh(); }
            }
        }

        // Parts can receive their livery during initialization after the root
        // Start, or from another mod. Finish once after those native writes.
        [HarmonyPatch(typeof(UnitPart), "SetLivery")]
        internal static class PartLiveryChanged
        {
            private static void Postfix(UnitPart __instance)
            {
                var owner = __instance.parentUnit;
                var paint = owner == null ? null : owner.GetComponent<Fs20ExteriorPaint>();
                if (paint != null) paint.QueueRefresh();
            }
        }
    }
}
