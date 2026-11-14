using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Japensou.NuclearOption.VortexConventional
{
    // Split drag leaves expose the inside of a skin that was previously hidden
    // in the closed elevon. Give unpaired skin faces a thin inner wall and rim.
    internal static class Fs20NASplitShell
    {
        internal static void Close(Mesh mesh,Material[] materials)
        {
            const float thickness=.002f;
            var source=mesh.vertices;var sourceNormals=mesh.normals;var sourceUv=mesh.uv;var sourceColors=mesh.colors32;
            var points=new List<Vector3>(source);var normals=new List<Vector3>(sourceNormals);
            var uv=new List<Vector2>(sourceUv);while(uv.Count<points.Count)uv.Add(Vector2.zero);
            var colors=Enumerable.Range(0,source.Length).Select(i=>sourceColors.Length==source.Length?sourceColors[i]:new Color32(255,255,255,255)).ToList();
            string Key(int i){var p=source[i];return Mathf.RoundToInt(p.x*100000f)+","+Mathf.RoundToInt(p.y*100000f)+","+Mathf.RoundToInt(p.z*100000f);}
            string Face(int a,int b,int c){var keys=new[]{Key(a),Key(b),Key(c)};Array.Sort(keys,StringComparer.Ordinal);return string.Join("/",keys);}
            string Edge(int a,int b){string x=Key(a),y=Key(b);return string.CompareOrdinal(x,y)<0?x+"/"+y:y+"/"+x;}
            var indices=Enumerable.Range(0,mesh.subMeshCount).Select(s=>new List<int>(mesh.GetTriangles(s))).ToArray();
            for(int sub=0;sub<indices.Length&&sub<materials.Length;sub++){
                if(materials[sub]==null||materials[sub].name.IndexOf("skin",StringComparison.OrdinalIgnoreCase)<0)continue;
                var original=indices[sub].ToArray();var faces=new Dictionary<string,List<int>>();
                for(int i=0;i<original.Length;i+=3){string key=Face(original[i],original[i+1],original[i+2]);if(!faces.TryGetValue(key,out var list))faces.Add(key,list=new List<int>());list.Add(i);}
                var unpaired=faces.Values.Where(v=>v.Count==1).Select(v=>v[0]).ToArray();
                var back=new Dictionary<int,int>();var edges=new Dictionary<string,List<int[]>>();
                int Inner(int i){if(back.TryGetValue(i,out var j))return j;j=points.Count;back.Add(i,j);points.Add(source[i]-sourceNormals[i]*thickness);normals.Add(-sourceNormals[i]);uv.Add(i<sourceUv.Length?sourceUv[i]:Vector2.zero);colors.Add(colors[i]);return j;}
                foreach(int i in unpaired){
                    int a=original[i],b=original[i+1],c=original[i+2];
                    indices[sub].AddRange(new[]{Inner(c),Inner(b),Inner(a)});
                    foreach(var edge in new[]{new[]{a,b},new[]{b,c},new[]{c,a}}){string key=Edge(edge[0],edge[1]);if(!edges.TryGetValue(key,out var list))edges.Add(key,list=new List<int[]>());list.Add(edge);}
                }
                foreach(var boundary in edges.Values.Where(v=>v.Count==1)){
                    int a=boundary[0][0],b=boundary[0][1];var corners=new[]{source[a],source[b],points[Inner(b)],points[Inner(a)]};
                    var n=Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]).normalized;int start=points.Count;
                    for(int k=0;k<4;k++){points.Add(corners[k]);normals.Add(n);uv.Add(k==0||k==3?uv[a]:uv[b]);colors.Add(k==0||k==3?colors[a]:colors[b]);}
                    indices[sub].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
            }
            mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(points);mesh.SetNormals(normals);mesh.SetUVs(0,uv);
            mesh.SetColors(colors);
            for(int sub=0;sub<indices.Length;sub++)mesh.SetTriangles(indices[sub],sub,false);
            mesh.RecalculateBounds();mesh.RecalculateTangents();mesh.name="FS20NA closed split skin";
        }
    }
}
