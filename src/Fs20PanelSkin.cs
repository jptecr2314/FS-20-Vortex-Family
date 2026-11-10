using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20PanelSkin
    {
        internal static void Create(Transform root, Transform owner, Transform hinge, string key, int side, Fs20SkinSurgery.Surface surface)
        {
            var data=Fs20InternalMeshes.Get(key);
            var points=data.Vertices.Select(p=>new Vector3(p.x*side,p.y,p.z)).ToArray();
            var triangles=Fs20PanelUv.OutwardTriangles(points,data.Triangles);
            var uv=surface.Panels.Coordinates(points);
            var map=hinge.worldToLocalMatrix*root.localToWorldMatrix;
            var mesh=new Mesh{name="FS20 conformal forward panel",indexFormat=IndexFormat.UInt32};
            mesh.vertices=points.Select(map.MultiplyPoint3x4).ToArray();mesh.triangles=triangles;mesh.uv=uv;
            mesh.colors=Enumerable.Repeat(Color.white,points.Length).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var ob=new GameObject("FS20 native-painted forward panel");ob.layer=owner.gameObject.layer;ob.transform.SetParent(hinge,false);
            ob.AddComponent<MeshFilter>().sharedMesh=mesh;ob.AddComponent<MeshRenderer>().sharedMaterial=surface.SkinMaterial;
            Fs20HookInterior.RegisterDamageRenderer(owner,ob.GetComponent<Renderer>());
        }
    }
}
