using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20TDeltaPaint : MonoBehaviour
    {
        internal const string MaterialKey = "__deltaSkin";
        private const int Size = 4096;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private static byte[] shading;
        private static Texture2D normalAtlas;
        [SerializeField] private Renderer[] panels;
        [SerializeField] private float glossiness;

        // Keep the native shader's neutral basis independent from the chosen coat.
        internal static Texture2D NeutralAtlas => Fs20PreparedLiveries.WingBasis;
        internal static Material NativeMaterial(Material native)
        {
            LoadMask();
            Fs20TNativePaint.Configure(native);
            var neutral=NeutralAtlas;
            return Fs20TNativePaint.Material(neutral,NormalAtlas,shading,Size);
        }
        internal static Texture2D NormalAtlas
        {
            get
            {
                if (normalAtlas != null) return normalAtlas;
                using (var stream = typeof(Fs20TDeltaPaint).Assembly.GetManifestResourceStream("Fs20TFinishNormal.png"))
                using (var bytes = new MemoryStream())
                {
                    if (stream == null) throw new InvalidDataException("Missing FS-20T normal atlas.");
                    stream.CopyTo(bytes);
                    var texture = new Texture2D(2, 2, TextureFormat.RGB24, true, true);
                    if (!ImageConversion.LoadImage(texture, bytes.ToArray()) || texture.width != Size || texture.height != Size)
                        throw new InvalidDataException("Invalid FS-20T normal atlas.");
                    texture.name = "FS20T panel normals"; texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 8;
                    texture.Apply(true, true);
                    return normalAtlas = texture;
                }
            }
        }
        internal void Bind(Renderer[] renderers) => panels = renderers;

        private static void LoadMask()
        {
            if (shading != null) return;
            using (var stream = typeof(Fs20TDeltaPaint).Assembly.GetManifestResourceStream("Fs20TDetail.png"))
            using (var bytes = new MemoryStream())
            {
                if (stream == null) throw new InvalidDataException("Missing FS-20T paint mask.");
                stream.CopyTo(bytes);
                var mask = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
                try
                {
                    if (!ImageConversion.LoadImage(mask, bytes.ToArray()) || mask.width != Size || mask.height != Size)
                        throw new InvalidDataException("Invalid FS-20T paint mask.");
                    var colors = mask.GetPixels32();
                    shading = new byte[colors.Length];
                    for (int i = 0; i < colors.Length; i++)
                    {
                        shading[i] = colors[i].g;
                    }
                }
                finally { if (Application.isPlaying) Destroy(mask); else DestroyImmediate(mask); }
            }
        }

        private void ApplyPrepared(Texture2D atlas)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var panel in panels)
            {
                if (panel == null) continue;
                var materials = panel.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    if (materials[slot] == null || !materials[slot].name.StartsWith("FS20T delta paint", StringComparison.Ordinal)) continue;
                    panel.GetPropertyBlock(properties, slot);
                    properties.SetTexture(BaseMap, atlas);
                    properties.SetTexture("_Livery", atlas);
                    properties.SetFloat("_Glossiness",glossiness);
                    properties.SetTexture("_BumpMap", NormalAtlas);
                    panel.SetPropertyBlock(properties, slot);
                }
            }
        }

        [HarmonyPatch(typeof(LiveryBehaviour), "SetLivery")]
        internal static class LiveryChanged
        {
            private static void Prefix(Aircraft ___aircraft,ref LiveryData livery)
            {
                if(___aircraft==null||___aircraft.GetComponent<Fs20NAMarker>()!=null||___aircraft.GetComponent<Fs20TDeltaPaint>()==null)return;
                var prepared=Fs20PreparedLiveries.Resolve("T",livery);Fs20PreparedLiveries.Select(___aircraft,prepared);livery=prepared.Data;
            }

            private static void Postfix(Aircraft ___aircraft, LiveryData livery)
            {
                var paint = ___aircraft == null ? null : ___aircraft.GetComponent<Fs20TDeltaPaint>();
                if (paint == null || paint.panels == null) return;
                try {
                    bool na=___aircraft.GetComponent<Fs20NAMarker>()!=null;
                    var prepared=Fs20PreparedLiveries.Current(___aircraft,na?"NA":"T");prepared.Load();
                    paint.glossiness=prepared.glossiness;paint.ApplyPrepared(prepared.wingTexture);
                }
                catch (Exception error) { Debug.LogWarning("FS-20T paint kept its current finish: " + error.Message); }
            }
        }
    }
}

