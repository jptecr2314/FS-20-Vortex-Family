using System.Collections.Generic;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal sealed class Fs20PanelUv
    {
        private readonly float[][] triangles;
        internal Fs20PanelUv(float[][] triangles) { this.triangles = triangles; }
        internal bool TrySample(float x, float z, out Vector2 uv)
        {
            uv = Vector2.zero;float lowest = float.PositiveInfinity;bool found = false;
            foreach (var p in triangles)
            {
                float den = (p[7] - p[12]) * (p[0] - p[10]) + (p[10] - p[5]) * (p[2] - p[12]);
                if (Mathf.Abs(den) < 1e-9f) continue;
                float u = ((p[7] - p[12]) * (x - p[10]) + (p[10] - p[5]) * (z - p[12])) / den;
                float v = ((p[12] - p[2]) * (x - p[10]) + (p[0] - p[10]) * (z - p[12])) / den;
                if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                float w = 1 - u - v, y = u * p[1] + v * p[6] + w * p[11];
                if (y >= lowest) continue;
                lowest = y;uv = new Vector2(u * p[3] + v * p[8] + w * p[13], u * p[4] + v * p[9] + w * p[14]);found = true;
            }
            return found;
        }
        internal Vector2[] Coordinates(Vector3[] points)
        {
            var result = new Vector2[points.Length];
            for (int i=0;i<points.Length;i++)
            {
                // Continue the clean native door paint onto the extension.
                float x = Mathf.Sign(points[i].x) * Mathf.Clamp(Mathf.Abs(points[i].x), .15f, .94f);
                if (!TrySample(x, points[i].z - 1.20f, out result[i]))
                    throw new System.InvalidOperationException("Native FS-20 door paint layout changed.");
            }
            return result;
        }
        internal static int[] OutwardTriangles(Vector3[] points, int[] input)
        {
            var result=(int[])input.Clone();
            for(int i=0;i<result.Length;i+=3)
                if(Vector3.Cross(points[result[i+1]]-points[result[i]],points[result[i+2]]-points[result[i]]).y>0)
                {int swap=result[i+1];result[i+1]=result[i+2];result[i+2]=swap;}
            return result;
        }
    }
}
