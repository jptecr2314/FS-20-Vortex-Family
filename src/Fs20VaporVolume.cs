using System;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal sealed partial class Fs20VaporSheet
    {
        private readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        private readonly Vector4[] pressureVectors=new Vector4[4];
        private static Texture3D volumeNoise;
        internal Vector3 TextureOffset { get; private set; }
        internal Vector3 FlowState { get; private set; }
        private static readonly int[] BoxTriangles={0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};

        internal static Texture3D VolumeNoise {
            get {
                if(volumeNoise!=null)return volumeNoise;
                const int n=32;var pixels=new byte[n*n*n];
                float Hash(int x,int y,int z,int period){
                    uint h=unchecked((uint)((x%period)*73856093^(y%period)*19349663^(z%period)*83492791));
                    h^=h>>13;h=unchecked(h*1274126177u);return (h&65535u)/65535f;
                }
                float Noise(float x,float y,float z,int period){
                    int ix=(int)x,iy=(int)y,iz=(int)z;float a=x-ix,b=y-iy,c=z-iz;
                    a=a*a*(3-2*a);b=b*b*(3-2*b);c=c*c*(3-2*c);
                    float low=Mathf.Lerp(Mathf.Lerp(Hash(ix,iy,iz,period),Hash(ix+1,iy,iz,period),a),Mathf.Lerp(Hash(ix,iy+1,iz,period),Hash(ix+1,iy+1,iz,period),a),b);
                    float high=Mathf.Lerp(Mathf.Lerp(Hash(ix,iy,iz+1,period),Hash(ix+1,iy,iz+1,period),a),Mathf.Lerp(Hash(ix,iy+1,iz+1,period),Hash(ix+1,iy+1,iz+1,period),a),b);
                    return Mathf.Lerp(low,high,c);
                }
                for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++){
                    float u=x/(float)n,v=y/(float)n,w=z/(float)n;
                    float value=.58f*Noise(u*4,v*4,w*4,4)+.28f*Noise(u*8,v*8,w*8,8)+.14f*Noise(u*16,v*16,w*16,16);
                    pixels[(z*n+y)*n+x]=(byte)Mathf.RoundToInt(value*255f);
                }
                volumeNoise=new Texture3D(n,n,n,TextureFormat.R8,false){name="FS20 spatial mist density",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
                volumeNoise.SetPixelData(pixels,0);volumeNoise.Apply(false,true);return volumeNoise;
            }
        }

        private static void BuildVolumeSkin(Shape shape,Vector3[] vertices,int[] indices,Transform frame,Transform root)
        {
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var p in vertices)bounds.Encapsulate(p);
            var low=bounds.min-new Vector3(.24f,.05f,.24f);var high=bounds.max+new Vector3(.24f,1.1f,.24f);
            int width=Mathf.Clamp(Mathf.CeilToInt((high.x-low.x)/.045f),24,128),height=Mathf.Clamp(Mathf.CeilToInt((high.z-low.z)/.045f),24,256);
            float sx=(high.x-low.x)/width,sz=(high.z-low.z)/height;
            var valid=new bool[width*height];var heights=new float[valid.Length];for(int i=0;i<heights.Length;i++)heights[i]=float.NegativeInfinity;
            for(int tri=0;tri<indices.Length;tri+=3){
                var a=vertices[indices[tri]];var b=vertices[indices[tri+1]];var c=vertices[indices[tri+2]];
                float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<1e-8f)continue;
                int x0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,Mathf.Min(b.x,c.x))-low.x)/sx),0,width-1),x1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,Mathf.Max(b.x,c.x))-low.x)/sx),0,width-1);
                int z0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,Mathf.Min(b.z,c.z))-low.z)/sz),0,height-1),z1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z,Mathf.Max(b.z,c.z))-low.z)/sz),0,height-1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
                    float px=low.x+(x+.5f)*sx,pz=low.z+(z+.5f)*sz;
                    float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/det,v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/det;
                    if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                    int i=z*width+x;float y=u*a.y+v*b.y+(1-u-v)*c.y;
                    if(y>heights[i]){heights[i]=y;valid[i]=true;}
                }
            }
            int[] Nearest(bool inside){
                var nearest=new int[valid.Length];for(int i=0;i<nearest.Length;i++)nearest[i]=valid[i]==inside?i:-1;
                float Distance(int x,int z,int i){float dx=(x-i%width)*sx,dz=(z-i/width)*sz;return dx*dx+dz*dz;}
                void Try(int x,int z,int xx,int zz){
                    if(xx<0||zz<0||xx>=width||zz>=height)return;int i=z*width+x,other=nearest[zz*width+xx];
                    if(other>=0&&(nearest[i]<0||Distance(x,z,other)<Distance(x,z,nearest[i])))nearest[i]=other;
                }
                for(int z=0;z<height;z++)for(int x=0;x<width;x++){Try(x,z,x-1,z);Try(x,z,x,z-1);Try(x,z,x-1,z-1);Try(x,z,x+1,z-1);}
                for(int z=height-1;z>=0;z--)for(int x=width-1;x>=0;x--){Try(x,z,x+1,z);Try(x,z,x,z+1);Try(x,z,x+1,z+1);Try(x,z,x-1,z+1);}
                return nearest;
            }
            var filled=Nearest(true);var empty=Nearest(false);var pixels=new Color[valid.Length];
            for(int z=0;z<height;z++)for(int x=0;x<width;x++){
                int i=z*width+x,source=filled[i],opposite=valid[i]?empty[i]:filled[i];
                float distance=-10f;if(opposite>=0){float dx=(x-opposite%width)*sx,dz=(z-opposite/width)*sz;distance=Mathf.Sqrt(dx*dx+dz*dz)*(valid[i]?1f:-1f);}
                float chord=Mathf.InverseLerp(bounds.max.z,bounds.min.z,low.z+(z+.5f)*sz);
                pixels[i]=new Color(source<0?0f:heights[source],0f,chord,distance);
            }
            shape.VolumeSkin=new Texture2D(width,height,TextureFormat.RGBAHalf,false,true){name="FS20 cached upper skin",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            shape.VolumeSkin.SetPixels(pixels);shape.VolumeSkin.Apply(false,false);
            shape.VolumeMin=low;shape.VolumeMax=high;shape.GridToFrame=frame.worldToLocalMatrix*root.localToWorldMatrix;shape.FrameToGrid=shape.GridToFrame.inverse;
        }

        private static void JoinVolumeSkin(Shape shape,Vector3[] neighbor,int[] triangles,Transform frame,Transform root,int border)
        {
            var map=shape.VolumeSkin;var pixels=map.GetPixels();
            for(int z=0;z<map.height;z++){
                float pz=Mathf.Lerp(shape.VolumeMin.z,shape.VolumeMax.z,(z+.5f)/map.height);
                float station=Mathf.InverseLerp(shape.VolumeMax.z-.24f,shape.VolumeMin.z+.24f,pz);
                int row=Mathf.RoundToInt(station*(Rows-1));var edge=root.InverseTransformPoint(frame.TransformPoint(shape.Points[row*Across+border]));
                var spans=Fs20TVaporPath.Slice(neighbor,triangles,pz);float nearest=float.PositiveInfinity,joinX=0;
                foreach(var span in spans){float x=Mathf.Clamp(edge.x,span.Min,span.Max),distance=Mathf.Abs(x-edge.x);if(distance<nearest){nearest=distance;joinX=x;}}
                if(nearest>.12f || !Fs20TVaporPath.Height(neighbor,triangles,joinX,pz,out float y,out _) || Mathf.Abs(y-edge.y)>.16f)continue;
                for(int x=0;x<map.width;x++){
                    float px=Mathf.Lerp(shape.VolumeMin.x,shape.VolumeMax.x,(x+.5f)/map.width);
                    pixels[z*map.width+x].g=1f-Mathf.SmoothStep(0,1,Mathf.Abs(px-edge.x)/.5f);
                }
            }
            map.SetPixels(pixels);map.Apply(false,false);
        }

        private void RenderVolume(Shape shape,float[] profile,Vector3 worldFlow,float strength,float visibility,int damageState,Camera camera,Vector3 transport,Transform aircraftRoot)
        {
            if(state!=damageState){state=damageState;mesh.Clear();mesh.vertices=positions;mesh.SetTriangles(BoxTriangles,0);TriangleCount=12;}
            var matrix=Renderer.transform.worldToLocalMatrix*layer.Frame.localToWorldMatrix*shape.GridToFrame;
            for(int i=0;i<8;i++)positions[i]=matrix.MultiplyPoint3x4(new Vector3((i&1)==0?shape.VolumeMin.x:shape.VolumeMax.x,(i&2)==0?shape.VolumeMin.y:shape.VolumeMax.y,(i&4)==0?shape.VolumeMin.z:shape.VolumeMax.z));
            mesh.SetVertices(positions,0,8);mesh.RecalculateBounds();
            PeakAlpha=0f;for(int i=0;i<16;i++){float opacity=profile[i]*visibility;pressureVectors[i/4][i%4]=opacity;PeakAlpha=Mathf.Max(PeakAlpha,opacity);}
            FlowState=shape.FrameToGrid.MultiplyVector(layer.Frame.InverseTransformDirection(worldFlow));TextureOffset=transport;
            float incidence=FlowState.magnitude<1?0f:Mathf.Clamp01(-FlowState.normalized.y);
            properties.SetTexture("_SkinMap",shape.VolumeSkin);properties.SetMatrix("_GridFromWorld",shape.FrameToGrid*layer.Frame.worldToLocalMatrix);
            properties.SetMatrix("_NoiseFromWorld",aircraftRoot.worldToLocalMatrix);
            properties.SetVector("_GridMin",shape.VolumeMin);properties.SetVector("_GridMax",shape.VolumeMax);
            properties.SetVectorArray("_Profiles",pressureVectors);properties.SetVector("_Transport",transport);
            properties.SetVector("_Envelope",new Vector4(strength,incidence,damageState,layer.JoinMix));
            var direction=FlowState.normalized;
            properties.SetVector("_Flow",new Vector4(direction.x,direction.y,direction.z,layer.Role=="tip"?1f:0f));Renderer.SetPropertyBlock(properties);
            Renderer.enabled=PeakAlpha>.001f;
        }
    }
}
