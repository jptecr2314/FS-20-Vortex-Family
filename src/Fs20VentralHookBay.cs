using System;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20VentralHookBay
    {
        internal static readonly Vector3 Mount = new Vector3(Fs20BaySpec.HookX, Fs20BaySpec.HookY, Fs20BaySpec.HookZ);
        internal static void Build(Transform root, Material metal)
        {
            var outline = Fs20InternalMeshes.Get("hook_outline");
            var bay = root.gameObject.AddComponent<Fs20HookBay>();
            var hinge = Fs20SkinSurgery.Attach(root, root, "FS20 concealed hook hatch hinge", outline.Pivot);
            hinge.localRotation = Quaternion.Euler(outline.Angle, 0, 0);
            bay.Cover = hinge;bay.ClosedPosition = hinge.localPosition;bay.ClosedRotation = hinge.localRotation;
            bay.DropOffset = Vector3.down * Fs20BaySpec.HookDoorDrop;bay.OpeningDegrees = Fs20BaySpec.HookDoorAngle;
            var planes = new System.Collections.Generic.List<Vector4>();
            for (int i = 0; i < outline.Vertices.Length; i++)
            {
                var a = outline.Vertices[i];var b = outline.Vertices[(i + 1) % outline.Vertices.Length];var e = b - a;
                planes.Add(new Vector4(e.z, 0, -e.x, -e.z * a.x + e.x * a.z));
            }
            planes.Add(new Vector4(0, -1, 0, -.95f));
            var framePlanes = planes.ToArray();framePlanes[framePlanes.Length - 1] = new Vector4(0, -1, 0, -.85f);
            Fs20SkinSurgery.Cut(root, new[] { root, root.Find("weaponBay_L"), root.Find("weaponBay_R"), root.Find("engine/fuselage_R") }.Where(p => p != null),
                new[] { new Fs20SkinSurgery.Door { Name = "internal hook hatch", Hinge = hinge, Owner = root, Planes = planes.ToArray(), BackThickness = .008f, BackMaterial = Fs20InteriorFinish.Material(root) } }, false, new[] { framePlanes });
            Fs20HatchHinges.Install(root, hinge);
            var well = Fs20InternalMeshes.Create("hook_well", root, root, Fs20InteriorFinish.Material(root));
            Fs20HookInterior.RegisterDamageRenderer(root, well.GetComponent<Renderer>());
        }
    }
}
