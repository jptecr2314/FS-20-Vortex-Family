using System;
using UnityEngine;
namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20DuctIslandSpec
    {
        internal static void RemoveCenter(Transform root)
        {
            foreach (string material in new[] { "aircraft_structure", "jet_mechanical" })
                Remove(root, material, Rules(material));
        }
        internal static Fs20IslandRule[] Rules(string material)
        {
            // Islands belong to a material submesh. Applying the combined
            // selector to each submesh aborts registration of both variants.
            if (material == "aircraft_structure") return new[] {
                new Fs20IslandRule(160, -.61917f, .61917f, -.86511f, .37323f, .55178f, 1.26313f),
                new Fs20IslandRule(64, -.61917f, .61917f, -.86511f, .37323f, -.93429f, -.34538f)
            };
            if (material == "jet_mechanical") return new[] {
                new Fs20IslandRule(32, -.61917f, .61917f, -.86511f, .37323f, -.34538f, .55178f),
                new Fs20IslandRule(48, -.56302f, .56302f, -.80896f, .31708f, .80446f, 1.03428f),
                new Fs20IslandRule(32, -.61829f, .61829f, -.87755f, .35902f, .64264f, 1.16105f),
                new Fs20IslandRule(32, -.63490f, .63490f, -.88084f, .38896f, -.35678f, .56318f),
                new Fs20IslandRule(32, -.62087f, .62087f, -.86681f, .37493f, -.94409f, -.43079f)
            };
            throw new InvalidOperationException("Unknown FS-20 center duct material: " + material);
        }
        private static void Remove(Transform root, string material, Fs20IslandRule[] rules)
        {
            var filter = root.GetComponent<MeshFilter>();
            var renderer = root.GetComponent<Renderer>();
            var mesh = Fs20Geometry.ReadableCopy(filter.sharedMesh);
            int slot = Array.FindIndex(renderer.sharedMaterials, m => m != null && m.name.StartsWith(material, StringComparison.Ordinal));
            if(slot<0) throw new InvalidOperationException("FS-20 center duct material changed.");
            var v=mesh.vertices;var p=new float[v.Length*3];
            for(int i=0;i<v.Length;i++){p[i*3]=v[i].x;p[i*3+1]=v[i].y;p[i*3+2]=v[i].z;}
            mesh.SetTriangles(Fs20MeshIslands.Remove(p,mesh.GetTriangles(slot),rules),slot);
            mesh.RecalculateBounds();filter.sharedMesh=mesh;
        }
    }
}
