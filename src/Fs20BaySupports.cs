using System;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20BaySupports : MonoBehaviour
    {
        [SerializeField] internal int Side;
        [SerializeField] internal MeshFilter[] Brackets;
        private Aircraft aircraft;
        private Vector3[][] original;
        private Mesh[] meshes;
        private bool? augerLoaded;

        private void Start()
        {
            aircraft = GetComponentInParent<Aircraft>();
            if (aircraft == null || Brackets == null) return;
            original = new Vector3[Brackets.Length][];meshes = new Mesh[Brackets.Length];
            for (int i = 0; i < Brackets.Length; i++)
            {
                // Prefab instances share the initial meshes. Each aircraft
                // receives its own copies before a loadout changes the links.
                meshes[i] = Instantiate(Brackets[i].sharedMesh);
                Brackets[i].sharedMesh = meshes[i];original[i] = meshes[i].vertices;
            }
            aircraft.weaponManager.OnWeaponsLoaded += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            bool auger = aircraft.loadout != null && aircraft.loadout.weapons.Count > 1 &&
                Fs20AugerMount.IsAuger(aircraft.loadout.weapons[1]?.jsonKey);
            if (augerLoaded == auger) return;
            augerLoaded = auger;
            for (int i = 0; i < Brackets.Length; i++)
            {
                var points = (Vector3[])original[i].Clone();
                if (auger)
                {
                    var toRoot = aircraft.transform.worldToLocalMatrix * Brackets[i].transform.localToWorldMatrix;
                    var fromRoot = Brackets[i].transform.worldToLocalMatrix * aircraft.transform.localToWorldMatrix;
                    for (int j = 0; j < points.Length; j++) points[j] = toRoot.MultiplyPoint3x4(points[j]);
                    Retarget(points, i, Side, Fs20AugerMount.RackCenter(Side));
                    for (int j = 0; j < points.Length; j++) points[j] = fromRoot.MultiplyPoint3x4(points[j]);
                }
                meshes[i].vertices = points;meshes[i].RecalculateNormals();
                meshes[i].RecalculateTangents();meshes[i].RecalculateBounds();
            }
        }

        internal static void Retarget(Vector3[] points, int index, int side, Vector3 center)
        {
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (var p in points)
            {
                lo = Math.Min(lo, side * p.x);hi = Math.Max(hi, side * p.x);
                bottom = Math.Min(bottom, p.y);top = Math.Max(top, p.y);
            }
            for (int j = 0; j < points.Length; j++)
            {
                var p = points[j];
                if (index % 2 == 0)
                {
                    p.x = side * (lo + (side * p.x - lo) / (hi - lo) * (Math.Abs(center.x) + .01f - lo));
                    p.y += center.y - (bottom + top) * .5f;
                }
                else p.y = top + (p.y - top) / (bottom - top) * (center.y - .04f - top);
                points[j] = p;
            }
        }

        private void OnDestroy()
        {
            if (aircraft != null) aircraft.weaponManager.OnWeaponsLoaded -= Refresh;
            if (meshes != null) foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
