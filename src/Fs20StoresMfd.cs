using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NuclearOption.UIStyleSystem;

namespace Japensou.NuclearOption.VortexConventional
{
    // Adapt the actual native screen; do not rebuild its artwork, typography or borders.
    public sealed class Fs20StoresMfd : MonoBehaviour
    {
        private Aircraft aircraft;
        private readonly List<Entry> entries = new List<Entry>();
        private float refreshAt;
        private WeaponStation displayedStation;
        private static readonly FieldInfo Station = AccessTools.Field(typeof(Weapon), "weaponStation");
        private sealed class Ink
        {
            internal Graphic Graphic;
            internal float Alpha, Saturation;
        }
        private sealed class Entry
        {
            internal Hardpoint Point;
            internal TextMeshProUGUI Title, Weapon, Status;
            internal Ink[] Ink;
            internal Image Marker;
            internal bool Gun;
        }
        internal static bool IsVariant(Aircraft a)
        {
            if (!a || !a.definition) return false;
            string key = a.definition.jsonKey;
            return key == "JP_FS20_A" || key == "JP_FS20_CTOL" || key == "JP_FS20_T" || key == "JP_FS20_NA";
        }
        [HarmonyPatch(typeof(PylonIndicator), "Start", new Type[0])]
        internal static class Initialize
        {
            private static bool Prefix(PylonIndicator __instance)
            {
                Aircraft local;
                if (!GameManager.GetLocalAircraft(out local) || !IsVariant(local)) return true;
                AccessTools.Field(typeof(PylonIndicator), "attachedAircraft").SetValue(__instance, local);
                var ui = __instance.gameObject.AddComponent<Fs20StoresMfd>();
                try { ui.Build(local); }
                catch (Exception e) { ui.enabled = false; VortexConventionalPlugin.ReportAssetState("FS-20 native stores adaptation unavailable: " + e); }
                // The donor's fixed seven-element array cannot address the new IDs.
                return false;
            }
        }
        [HarmonyPatch(typeof(PylonIndicator), "UpdateImages", new Type[0])]
        internal static class NativeUpdate
        {
            private static bool Prefix(PylonIndicator __instance)
            {
                var ui = __instance.GetComponent<Fs20StoresMfd>();
                if (!ui) return true;
                ui.refreshAt = 0; return false;
            }
        }
        private void Build(Aircraft local)
        {
            aircraft = local;
            var profile = transform.Find("frontProfile") as RectTransform;
            var lower = transform.Find("lowerWeaponsPanel") as RectTransform;
            var gunBox = transform.Find("Box_Gun");
            if (!profile || !lower || !gunBox || lower.childCount < 6) throw new InvalidOperationException("Native FS-20 stores hierarchy changed");
            var image = profile.GetComponent<Image>();
            if (!image || !image.sprite) throw new InvalidOperationException("Native FS-20 front-profile sprite missing");
            var template = lower.GetChild(0).gameObject;
            if (!template.transform.Find("Title") || !template.transform.Find("WPN") || !template.transform.Find("Value") || !template.transform.Find("Border"))
                throw new InvalidOperationException("Native station card contract changed");
            var markerTemplate = profile.Find("hardpoint_PylonR");
            if (!markerTemplate || !markerTemplate.GetComponent<Image>()) throw new InvalidOperationException("Native station marker missing");

            // All native profile/material/rectangle settings stay intact. Deform only the wing
            // extent for the enlarged wing, and clip the retired fins from the NA diagram.
            float ratio = Fs20NativeMfdProfile.SpanRatio(local.definition.jsonKey);
            var projection = profile.gameObject.AddComponent<Fs20NativeMfdProfile>();
            projection.Configure(ratio, local.definition.jsonKey == "JP_FS20_NA");
            var points = new Dictionary<int, KeyValuePair<string, Hardpoint>>();
            foreach (var group in local.weaponManager.hardpointSets)
            {
                if (group.name.IndexOf("Hook", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                foreach (var point in group.hardpoints)
                    if (!points.ContainsKey(point.HardpointIndex)) points.Add(point.HardpointIndex, new KeyValuePair<string, Hardpoint>(group.name, point));
            }
            var stores = points.Values.Where(p => p.Key != "Internal Cannon")
                .OrderByDescending(p => local.transform.InverseTransformPoint(p.Value.transform.position).x).ToList();
            bool pairedRows = stores.Count > 0;
            // In the two-row view, align the same station class on both wings:
            // bay -> inner -> center -> outer -> tip, right row above left row.
            if (pairedRows) stores = stores
                .OrderByDescending(p => local.transform.InverseTransformPoint(p.Value.transform.position).x > 0f)
                .ThenBy(p => Mathf.Abs(local.transform.InverseTransformPoint(p.Value.transform.position).x))
                .ThenBy(p => p.Value.HardpointIndex).ToList();
            var originals = new List<GameObject>();
            foreach (Transform child in lower) originals.Add(child.gameObject);
            var layout = lower.GetComponent<HorizontalLayoutGroup>();
            // All family variants use paired right/left rows, aligned from bay to tip,
            // reusing native cards and their sliced borders in the same rectangle.
            if (pairedRows && layout) layout.enabled = false;
            var cards = new List<Transform>();
            for (int i = 0; i < stores.Count; i++)
            {
                var card = i < originals.Count ? originals[i].transform : Instantiate(template, lower, false).transform;
                card.gameObject.SetActive(true); cards.Add(card);
            }
            for (int i = stores.Count; i < originals.Count; i++) originals[i].SetActive(false);
            foreach (Transform marker in profile) if (marker.GetComponent<Image>()) marker.gameObject.SetActive(false);
            int columns = (stores.Count + 1) / 2;
            float spacing = layout ? layout.spacing : 2f;
            if (pairedRows)
            {
                float width = (lower.rect.width - spacing * (columns - 1)) / columns;
                float height = (lower.rect.height - spacing) / 2;
                for (int i = 0; i < cards.Count; i++)
                {
                    var rect = (RectTransform)cards[i];
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
                    rect.sizeDelta = new Vector2(width, height);
                    rect.anchoredPosition = new Vector2((i % columns - (columns - 1) * .5f) * (width + spacing), (i < columns ? 1 : -1) * (height + spacing) * .5f);
                    foreach (var text in rect.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        // Keep the donor's 40 pt font, face, material and weight. Only
                        // adapt its existing transform scale to the smaller card height.
                        text.rectTransform.localScale *= .8f;
                        text.rectTransform.sizeDelta = new Vector2((width - 6f) / text.rectTransform.localScale.x, text.rectTransform.sizeDelta.y);
                        var p = text.rectTransform.anchoredPosition;
                        p.y = text.name == "Title" ? 15.5f : text.name == "Value" ? -15.5f : 0f;
                        text.rectTransform.anchoredPosition = p;
                    }
                }
            }
            for (int i = 0; i < stores.Count; i++)
            {
                var pair = stores[i]; var marker = Instantiate(markerTemplate, profile, false);
                marker.name = "FS20 station marker " + pair.Value.HardpointIndex;
                marker.gameObject.SetActive(true);
                var p = local.transform.InverseTransformPoint(pair.Value.transform.position);
                float y = pair.Key.IndexOf("Bay", StringComparison.OrdinalIgnoreCase) >= 0 ? -32.6f : -15.7f;
                ((RectTransform)marker).anchoredPosition = new Vector2(-p.x * profile.rect.width / (2f * Fs20NativeMfdProfile.NativeHalfSpan) / ratio, y / ratio);
                Bind(cards[i], marker, pair.Value, Label(pair.Key, p.x), false);
            }
            var cannon = points.Values.FirstOrDefault(p => p.Key == "Internal Cannon");
            if (cannon.Value != null)
            {
                var marker = profile.Find("hardpoint_Gun"); marker.gameObject.SetActive(true);
                ((RectTransform)marker).anchoredPosition /= ratio;
                Bind(gunBox, marker, cannon.Value, null, true);
            }
            Refresh();
        }
        private void Bind(Transform box, Transform marker, Hardpoint point, string label, bool gun)
        {
            var title = box.Find("Title").GetComponent<TextMeshProUGUI>();
            if (label != null) title.text = label;
            var weapon = gun ? null : box.Find("WPN").GetComponent<TextMeshProUGUI>();
            var status = box.Find("Value").GetComponent<TextMeshProUGUI>();
            var graphics = box.GetComponentsInChildren<Graphic>(true);
            var inks = new List<Ink>();
            foreach (var graphic in graphics)
            {
                float h, s, v; Color.RGBToHSV(graphic.color, out h, out s, out v);
                inks.Add(new Ink { Graphic = graphic, Alpha = graphic.color.a, Saturation = s });
            }
            entries.Add(new Entry { Point = point, Title = title, Weapon = weapon, Status = status, Ink = inks.ToArray(), Marker = marker.GetComponent<Image>(), Gun = gun });
        }
        internal static string Label(string group, float x)
        {
            string side = x > 0 ? "R." : "L.";
            if (group.IndexOf("Bay", StringComparison.OrdinalIgnoreCase) >= 0) return side + "BAY";
            if (group.IndexOf("tip", StringComparison.OrdinalIgnoreCase) >= 0) return side + "TIP";
            if (group.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0) return side + "CENTER";
            if (group.IndexOf("Inner", StringComparison.OrdinalIgnoreCase) >= 0) return side + "INNER";
            if (group.IndexOf("Outer", StringComparison.OrdinalIgnoreCase) >= 0) return side + "OUTER";
            return side + group;
        }
        private void Update()
        {
            if (!aircraft || !aircraft.weaponManager) return;
            // Selection changes update on the next UI frame, without adding a native hook.
            if (ReferenceEquals(displayedStation, aircraft.weaponManager.currentWeaponStation) && Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .25f; Refresh();
        }
        private void Refresh()
        {
            if (!aircraft || !aircraft.weaponManager) return;
            var colors = ThemeManager.Active.ColorTheme;
            var selectedStation = aircraft.weaponManager.currentWeaponStation;
            displayedStation = selectedStation;
            foreach (var entry in entries)
            {
                List<Weapon> weapons;
                aircraft.weaponManager.HardpointsIndexes.TryGetValue(entry.Point.HardpointIndex, out weapons);
                int ammo = 0; bool loaded = false, safe = true, selected = false; string name = "";
                if (weapons != null) foreach (var weapon in weapons)
                {
                    if (!weapon) continue; loaded = true; ammo += Math.Max(0, weapon.ammo);
                    if (weapon.info && name.Length == 0) name = weapon.info.shortName;
                    var station = Station.GetValue(weapon) as WeaponStation;
                    if (selectedStation != null && ReferenceEquals(station, selectedStation) && weapon.ammo > 0) selected = true;
                    if (weapon.ammo > 0 && !weapon.Safety && (station == null || !station.SafetyIsOn(aircraft))) safe = false;
                }
                bool detached = !entry.Point.part || entry.Point.part.IsDetached() || entry.Point.part.hitPoints <= 0;
                string status = Status(detached, loaded, ammo, safe);
                Color color = detached || (loaded && ammo == 0) ? colors.Alert : !loaded || safe ? Color.gray : colors.AllClear;
                string statusText = !entry.Gun && (status == "ARMED" || status == "SAFE") ? ammo + " " + status : status;
                if (entry.Weapon)
                {
                    string weaponText = detached ? "" : loaded ? name : "EMPTY";
                    if (entry.Weapon.text != weaponText) entry.Weapon.text = weaponText;
                }
                if (entry.Status.text != statusText) entry.Status.text = statusText;
                float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
                foreach (var ink in entry.Ink)
                {
                    var tint = Color.HSVToRGB(h, s * ink.Saturation, v, true); tint.a = ink.Alpha;
                    ink.Graphic.color = tint;
                }
                // Keep attached station locations visible at low intensity, with the
                // selected loaded group at full intensity. State colors stay independent.
                if (entry.Marker)
                {
                    var markerColor = color;
                    markerColor.a = detached ? 0f : selected ? 1f : .25f;
                    entry.Marker.color = markerColor;
                }
            }
        }
        internal static string Status(bool detached, bool loaded, int ammo, bool safe)
        {
            return detached ? "LOST" : !loaded ? "EMPTY" : ammo <= 0 ? "EXPENDED" : safe ? "SAFE" : "ARMED";
        }
    }

    // Reuse the installed white wire drawing and its material, without pixel reads,
    // new textures or per-frame geometry work. Piecewise wing projection preserves
    // the native fuselage linework; NA omits the two retired fins in the UV mesh.
    public sealed class Fs20NativeMfdProfile : BaseMeshEffect
    {
        internal const float NativeHalfSpan = 4.6736846f;
        private float ratio = 1f;
        private bool tailless;
        internal static float SpanRatio(string key)
        {
            switch (key)
            {
                case "JP_FS20_CTOL": return 5.6919327f / NativeHalfSpan;
                case "JP_FS20_T": return 5.6899982f / NativeHalfSpan;
                case "JP_FS20_NA": return 5.6904469f / NativeHalfSpan;
                default: return 1f;
            }
        }
        internal void Configure(float extentRatio, bool removeFins)
        {
            ratio = extentRatio; tailless = removeFins; graphic.SetVerticesDirty();
        }
        private static readonly Vector2[] Crown = {
            new Vector2(0,245),new Vector2(280,235),new Vector2(340,215),new Vector2(390,195),
            new Vector2(423,171),new Vector2(445,146),new Vector2(470,124),new Vector2(492,116),
            new Vector2(512,113),new Vector2(532,116),new Vector2(554,124),new Vector2(579,146),
            new Vector2(601,171),new Vector2(634,195),new Vector2(684,215),new Vector2(744,235),new Vector2(1024,245)
        };
        private float Top(float u)
        {
            if (!tailless) return 1f;
            float x = u * 1024f;
            for (int i = 1; i < Crown.Length; i++) if (x <= Crown[i].x)
                return 1f - Mathf.Lerp(Crown[i - 1].y, Crown[i].y, Mathf.InverseLerp(Crown[i - 1].x, Crown[i].x, x)) / 512f;
            return 1f - Crown[Crown.Length - 1].y / 512f;
        }
        private UIVertex Vertex(float u, float v, Vector4 uv, Rect rect)
        {
            float x = 2f * u - 1f, abs = Mathf.Abs(x);
            const float shoulder = .44f;
            float warped = abs <= shoulder ? abs : shoulder + (abs - shoulder) * (ratio - shoulder) / (1f - shoulder);
            var vertex = UIVertex.simpleVert; vertex.color = graphic.color;
            vertex.position = new Vector3(rect.center.x + Mathf.Sign(x) * warped / ratio * rect.width * .5f, rect.center.y + (v - .5f) / ratio * rect.height);
            vertex.uv0 = new Vector2(Mathf.Lerp(uv.x, uv.z, u), Mathf.Lerp(uv.y, uv.w, v));
            return vertex;
        }
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            var image = graphic as Image; if (!image || !image.sprite) return;
            var rect = graphic.rectTransform.rect;
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite);
            var cuts = new SortedSet<float>(); for (int i = 0; i <= 64; i++) cuts.Add(i / 64f);
            cuts.Add((1f - .44f) * .5f); cuts.Add((1f + .44f) * .5f);
            if (tailless) foreach (var p in Crown) cuts.Add(p.x / 1024f);
            helper.Clear(); float previous = 0; bool first = true;
            foreach (float current in cuts)
            {
                if (first) { previous = current; first = false; continue; }
                int start = helper.currentVertCount;
                helper.AddVert(Vertex(previous, 0, uv, rect)); helper.AddVert(Vertex(previous, Top(previous), uv, rect));
                helper.AddVert(Vertex(current, Top(current), uv, rect)); helper.AddVert(Vertex(current, 0, uv, rect));
                helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3); previous = current;
            }
        }
    }
}
