using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Japensou.NuclearOption.VortexConventional
{
    // Cached upper-skin volumes. Native loads drive extinction; presentation
    // updates small property blocks without a particle birth/death cycle.
    internal sealed partial class Fs20VaporSheet
    {
        [Serializable] internal sealed class Shape
        {
            public Vector3[] Points, Normals;
            public Vector3[] RowSides;
            public Vector2[] Uv;
            public float[] Chord, Edge, Fade, Peel, Join;
            public int Rows, Strips;
            public float Length;
            public Texture2D VolumeSkin;
            public Vector3 VolumeMin,VolumeMax;
            public Matrix4x4 GridToFrame,FrameToGrid;
        }
        private const int Rows=33, Across=9;
        private readonly Fs20TVapor.Layer layer;
        private readonly Mesh mesh;
        internal readonly MeshRenderer Renderer;
        private readonly Vector3[] positions;
        private int state=-1;
        private static AssetBundle shaderBundle;
        private static Shader vaporShader;
        internal static Shader VaporShader {
            get {
                if(vaporShader!=null)return vaporShader;
                using(var source=typeof(Fs20VaporSheet).Assembly.GetManifestResourceStream("Fs20Vapor.bundle")){
                    if(source==null)throw new InvalidOperationException("FS20 surface vapor shader resource missing.");
                    var bytes=new byte[source.Length];int offset=0,read;
                    while(offset<bytes.Length && (read=source.Read(bytes,offset,bytes.Length-offset))>0)offset+=read;
                    shaderBundle=AssetBundle.LoadFromMemory(bytes);
                }
                if(shaderBundle==null)throw new InvalidOperationException("FS20 surface vapor shader bundle could not load.");
                vaporShader=shaderBundle.LoadAsset<Shader>("Assets/FS20Validation/Fs20SurfaceMist.shader");
                if(vaporShader==null || !vaporShader.isSupported)throw new InvalidOperationException("FS20 surface vapor shader is unsupported.");
                return vaporShader;
            }
        }
        internal int TriangleCount { get; private set; }
        internal float PeakAlpha { get; private set; }
        internal Mesh Mesh => mesh;

        internal static Shape[] Build(Mesh[] skins,Transform frame,Transform root)
        {
            var result=new Shape[skins.Length];
            for(int stage=0;stage<skins.Length;stage++){
                const int strips=1;int count=Rows*Across;
                var shape=new Shape {Points=new Vector3[count],Normals=new Vector3[count],Uv=new Vector2[count],
                    Chord=new float[count],Edge=new float[count],Fade=new float[count],Peel=new float[count],RowSides=new Vector3[Rows],Rows=Rows,Strips=strips};
                var skin=skins[stage];var localSkin=skin.vertices;
                var rootSkin=new Vector3[localSkin.Length];float front=float.NegativeInfinity,rear=float.PositiveInfinity;
                for(int i=0;i<rootSkin.Length;i++){
                    rootSkin[i]=root.InverseTransformPoint(frame.TransformPoint(localSkin[i]));
                    front=Mathf.Max(front,rootSkin[i].z);rear=Mathf.Min(rear,rootSkin[i].z);
                }
                shape.Length=Mathf.Max(.01f,front-rear);
                var indices=skin.triangles;var valid=new bool[count];
                for(int row=0;row<Rows;row++){
                    float chord=row/(float)(Rows-1),z=Mathf.Lerp(front,rear,Mathf.Lerp(.005f,.995f,chord));
                    var spans=Fs20TVaporPath.Slice(rootSkin,indices,z);
                    if(spans.Count==0)continue;
                    float min=spans[0].Min,max=spans[spans.Count-1].Max;
                    shape.RowSides[row]=frame.InverseTransformDirection(root.right).normalized;
                    for(int cross=0;cross<Across;cross++){
                        int index=row*Across+cross;float across=cross/(float)(Across-1),offset=across*2f-1f;
                        float x=Mathf.Lerp(min,max,Mathf.Lerp(.01f,.99f,across));
                        bool inside=Fs20TVaporPath.Height(rootSkin,indices,x,z,out float y,out var n);
                        shape.Points[index]=frame.InverseTransformPoint(root.TransformPoint(new Vector3(x,inside?y:0f,z)));
                        shape.Normals[index]=frame.InverseTransformDirection(root.TransformDirection(n)).normalized;
                        shape.Uv[index]=new Vector2(across,(front-z)*.6f);
                        shape.Chord[index]=chord;shape.Edge[index]=inside?Mathf.Pow(Mathf.Max(0f,1f-offset*offset),1.5f):0f;
                        shape.Fade[index]=Mathf.SmoothStep(0f,1f,chord*12f)*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.82f,1f,chord)));
                        shape.Peel[index]=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.8f,1f,chord));valid[index]=inside;
                    }
                }
                BuildVolumeSkin(shape,rootSkin,indices,frame,root);result[stage]=shape;
            }
            return result;
        }

        internal static void Join(Fs20TVapor.Layer layer,Fs20TVapor.Layer neighbor,Transform root,bool outboard)
        {
            var skin=neighbor.Surfaces[0];var vertices=skin.vertices;
            for(int i=0;i<vertices.Length;i++)vertices[i]=root.InverseTransformPoint(neighbor.Frame.TransformPoint(vertices[i]));
            var indices=skin.triangles;
            for(int state=0;state<layer.SheetShapes.Length;state++){
                var shape=layer.SheetShapes[state];shape.Join=new float[shape.Points.Length];
                // A join is authored only on intact skin. All damaged topology
                // continues using its original clipped, feathered boundaries.
                if(state!=0)continue;
                bool left=layer.Left;
                int border=(outboard==left)?0:Across-1;
                for(int row=0;row<Rows;row++){
                    var edge=root.InverseTransformPoint(layer.Frame.TransformPoint(shape.Points[row*Across+border]));
                    var spans=Fs20TVaporPath.Slice(vertices,indices,edge.z);
                    float closest=float.PositiveInfinity,joinX=0f;
                    foreach(var span in spans){
                        float x=Mathf.Clamp(edge.x,span.Min,span.Max),distance=Mathf.Abs(x-edge.x);
                        if(distance<closest){closest=distance;joinX=x;}
                    }
                    if(closest>.12f || !Fs20TVaporPath.Height(vertices,indices,joinX,edge.z,out float y,out _) || Mathf.Abs(y-edge.y)>.16f)continue;
                    for(int cross=0;cross<Across;cross++){
                        int i=row*Across+cross;var p=root.InverseTransformPoint(layer.Frame.TransformPoint(shape.Points[i]));
                        float distance=Mathf.Abs(p.x-edge.x);
                        shape.Join[i]=.72f*(1f-Mathf.SmoothStep(0f,1f,distance/.45f));
                    }
                }
                // Use the same physical texture coordinates on the two skins.
                for(int i=0;i<shape.Points.Length;i++){
                    var p=root.InverseTransformPoint(layer.Frame.TransformPoint(shape.Points[i]));
                    shape.Uv[i]=new Vector2(p.x*.6f,-p.z*.6f);
                }
                JoinVolumeSkin(shape,vertices,indices,layer.Frame,root,border);
            }
        }

        internal Fs20VaporSheet(Fs20TVapor.Layer source)
        {
            layer=source;
            positions=new Vector3[8];
            var inherited=source.Particles.transform.Find("FS20 continuous vapor");
            if(inherited!=null){inherited.gameObject.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(inherited.gameObject);else UnityEngine.Object.DestroyImmediate(inherited.gameObject);}
            var visual=new GameObject("FS20 continuous vapor"){layer=source.Particles.gameObject.layer};
            visual.transform.SetParent(source.Particles.transform,false);
            mesh=new Mesh{name="FS20 surface flow "+source.Key};mesh.MarkDynamic();
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;Renderer=visual.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial=source.Particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            Renderer.shadowCastingMode=ShadowCastingMode.Off;Renderer.receiveShadows=false;
            Renderer.lightProbeUsage=LightProbeUsage.Off;Renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            Renderer.enabled=false;
        }
        internal void Render(float[] profile,Vector3 worldFlow,float strength,float visibility,float dt,int damageState,Camera camera,Vector3 transport,Transform aircraftRoot)
        {
            if(layer.Frame==null || damageState<0 || damageState>=Fs20TDamagePolicy.Lost){Hide();return;}
            int next=Mathf.Min(damageState,layer.SheetShapes.Length-1);
            var shape=layer.SheetShapes[next];
            RenderVolume(shape,profile,worldFlow,strength,visibility,damageState,camera,transport,aircraftRoot);
        }
        internal void Hide(){Renderer.enabled=false;PeakAlpha=0f;}
        internal void Dispose(){
            if(Application.isPlaying){if(Renderer!=null)UnityEngine.Object.Destroy(Renderer.gameObject);if(mesh!=null)UnityEngine.Object.Destroy(mesh);}
            else{if(Renderer!=null)UnityEngine.Object.DestroyImmediate(Renderer.gameObject);if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
