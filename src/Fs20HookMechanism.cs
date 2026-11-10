using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    public sealed class Fs20HookMechanism : MonoBehaviour
    {
        [SerializeField] internal Transform Arm, Cylinder, Rod;
        internal static void Install(Transform root, Transform arm, Material metal)
        {
            for (int i = 0; i < 3; i++)
                Fs20InternalMeshes.Create("hook_support_" + i, root, root, metal, 1, -Fs20VentralHookBay.Mount);
            Fs20InternalMeshes.Create("hook_pin", root, root, metal, 1, -Fs20VentralHookBay.Mount);
            var mechanism = root.gameObject.AddComponent<Fs20HookMechanism>();mechanism.Arm = arm;
            mechanism.Cylinder = CylinderObject(root, "FS20 hook actuator housing", metal);
            mechanism.Rod = CylinderObject(root, "FS20 hook actuator rod", metal);
            mechanism.Advance();
        }
        private static Transform CylinderObject(Transform root, string name, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);obj.name = name;obj.transform.SetParent(root, false);
            DestroyImmediate(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial = material;return obj.transform;
        }
        private void LateUpdate() => Advance();
        private void Advance()
        {
            if (Arm == null || Cylinder == null || Rod == null) return;
            var anchor = new Vector3(.095f, .18f, .02f);
            var end = transform.InverseTransformPoint(Arm.TransformPoint(new Vector3(.095f, .015f, -.25f / Fs20BaySpec.HookScale)));
            var axis = (end - anchor).normalized;
            Pose(Cylinder, anchor, anchor + axis * .20f, .016f);
            Pose(Rod, anchor + axis * .18f, end, .009f);
        }
        private static void Pose(Transform part, Vector3 a, Vector3 b, float radius)
        {
            part.localPosition = (a + b) * .5f;part.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            part.localScale = new Vector3(radius * 2, (b - a).magnitude * .5f, radius * 2);
        }
    }
}
