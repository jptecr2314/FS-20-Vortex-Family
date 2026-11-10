using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20HatchHinges
    {
        internal static void Install(Transform root, Transform cover)
        {
            var frame = new GameObject("FS20 fixed hook hatch hinge frame").transform;
            frame.SetParent(root, false);frame.localPosition = cover.localPosition;frame.localRotation = cover.localRotation;
            var metal = Fs20HookInterior.NativeMetal(root);
            var owner = root.GetComponent<AeroPart>();
            if (owner == null) throw new System.InvalidOperationException("FS-20 hook hatch owner is missing.");
            foreach (float z in new[] { -.20f, -.65f })
            {
                Cylinder(owner, frame, "Fixed hinge barrel", new Vector3(0, 0, z - .026f), .008f, .020f, metal);
                Cylinder(owner, frame, "Fixed hinge barrel", new Vector3(0, 0, z + .026f), .008f, .020f, metal);
                Cylinder(owner, cover, "Moving hatch knuckle", new Vector3(0, 0, z), .008f, .030f, metal);
                Cylinder(owner, frame, "Captive hinge pin", new Vector3(0, 0, z), .003f, .080f, metal);
                Tab(owner, frame, "Hinge frame attachment", new Vector3(-.016f, .012f, z), new Vector3(.040f, .028f, .075f), metal);
                Tab(owner, cover, "Hatch hinge attachment", new Vector3(.020f, .004f, z), new Vector3(.048f, .012f, .028f), metal);
            }
        }
        private static void Cylinder(AeroPart owner, Transform parent, string name, Vector3 center, float radius, float length, Material material)
        {
            var obj = Primitive(parent, name, PrimitiveType.Cylinder, material);
            obj.localPosition = center;obj.localRotation = Quaternion.Euler(90, 0, 0);
            obj.localScale = new Vector3(radius * 2, length * .5f, radius * 2);
            Fs20HookInterior.RegisterDamageRenderer(owner.transform, obj.GetComponent<Renderer>());
        }
        private static void Tab(AeroPart owner, Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var obj = Primitive(parent, name, PrimitiveType.Cube, material);
            obj.localPosition = center;obj.localScale = size;
            Fs20HookInterior.RegisterDamageRenderer(owner.transform, obj.GetComponent<Renderer>());
        }
        private static Transform Primitive(Transform parent, string name, PrimitiveType type, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);obj.name = name;obj.layer = parent.gameObject.layer;
            Object.DestroyImmediate(obj.GetComponent<Collider>());obj.transform.SetParent(parent, false);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj.transform;
        }
    }
}
