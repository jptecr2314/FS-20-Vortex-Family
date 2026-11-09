using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Fixed fuselage mount.
    internal static class Fs20VentralHookBay
    {
        internal static readonly Vector3 Mount = new Vector3(0, -1.45f, -2.90f);
        internal static readonly Vector2[] Outline =
        {
            new Vector2(-.15f, -2.82f),
            new Vector2(.15f, -2.82f),
            new Vector2(.17f, -2.92f),
            new Vector2(.17f, -3.87f),
            new Vector2(.11f, -3.98f),
            new Vector2(-.11f, -3.98f),
            new Vector2(-.17f, -3.87f),
            new Vector2(-.17f, -2.92f)
        };
        private static float Height(float z, bool roof) => (roof ? -1.275f : -1.53f) - Mathf.Tan(9.77f * Mathf.Deg2Rad) * (z + 2.82f);
        internal static void Build(Transform root, Material metal)
        {
            if (root.GetComponent<AeroPart>() == null)
                throw new System.InvalidOperationException("Fixed fuselage part missing.");
            var bay = root.gameObject.AddComponent<Fs20HookBay>();
            bay.Cover = new GameObject("Fixed belly hook hatch hinge").transform;
            bay.Cover.SetParent(root, false);
            bay.Cover.localPosition = new Vector3(-.17f, -1.53f, -2.82f);
            bay.Cover.localRotation = Quaternion.Euler(9.77f, 0, 0);
            bay.ClosedPosition = bay.Cover.localPosition;
            bay.ClosedRotation = bay.Cover.localRotation;
            bay.DropOffset = Vector3.down * .025f;
            bay.OpeningDegrees = 80f;
            var shell = new Fs20HookInterior.Parts();
            var lid = new Fs20HookInterior.Parts();
            var center = new Vector3(0, Height(-3.40f, true), -3.40f);
            for (int i = 0; i < Outline.Length; i++)
            {
                var a = Outline[i];
                var b = Outline[(i + 1) % Outline.Length];
                var p = new Vector3(a.x, Height(a.y, true), a.y);
                var q = new Vector3(b.x, Height(b.y, true), b.y);
                var lowerP = new Vector3(a.x, Height(a.y, false), a.y);
                var lowerQ = new Vector3(b.x, Height(b.y, false), b.y);
                shell.Triangle(center, p, q, new Vector2(center.x, center.z), a, b, true);
                float length = (q - p).magnitude;
                shell.Quad(p, q, lowerQ, lowerP, Vector2.zero, new Vector2(length, 0), new Vector2(length, .255f), new Vector2(0, .255f), true);
                var origin = new Vector3(0, Height(-3.40f, false), -3.40f);
                // Map the housing separately from the native hook.
                lid.Triangle(bay.Cover.InverseTransformPoint(root.TransformPoint(origin)), bay.Cover.InverseTransformPoint(root.TransformPoint(lowerP)), bay.Cover.InverseTransformPoint(root.TransformPoint(lowerQ)), new Vector2(0, 0), a - new Vector2(0, -3.40f), b - new Vector2(0, -3.40f), true);
            }

            var inner = shell.Create("Sealed fixed belly hook compartment", root, metal);
            var door = lid.Create("Low-profile belly hook cover", bay.Cover, metal);
            Fs20HookInterior.RegisterDamageRenderer(root, inner.GetComponent<Renderer>());
            Fs20HookInterior.RegisterDamageRenderer(root, door.GetComponent<Renderer>());
        }
    }
}
