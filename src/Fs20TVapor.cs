using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    // Ternion-inspired overlapping shoulders/wing sheets, fitted to the T's own skin.
    // Particle material comes from the stock FS-20; no Ternion assets are bundled.
    public sealed partial class Fs20TVapor : MonoBehaviour
    {
        [Serializable] internal sealed class Layer
        {
            public string Key;
            public AeroPart Owner;
            public AeroPart SourceOwner;
            public Transform Frame;
            public bool Auxiliary;
            public string Role;
            public UnitPart JoinPart;
            public UnitPart[] JoinChain;
            public UnitPart[] AttachmentChain;
            public ParticleSystem Particles;
            public Mesh[] Surfaces;
            public Fs20VaporSheet.Shape[] SheetShapes;
            public float[] Fractions;
            public float Threshold, Rate, Opacity;
            public float LifetimeMin, LifetimeMax;
            public Vector3 FlowOrigin;
            public float FlowLength;
            public Fs20TSlat Slat;
            public HighLiftDevice NativeSlat;
            public UnitPart[] SlatChain;
            public bool Left;
            public int UpstreamIndex = -1;
            public Vector3 FlowEntry, FlowExit;
            [NonSerialized] internal int LastState = -1;
            [NonSerialized] internal float Strength;
            [NonSerialized] internal float Lift, Area, LoadTime = float.NegativeInfinity;
            [NonSerialized] internal int LoadCount, OwnerId;
            [NonSerialized] internal readonly float[] Profile = new float[16];
            [NonSerialized] internal float Span, Visibility;
            [NonSerialized] internal bool CollectLoads = true;
            [NonSerialized] internal Vector3 PointFlow;
            [NonSerialized] internal float RenderStrength;
            [NonSerialized] internal float Demand;
            [NonSerialized] internal bool Emitting;
            [NonSerialized] internal readonly float[] RenderProfile = new float[16];
            [NonSerialized] internal Fs20VaporSheet Sheet;
            [NonSerialized] internal float JoinMix;
        }
        [SerializeField] private Layer[] layers;
        private sealed class Template { internal Layer[] Layers; internal Transform Root; }
        private static readonly Dictionary<string,Template> Templates=new Dictionary<string,Template>();
        [SerializeField] private string family=Fs20TAirframe.Key;
        private Aircraft aircraft;
        private bool bindingsInitialized;
        private bool runtimeReady;
        private int runtimeReports;
        private float nextRuntimeReport;
        private Camera viewCamera;
        private float nextCameraCheck;
        private float visualTime;
        private Vector3 transport;
        private float quality = 1f;
        private bool loadsRegistered;
        private Fs20TVaporEnvelope.Atmosphere atmosphere;
        private float leftTip, rightTip;
        private int freshLoads;
        private bool culled;
        private float maxPressureDrop, maxCooling;
        private static readonly float[] ChordWeights=Enumerable.Range(0,16).Select(i=>Fs20TVaporEnvelope.ChordWeight((i+.5f)/16f)).ToArray();
        private Tip[] tips;
        private sealed class Tip { internal VaporEmitter Emitter; internal Transform[] Origins; internal UnitPart[] Owners; internal UnitPart[][] Chains; internal float Carry; }
        private static readonly FieldInfo Emitters = Fs20Tuning.Field(typeof(VaporEffect), "emitters");
        private static readonly FieldInfo Health = Fs20Tuning.Field(typeof(UnitPart), "hitPoints");
        private static readonly FieldInfo Fragmented = Fs20Tuning.Field(typeof(UnitPart), "fragmented");
        private static readonly FieldInfo Wind = Fs20Tuning.Field(typeof(Aircraft), "windVelocity");
        private static readonly FieldInfo Cockpit = Fs20Tuning.Field(typeof(Aircraft), "cockpit");

        internal static void Install(Transform root)
        {
            if (root.GetComponent<Fs20TVapor>() != null) return;
            var native = root.GetComponent<VaporEffect>();
            if (native == null) throw new InvalidOperationException("FS-20T native vapor effect missing.");
            var emitters = (VaporEmitter[])Emitters.GetValue(native);
            var sheets = emitters.Where(e => e.particles != null && e.particles.name.StartsWith("wingVapor_", StringComparison.Ordinal)).ToArray();
            if (sheets.Length != 2) throw new InvalidOperationException("Expected two FS-20T wing vapor donors.");
            var asset = Fs20TAsset.Read();
            var result = new List<Layer>();
            var material = new Material(sheets[0].particles.GetComponent<ParticleSystemRenderer>().sharedMaterial) { name = "FS20T conformal vapor" };
            SetContactFade(material);
            material.DisableKeyword("_NORMALMAP");
            try
            {
                foreach (string side in new[] { "L", "R" })
                {
                    var donor = sheets.Single(s => s.particles.name.EndsWith("_" + side, StringComparison.Ordinal)).particles;
                    var shoulder = root.Find("liftFan/wingRoot_" + side);
                    var shoulderPart = shoulder.GetComponent<AeroPart>();
                    var source = Fs20Geometry.ReadableCopy(shoulder.GetComponent<MeshFilter>().sharedMesh);
                    try
                    {
                        result.Add(Create(root, shoulderPart, donor, material, "root_" + side,
                            new[] { Skin(source.vertices, source.GetTriangles(0), shoulder.localToWorldMatrix, shoulderPart.transform, root) },
                            new[] { 1f }, 3f, 65f, .38f));
                    }
                    finally { Release(source); }
                    // wing1a's external skin belongs to the independent delta sections.
                    // The retained outboard skin is on wing2a (including the VTOL post owner).
                    var wing = asset.Pieces.Single(p => p.Name == "wing2a_" + side);
                    var wingPart = root.Find(wing.Owner).GetComponent<AeroPart>();
                    result.Add(Create(root, wingPart, donor, material, "main_" + side,
                        new[] { Skin(wing.Lods[0].Vertices, wing.Lods[0].Indices[0], root.localToWorldMatrix, wingPart.transform, root) },
                        new[] { 1f }, 4f, 90f, .4f));
                    foreach (var region in asset.Regions.Where(r => r.Id.StartsWith(side + "_", StringComparison.Ordinal)))
                    {
                        var owner = root.GetComponentsInChildren<Fs20TDamageState>(true)
                            .Single(s => s.name == "FS20T region " + region.Id).GetComponent<AeroPart>();
                        var surfaces = region.States.Select(state =>
                        {
                            var p = asset.Pieces.Single(piece => piece.Name == state.Piece);
                            return Skin(p.Lods[0].Vertices, p.Lods[0].Indices[0], root.localToWorldMatrix, owner.transform, root);
                        }).ToArray();
                        bool forward = region.Id.EndsWith("fillet", StringComparison.Ordinal) || region.Id.EndsWith("transition", StringComparison.Ordinal);
                        result.Add(Create(root, owner, donor, material, region.Id, surfaces, region.States.Select(s => s.AreaFraction).ToArray(),
                            forward ? 3.5f : 5.5f, forward ? 55f : 75f, forward ? .34f : .4f));
                    }
                }
                AddTips(root,result,material,sheets[0].particles);
                AddAuxiliary(root,result,material,sheets[0].particles);
                AddTrailingCoverage(root,result,material,sheets[0].particles);
                ShareVaporFields(result);
                FitWingChords(root,result);
                foreach(var layer in result)foreach(var shape in layer.SheetShapes)shape.VolumeSkin.Apply(false,true);
                var controller = root.gameObject.AddComponent<Fs20TVapor>();
                controller.layers = result.ToArray();
                controller.family=Fs20TAirframe.Key;
                Templates[controller.family]=new Template{Layers=controller.layers,Root=root};
                controller.aircraft = root.GetComponent<Aircraft>();
                // The stock transonic cone is a detached volume, not wing-hugging
                // condensation. It obscures the T's surface streams in flight.
                var replaced = sheets.Concat(emitters.Where(e => e.particles != null && e.particles.name == "vaporCone")).ToArray();
                Emitters.SetValue(native, emitters.Except(replaced).ToArray());
                foreach (var sheet in replaced)
                {
                    sheet.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var emission = sheet.particles.emission; emission.enabled = false;
                    var main = sheet.particles.main; main.playOnAwake = false;
                    sheet.particles.gameObject.SetActive(false);
                }
                Fs20VaporDriver.FitTips(root,native);
                VortexConventionalPlugin.ReportAssetState("FS-20T vapor: "+result.Count+" continuous surface layers, including moving slats and body shoulders; native tip trails and damage retained.");
            }
            catch
            {
                foreach (var layer in result)
                {
                    if (layer.Particles != null) Release(layer.Particles.gameObject);
                    foreach (var mesh in layer.Surfaces) if (mesh != null) Release(mesh);
                }
                Release(material);
                throw;
            }
        }

        internal static void InstallConventional(Transform root)
        {
            if(root.GetComponent<Fs20TVapor>()!=null)return;
            var native=root.GetComponent<VaporEffect>();
            var emitters=(VaporEmitter[])Emitters.GetValue(native);
            var donors=emitters.Where(e=>e.particles!=null && e.particles.name.StartsWith("wingVapor_")).ToArray();
            if(donors.Length!=2)throw new InvalidOperationException("FS-20C vapor donors missing.");
            var material=new Material(donors[0].particles.GetComponent<ParticleSystemRenderer>().sharedMaterial){name="FS20C conformal vapor"};
            SetContactFade(material);material.DisableKeyword("_NORMALMAP");
            var result=new List<Layer>();
            foreach(string side in new[]{"L","R"})
                foreach(string path in new[]{"liftFan/wingRoot_"+side,"wing1a_"+side,"wing1a_"+side+"/wing2a_"+side})
                {
                    var part=root.Find(path).GetComponent<AeroPart>();
                    var source=Fs20Geometry.ReadableCopy(part.GetComponent<MeshFilter>().sharedMesh);
                    try{
                        var layer=Create(root,part,donors.Single(e=>e.particles.name.EndsWith("_"+side)).particles,material,path.Replace('/','_'),
                            new[]{Skin(source.vertices,source.GetTriangles(0),part.transform.localToWorldMatrix,part.transform,root)},new[]{1f},3f,65f,.34f);
                        layer.Particles.name=layer.Particles.name.Replace("FS20T","FS20C");result.Add(layer);
                    }finally{Release(source);}
                }
            AddTips(root,result,material,donors[0].particles);
            AddAuxiliary(root,result,material,donors[0].particles);
            AddTrailingCoverage(root,result,material,donors[0].particles);
            ShareVaporFields(result);
            FitWingChords(root,result);
            foreach(var layer in result)foreach(var shape in layer.SheetShapes)shape.VolumeSkin.Apply(false,true);
            var controller=root.gameObject.AddComponent<Fs20TVapor>();controller.family=Fs20VaporDriver.CKey;
            controller.layers=result.ToArray();controller.aircraft=root.GetComponent<Aircraft>();
            Templates[controller.family]=new Template{Layers=controller.layers,Root=root};
            var removed=emitters.Where(e=>donors.Contains(e) || (e.particles!=null && e.particles.name=="vaporCone")).ToArray();
            Emitters.SetValue(native,emitters.Except(removed).ToArray());
            foreach(var e in removed){e.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=e.particles.main;main.playOnAwake=false;var emission=e.particles.emission;emission.enabled=false;e.particles.gameObject.SetActive(false);}
            Fs20VaporDriver.FitTips(root,native);
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private static void ShareVaporFields(List<Layer> layers)
        {
            // Pieces driven by the same native load should not restart their
            // chordwise condensate field at each visual ownership boundary.
            foreach(var group in layers.Where(l=>l.Role!="slat").GroupBy(l=>new{l.SourceOwner,Body=l.Role=="body"})){
                if(group.Count()<2)continue;
                float front=group.Max(l=>l.SheetShapes[0].VolumeMax.z-.24f),rear=group.Min(l=>l.SheetShapes[0].VolumeMin.z+.24f);
                foreach(var layer in group){
                    layer.FlowLength=front-rear;
                    foreach(var shape in layer.SheetShapes){
                        var map=shape.VolumeSkin;var pixels=map.GetPixels();
                        for(int z=0;z<map.height;z++){
                            float pz=Mathf.Lerp(shape.VolumeMin.z,shape.VolumeMax.z,(z+.5f)/map.height),chord=Mathf.InverseLerp(front,rear,pz);
                            for(int x=0;x<map.width;x++)pixels[z*map.width+x].b=chord;
                        }
                        map.SetPixels(pixels);map.Apply(false,false);
                    }
                }
            }
        }
        private static void SetContactFade(Material material)
        {
            material.shader=Fs20VaporSheet.VaporShader;
            material.shaderKeywords=Array.Empty<string>();
            material.SetTexture("_NoiseTex",Fs20VaporSheet.VolumeNoise);
            material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
        }

        private static void AddTips(Transform root,List<Layer> layers,Material material,ParticleSystem donor)
        {
            foreach(string side in new[]{"L","R"}){
                var wing=root.Find("wing1a_"+side+"/wing2a_"+side).GetComponent<AeroPart>();
                var tip=wing.transform.Find("wingtip_"+side).GetComponent<AeroPart>();
                var main=layers.Single(l=>l.Owner==wing && !l.Auxiliary);
                var mesh=Fs20Geometry.ReadableCopy(tip.GetComponent<MeshFilter>().sharedMesh);
                try{
                    var layer=Create(root,tip,donor,material,"tip_"+side,
                        new[]{Skin(mesh.vertices,mesh.GetTriangles(0),tip.transform.localToWorldMatrix,tip.transform,root)},new[]{1f},0,0,0);
                    // The native outboard part supplies pressure for both sides
                    // of this visual seam. Auxiliary coverage adds no lift twice.
                    layer.SourceOwner=wing;layer.Auxiliary=true;layer.Role="tip";
                    main.JoinPart=tip;layer.JoinPart=wing;
                    main.JoinChain=tip.GetComponentsInParent<UnitPart>(true);
                    layer.JoinChain=wing.GetComponentsInParent<UnitPart>(true);
                    Fs20VaporSheet.Join(main,layer,root,true);
                    Fs20VaporSheet.Join(layer,main,root,false);
                    layers.Add(layer);
                }finally{Release(mesh);}
            }
        }

        private static void AddAuxiliary(Transform root,List<Layer> layers,Material material,ParticleSystem donor)
        {
            var hinges=root.GetComponentsInChildren<Fs20TSlat>(true).Select(s=>s.transform)
                .Concat(root.GetComponentsInChildren<Fs20SlatHinge>(true).Select(s=>s.transform)).Distinct().ToArray();
            foreach(var hinge in hinges){
                var filter=hinge.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.sharedMesh!=null &&
                    f.name.IndexOf("slat",StringComparison.OrdinalIgnoreCase)>=0 && f.name.IndexOf("LOD",StringComparison.OrdinalIgnoreCase)<0 &&
                    f.GetComponent<MeshRenderer>()!=null && f.GetComponent<MeshRenderer>().enabled);
                if(filter==null)throw new InvalidOperationException("Moving slat skin missing: "+hinge.name);
                var part=filter.GetComponentInParent<AeroPart>(true);var frame=filter.transform;
                var mesh=Fs20Geometry.ReadableCopy(filter.sharedMesh);
                try{
                    var layer=Create(root,part,donor,material,"slat_"+hinge.name,new[]{Skin(mesh.vertices,mesh.GetTriangles(0),frame.localToWorldMatrix,frame,root)},new[]{1f},0,0,0,frame);
                    layer.Auxiliary=true;layer.Role="slat";layer.Slat=hinge.GetComponent<Fs20TSlat>();layers.Add(layer);
                }finally{Release(mesh);}
            }
            foreach(string side in new[]{"L","R"})foreach(string path in new[]{"","liftFan/cockpit"}){
                var frame=path.Length==0?root:root.Find(path);var part=frame.GetComponent<AeroPart>();
                var filter=frame.GetComponent<MeshFilter>();var mesh=Fs20Geometry.ReadableCopy(filter.sharedMesh);
                try{
                    var points=new List<Vector3>();var triangles=new List<int>();var vertices=mesh.vertices;var indices=mesh.GetTriangles(0);
                    float sign=side=="L"?-1f:1f;
                    for(int i=0;i<indices.Length;i+=3){
                        var polygon=new List<Vector3>();
                        for(int j=0;j<3;j++)polygon.Add(root.InverseTransformPoint(frame.TransformPoint(vertices[indices[i+j]])));
                        var clipped=new List<Vector3>();
                        for(int j=0;j<polygon.Count;j++){
                            var a=polygon[j];var b=polygon[(j+1)%polygon.Count];float da=a.x*sign-.68f,db=b.x*sign-.68f;
                            if(da>=0f)clipped.Add(a);
                            if((da>=0f)!=(db>=0f))clipped.Add(Vector3.Lerp(a,b,da/(da-db)));
                        }
                        for(int j=1;j<clipped.Count-1;j++)foreach(var p in new[]{clipped[0],clipped[j],clipped[j+1]}){triangles.Add(points.Count);points.Add(p);}
                    }
                    if(triangles.Count==0)continue;
                    var layer=Create(root,part,donor,material,"body_"+(path.Length==0?"aft":"forward")+"_"+side,
                        new[]{Skin(points.ToArray(),triangles.ToArray(),root.localToWorldMatrix,frame,root,.15f)},new[]{1f},0,0,0,frame);
                    layer.SourceOwner=root.Find("liftFan/wingRoot_"+side).GetComponent<AeroPart>();
                    layer.Auxiliary=true;layer.Role="body";layer.Left=side=="L";layers.Add(layer);
                }finally{Release(mesh);}
            }
        }

        private static Mesh Skin(Vector3[] vertices, int[] triangles, Matrix4x4 sourceToWorld, Transform owner, Transform root,float minimumUp=.35f)
        {
            var points = new List<Vector3>();
            var indices = new List<int>();
            var skin = new List<Vector3[]>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = sourceToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b = sourceToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                Vector3 c = sourceToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                // Omit the underside, cut walls and internal framing; emit above the actual skin.
                if (Vector3.Dot(normal.normalized, root.up) < minimumUp || normal.sqrMagnitude < 1e-10f) continue;
                skin.Add(new[] { a, b, c });
            }
            foreach (var triangle in skin)
                foreach (var point in triangle)
                {
                    indices.Add(points.Count);
                    points.Add(owner.InverseTransformPoint(point + root.up * .055f));
                }
            if (points.Count == 0) throw new InvalidOperationException("FS-20T vapor surface has no upper skin: " + owner.name);
            var mesh = new Mesh { name = "FS20T vapor upper skin", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(points); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Layer Create(Transform root, AeroPart owner, ParticleSystem donor, Material material, string key,
            Mesh[] surfaces, float[] fractions, float threshold, float rate, float opacity,Transform frame=null)
        {
            if(frame==null)frame=owner.transform;
            var ps = Instantiate(donor, frame, false);
            ps.name = "FS20T condensation " + key;
            ps.gameObject.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.localPosition = Vector3.zero;
            ps.transform.localRotation = Quaternion.identity;
            ps.transform.localScale = Vector3.one;
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None; main.playOnAwake = false;
            main.loop = true; main.prewarm = false; main.startDelay = 0f; main.simulationSpeed = 1f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Local;
            main.startSpeed = 0f; main.gravityModifier = 0f; main.startSize3D = true; main.startRotation3D = true; main.maxParticles = 64;
            main.startSize=.7f;
            main.startRotationX=0f;main.startRotationY=0f;main.startRotationZ=0f;
            bool rootFlow = key.Contains("root") || key.Contains("fillet") || key.Contains("transition");
            float lifeMin = rootFlow ? .55f : .65f, lifeMax = rootFlow ? .75f : .9f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSizeX = .3f; main.startSizeY = .7f; main.startSizeZ = .012f;
            main.startColor = new Color(1f, 1f, 1f, 0f);
            var shape = ps.shape;
            shape.enabled = false; shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle; shape.mesh = surfaces[0];
            shape.position = Vector3.zero; shape.rotation = Vector3.zero; shape.scale = Vector3.one;
            shape.randomDirectionAmount = 0f; shape.sphericalDirectionAmount = 0f; shape.randomPositionAmount = 0f;
            var emission = ps.emission;
            emission.enabled = false; emission.rateOverTime = 0f; emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = false; sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = 4; sheet.numTilesY = 4;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f,1f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.cycleCount = 1;
            var noise = ps.noise; noise.enabled = false;
            var force = ps.forceOverLifetime; force.enabled = false;
            var rotation = ps.rotationOverLifetime; rotation.enabled = false;
            var rotationSpeed = ps.rotationBySpeed; rotationSpeed.enabled = false;
            var sizeSpeed = ps.sizeBySpeed; sizeSpeed.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true; size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .8f, 1f, 1.05f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = false;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.sharedMaterial = material;
            renderer.enableGPUInstancing = false;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            var color = ps.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.7f, .1f), new GradientAlphaKey(.55f, .7f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            bool left = root.InverseTransformPoint(owner.transform.position).x < 0f;
            var slat = root.GetComponentsInChildren<Fs20TSlat>(true).Where(s => (root.InverseTransformPoint(s.transform.position).x < 0f) == left)
                .OrderBy(s => Vector3.SqrMagnitude(s.transform.position-owner.transform.position)).FirstOrDefault();
            var nativeSlat = root.GetComponentsInChildren<HighLiftDevice>(true).Where(s => (root.InverseTransformPoint(s.transform.position).x < 0f) == left)
                .OrderBy(s => Vector3.SqrMagnitude(s.transform.position-owner.transform.position)).FirstOrDefault();
            var slatTransform = slat != null ? slat.transform : nativeSlat != null ? nativeSlat.transform : null;
            var shapes=Fs20VaporSheet.Build(surfaces,frame,root);
            return new Layer { Key = key, Owner = owner,SourceOwner=owner,Frame=frame,Role="wing",AttachmentChain = owner.GetComponentsInParent<UnitPart>(true),
                Particles = ps, Surfaces = surfaces,SheetShapes=shapes, Fractions = fractions,
                Threshold = threshold, Rate = rate * 1.3f, Opacity = opacity*1.1f,
                LifetimeMin = lifeMin, LifetimeMax = lifeMax,
                FlowOrigin = surfaces[0].bounds.center,FlowLength=shapes[0].Length, Slat = slat, NativeSlat = nativeSlat,
                SlatChain = slatTransform == null ? Array.Empty<UnitPart>() : slatTransform.GetComponentsInParent<UnitPart>(true), Left = left };
        }

        private void Awake() { aircraft = GetComponent<Aircraft>(); tips = null; }
        internal bool BindBeforePhysics() => EnsureBindings();
        private bool EnsureBindings()
        {
            if(aircraft==null)aircraft=GetComponent<Aircraft>();
            string key=Fs20VaporDriver.Family(aircraft);if(key!=null)family=key;
            Templates.TryGetValue(family,out var template);
            if(layers==null || (template!=null && layers.Length!=template.Layers.Length) || layers.Any(l=>l==null || l.SheetShapes==null || l.SheetShapes.Any(p=>p==null || p.Points==null || p.RowSides==null || p.VolumeSkin==null || p.GridToFrame.determinant==0f)) ||
                (!bindingsInitialized && layers.Any(l=>l.Owner==null || l.SourceOwner==null || l.Frame==null || l.Particles==null)))
            {
                if(template==null || template.Root==null)return false;
                layers=template.Layers.Select(source=>{
                    Transform Bound(Transform t){string path=RelativePath(template.Root,t);return path.Length==0?transform:transform.Find(path);}
                    var owner=Bound(source.Owner.transform).GetComponent<AeroPart>();
                    var particles=transform.Find(RelativePath(template.Root,source.Particles.transform)).GetComponent<ParticleSystem>();
                    var slat = source.Slat==null ? null : transform.Find(RelativePath(template.Root,source.Slat.transform)).GetComponent<Fs20TSlat>();
                    var nativeSlat = source.NativeSlat==null ? null : transform.Find(RelativePath(template.Root,source.NativeSlat.transform)).GetComponent<HighLiftDevice>();
                    var slatTransform = slat!=null ? slat.transform : nativeSlat!=null ? nativeSlat.transform : null;
                    return new Layer{Key=source.Key,Owner=owner,SourceOwner=Bound(source.SourceOwner.transform).GetComponent<AeroPart>(),Frame=Bound(source.Frame),Auxiliary=source.Auxiliary,Role=source.Role,
                        JoinPart=source.JoinPart==null?null:Bound(source.JoinPart.transform).GetComponent<UnitPart>(),
                        JoinChain=source.JoinPart==null?Array.Empty<UnitPart>():Bound(source.JoinPart.transform).GetComponentsInParent<UnitPart>(true),
                        Particles=particles,AttachmentChain=owner.GetComponentsInParent<UnitPart>(true),
                        Surfaces=source.Surfaces,SheetShapes=source.SheetShapes,Fractions=source.Fractions,Threshold=source.Threshold,Rate=source.Rate,Opacity=source.Opacity,
                        LifetimeMin=source.LifetimeMin,LifetimeMax=source.LifetimeMax,FlowOrigin=source.FlowOrigin,FlowLength=source.FlowLength,Slat=slat,NativeSlat=nativeSlat,
                        SlatChain=slatTransform==null ? Array.Empty<UnitPart>() : slatTransform.GetComponentsInParent<UnitPart>(true),Left=source.Left,
                        UpstreamIndex=source.UpstreamIndex,FlowEntry=source.FlowEntry,FlowExit=source.FlowExit};
                }).ToArray();
                tips=null;
            }
            if(!loadsRegistered){
                foreach(var layer in layers){
                    var bounds=layer.Surfaces[0].bounds;
                    float span=0f;
                    for(int corner=0;corner<8;corner++){
                        var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        span=Mathf.Max(span,Mathf.Abs(transform.InverseTransformPoint(layer.Frame.TransformPoint(point)).x));
                    }
                    layer.Span=span;
                    layer.Sheet=new Fs20VaporSheet(layer);
                    Fs20VaporDriver.Register(layer);
                }
                loadsRegistered=true;
            }
            bindingsInitialized=true;
            return aircraft!=null;
        }
        private static string RelativePath(Transform root,Transform child)
        {
            var path=new Stack<string>();
            while(child!=root){if(child==null)throw new InvalidOperationException("FS-20T vapor owner is outside its template.");path.Push(child.name);child=child.parent;}
            return string.Join("/",path.ToArray());
        }
        private Rigidbody ResolveBody()
        {
            if(aircraft==null)return null;
            var cockpit=(UnitPart)Cockpit.GetValue(aircraft);
            return cockpit!=null && cockpit.rb!=null ? cockpit.rb : aircraft.rb;
        }
        internal void NativeTick(float dt)
        {
            if (!EnsureBindings()) { RuntimeReport("waiting: aircraft="+(aircraft!=null)+" layers="+(layers==null?0:layers.Length)); StopLayers(); return; }
            var body = ResolveBody();
            if(body==null){RuntimeReport("waiting: root body and cockpit body missing");StopLayers();return;}
            // Native simulation owns binding and pressure sampling; presentation
            // runs each rendered frame rather than snapping at the physics cadence.
            if(!enabled)enabled=true;
            if(!runtimeReady){runtimeReady=true;runtimeReports=0;nextRuntimeReport=0f;VortexConventionalPlugin.ReportAssetState(RuntimeLabel+" vapor runtime bound: layers="+layers.Length+", rootBody="+(aircraft.rb!=null)+", selectedBody="+body.name);}
            Vector3 airflow = body.velocity - (Vector3)Wind.GetValue(aircraft);
            // Aircraft.accel is inertial acceleration in g, without gravity compensation.
            float normalG = Vector3.Dot(aircraft.accel - Physics.gravity / 9.81f, body.rotation*Vector3.up);
            if(Time.unscaledTime>=nextCameraCheck){nextCameraCheck=Time.unscaledTime+.5f;viewCamera=Camera.main;}
            float distance=viewCamera==null ? 0f : Vector3.Distance(viewCamera.transform.position,body.position);
            quality=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(200f,1200f,distance));
            if(quality<=.001f || aircraft.displayDetail<=.4f){
                if(!culled){
                    GuardTips();StopLayers();
                    foreach(var layer in layers){
                        layer.CollectLoads=false;layer.Strength=layer.RenderStrength=0f;layer.Emitting=false;
                        Array.Clear(layer.Profile,0,layer.Profile.Length);
                        Array.Clear(layer.RenderProfile,0,layer.RenderProfile.Length);
                    }
                    foreach(var tip in tips){tip.Carry=0f;tip.Emitter.particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}
                    leftTip=rightTip=0f;visualTime=0f;culled=true;
                }
                return;
            }
            if(culled){foreach(var layer in layers)layer.CollectLoads=true;culled=false;}
            visualTime+=dt;
            if(visualTime>=1f/30f){float elapsed=visualTime;visualTime=0f;
                StepLocal(Quaternion.Inverse(body.rotation)*airflow, aircraft.airDensity, normalG, aircraft.radarAlt, aircraft.displayDetail,
                    (Quaternion.Inverse(body.rotation)*body.angularVelocity).z, elapsed);}
            GuardTips();
            EmitPhysicalTips(body,airflow,dt);
            if (airflow.magnitude > 60f && runtimeReports < 15 && Time.unscaledTime >= nextRuntimeReport)
                RuntimeReport(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "speed={0:F1} G={1:F2} detail={2:F2} radarAlt={3:F1} density={4:F3} demand={5:F2}/{6:F2} hosts={7}/{8} sheets={9} vertices={10} strength={11:F2}",
                    airflow.magnitude, normalG, aircraft.displayDetail, aircraft.radarAlt, aircraft.airDensity, leftTip, rightTip,
                    layers.Count(l=>l.Particles!=null && l.Particles.gameObject.activeInHierarchy),layers.Length,
                    layers.Count(l=>l.Sheet!=null && l.Sheet.Renderer.enabled),layers.Sum(l=>l.Sheet==null?0:l.Sheet.Mesh.vertexCount),
                    layers.Sum(l=>l.Demand))+string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        " physicalLoads={0}/{1} suctionPa={2:F0} coolingK={3:F2} RH={4:F2}",freshLoads,layers.Length,maxPressureDrop,maxCooling,VortexConventionalPlugin.VaporHumidity));
        }
        private void RuntimeReport(string state)
        {
            if(runtimeReports>=15 || Time.unscaledTime<nextRuntimeReport)return;
            runtimeReports++;nextRuntimeReport=Time.unscaledTime+2f;
            VortexConventionalPlugin.ReportAssetState(RuntimeLabel+" vapor runtime: "+state);
        }
        private string RuntimeLabel => family==Fs20VaporDriver.CKey?"FS-20C":"FS-20T";

        internal void Step(Vector3 airflow, float density, float normalG, float radarAltitude, float detail, float rollRate, float dt)
        {
            if(!EnsureBindings())return;
            StepLocal(transform.InverseTransformDirection(airflow),density,normalG,radarAltitude,detail,rollRate,dt);
            RenderTick(dt);
        }
        private void StepLocal(Vector3 local,float density,float normalG,float radarAltitude,float detail,float rollRate,float dt)
        {
            var body=ResolveBody();
            Vector3 airflow=body!=null ? body.rotation*local : transform.TransformDirection(local);
            float altitude=body==null ? transform.GlobalPosition().y : body.transform.GlobalPosition().y;
            atmosphere=Fs20TVaporEnvelope.Ambient(density,altitude,VortexConventionalPlugin.VaporHumidity);
            freshLoads=0;maxPressureDrop=0f;maxCooling=0f;
            float leftLift=0f,rightLift=0f,leftArea=0f,rightArea=0f,leftSpan=0f,rightSpan=0f;
            float detailGate=Mathf.Clamp01((detail-.4f)/.35f)*quality;
            foreach (var layer in layers)
            {
                var ps = layer.Particles;
                if (ps == null) continue;
                bool alive = layer.Owner != null && layer.Frame!=null && Attached(layer.SourceOwner) && layer.AttachmentChain.All(Attached);
                int stage = alive ? Fs20TDamagePolicy.State((float)Health.GetValue(layer.Owner), (bool)Fragmented.GetValue(layer.Owner)) : Fs20TDamagePolicy.Lost;
                // Auxiliary skins have no authored damaged geometry. Hide them
                // with damage instead of painting vapor across an unseen hole.
                if(layer.Auxiliary && stage>0 && layer.Surfaces.Length==1)stage=Fs20TDamagePolicy.Lost;
                layer.JoinMix=stage==0 && layer.JoinPart!=null && layer.JoinChain.All(Attached) &&
                    Fs20TDamagePolicy.State((float)Health.GetValue(layer.JoinPart),(bool)Fragmented.GetValue(layer.JoinPart))==0?1f:0f;
                if (stage != layer.LastState)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    layer.Sheet?.Hide();
                    layer.Strength=0f;
                    layer.RenderStrength=0f;
                    layer.Emitting=false;
                    Array.Clear(layer.Profile,0,layer.Profile.Length);
                    Array.Clear(layer.RenderProfile,0,layer.RenderProfile.Length);
                    if (stage < Fs20TDamagePolicy.Lost) { var shape = ps.shape; shape.mesh = layer.Surfaces[Mathf.Min(stage, layer.Surfaces.Length - 1)]; }
                    layer.LastState = stage;
                }
                Vector3 pointFlow=airflow;
                if(body!=null && layer.Frame!=null)pointFlow+=Vector3.Cross(body.angularVelocity,layer.Frame.TransformPoint(layer.FlowOrigin)-body.worldCenterOfMass);
                layer.PointFlow=pointFlow;
                bool fresh=alive && stage<Fs20TDamagePolicy.Lost && layer.LoadCount>0 && Time.fixedTime-layer.LoadTime<=.15f;
                float pressureJump=fresh && layer.Area>.001f && Fs20TVaporEnvelope.Finite(layer.Lift) ? Mathf.Max(0f,layer.Lift)/layer.Area : 0f;
                // Body-shoulder suction is a wing-root-correlated approximation,
                // not a second copy of lift or a solved fuselage pressure field.
                if(layer.Role=="body")pressureJump*=.65f;
                float target=0f;
                if(fresh){
                    freshLoads++;
                    if(!layer.Auxiliary){
                        if(layer.Left){leftLift+=layer.Lift;leftArea+=layer.Area;leftSpan=Mathf.Max(leftSpan,layer.Span);}
                        else{rightLift+=layer.Lift;rightArea+=layer.Area;rightSpan=Mathf.Max(rightSpan,layer.Span);}
                    }
                }
                // Transport newly condensed water along the chord before it
                // evaporates. This reduced-order relaxation is not a CFD field.
                float carryWater=0f;
                float alongFlow=Vector3.Dot(pointFlow,transform.forward);
                float retention=Mathf.Exp(-layer.FlowLength/(layer.Profile.Length-1)/Mathf.Max(1f,Mathf.Abs(alongFlow))/.06f);
                for(int along=0;along<layer.Profile.Length;along++){
                    int station=alongFlow>=0f?along:layer.Profile.Length-1-along;
                    float suction=.5f*pressureJump*ChordWeights[along];
                    var cloud=Fs20TVaporEnvelope.Evaluate(atmosphere,suction,.6f);
                    carryWater=Mathf.Max(cloud.Water,carryWater*retention);
                    float opacity=1f-Mathf.Exp(-750f*carryWater*.6f);
                    layer.Profile[station]=stage<Fs20TDamagePolicy.Lost ? Fs20TVaporEnvelope.Approach(layer.Profile[station],opacity,dt) : 0f;
                    target=Mathf.Max(target,layer.Profile[station]);
                    maxPressureDrop=Mathf.Max(maxPressureDrop,atmosphere.Pressure-cloud.Pressure);
                    maxCooling=Mathf.Max(maxCooling,atmosphere.Temperature-cloud.Temperature);
                }
                target=Mathf.Max(target,CarryIncoming(layer,fresh,stage,pointFlow));
                layer.Strength=Fs20TVaporEnvelope.Approach(layer.Strength,target,dt);
                layer.Visibility=detailGate;
                float strength=layer.Strength*detailGate*(stage<Fs20TDamagePolicy.Lost ? 1f : 0f);
                if(strength<.01f)strength=0f;
                layer.Demand=strength;
            }
            float speed=airflow.magnitude;
            float lt=Fs20TVaporEnvelope.Evaluate(atmosphere,Fs20TVaporEnvelope.TipSuction(leftLift,density,speed,leftSpan,leftArea/Mathf.Max(.1f,leftSpan)),.15f).Opacity*detailGate;
            float rt=Fs20TVaporEnvelope.Evaluate(atmosphere,Fs20TVaporEnvelope.TipSuction(rightLift,density,speed,rightSpan,rightArea/Mathf.Max(.1f,rightSpan)),.15f).Opacity*detailGate;
            leftTip=Fs20TVaporEnvelope.Approach(leftTip,lt,dt);rightTip=Fs20TVaporEnvelope.Approach(rightTip,rt,dt);
        }
        private void LateUpdate() => RenderTick(Time.deltaTime);
        internal void RenderTick(float dt)
        {
            if(!bindingsInitialized || culled || layers==null || dt<=0f)return;
            dt=Mathf.Min(dt,.1f);
            float blend=1f-Mathf.Exp(-dt/.06f);
            var body=ResolveBody();
            if(body!=null){
                var flow=transform.InverseTransformDirection(body.velocity-(Vector3)Wind.GetValue(aircraft));
                // The shader samples density at scaled position minus phase.
                // Its features therefore follow phase: air crosses the aircraft
                // opposite to its air-relative velocity. Match the shader's
                // anisotropic density coordinates without skewing that direction.
                transport-=Vector3.Scale(flow,new Vector3(.42f,1.25f,.23f))*(dt*(.008f/.23f));
                transport=new Vector3(Mathf.Repeat(transport.x,1f),Mathf.Repeat(transport.y,1f),Mathf.Repeat(transport.z,1f));
            }
            foreach(var layer in layers){
                var ps=layer.Particles;
                if(ps==null || layer.LastState<0 || layer.LastState>=Fs20TDamagePolicy.Lost)continue;
                layer.RenderStrength=Mathf.Lerp(layer.RenderStrength,layer.Strength,blend);
                for(int i=0;i<layer.RenderProfile.Length;i++)
                    layer.RenderProfile[i]=Mathf.Lerp(layer.RenderProfile[i],layer.Profile[i],blend);
                float strength=layer.RenderStrength*layer.Visibility;
                // Sub-visible fluctuations must not repeatedly restart the system.
                if(layer.Emitting ? strength<.003f : strength>.012f)layer.Emitting=!layer.Emitting;
                if(layer.Emitting)layer.Sheet.Render(layer.RenderProfile,layer.PointFlow,strength,layer.Visibility,dt,layer.LastState,viewCamera,transport,transform);
                else layer.Sheet.Hide();
            }
        }
        private void EmitPhysicalTips(Rigidbody body,Vector3 flow,float dt)
        {
            if(tips==null)return;
            foreach(var tip in tips){
                var e=tip.Emitter;
                tip.Carry=Mathf.Min(2f,tip.Carry+Mathf.Max(0f,dt)*e.emitFrequency*Mathf.Clamp01(flow.magnitude/Mathf.Max(1f,e.minSpeed)));
                if(tip.Carry<1f)continue;
                tip.Carry-=1f;
                for(int i=0;i<tip.Origins.Length;i++){
                    var origin=e.emitTransforms[i];if(origin==null)continue;
                    float opacity=transform.InverseTransformPoint(origin.position).x<0f ? leftTip : rightTip;
                    if(opacity<.01f)continue;
                    // Reuse native tip material, lifetime and global-space transport.
                    // The old AoA threshold and its one-second fade are not called.
                    var main=e.particles.main;
                    var ep=new ParticleSystem.EmitParams {position=origin.GlobalPosition().AsVector3(),
                        velocity=body.velocity+UnityEngine.Random.insideUnitSphere*main.startSpeed.constant,
                        startColor=new Color(1f,1f,1f,opacity),applyShapeToPosition=false};
                    e.particles.Emit(ep,1);
                }
            }
        }

        internal void GuardTips()
        {
            if (tips == null)
            {
                var native = GetComponent<VaporEffect>();
                tips = ((VaporEmitter[])Emitters.GetValue(native)).Where(e => e.particles != null &&
                    e.particles.name.IndexOf("wingtip", StringComparison.OrdinalIgnoreCase) >= 0).Select(e => new Tip {
                        Emitter = e, Origins = (Transform[])e.emitTransforms.Clone(),
                        Owners = e.emitTransforms.Select(t => t == null ? null : t.GetComponentInParent<UnitPart>(true)).ToArray(),
                        Chains = e.emitTransforms.Select(t => t == null ? Array.Empty<UnitPart>() : t.GetComponentsInParent<UnitPart>(true)).ToArray() }).ToArray();
            }
            foreach (var tip in tips)
            {
                for (int i = 0; i < tip.Origins.Length; i++)
                {
                    var owner = tip.Owners[i];
                    bool alive = owner != null && tip.Chains[i].All(Attached);
                    tip.Emitter.emitTransforms[i] = alive ? tip.Origins[i] : null;
                }
                if(tip.Emitter.emitTransforms.All(t=>t==null))tip.Emitter.particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                else if(!tip.Emitter.particles.isPlaying)tip.Emitter.particles.Play(false);
            }
        }
        private static bool Attached(UnitPart part) => part != null && !part.IsDetached() && !(bool)Fragmented.GetValue(part);
        private void OnDisable() => StopLayers();
        private void OnDestroy(){if(layers!=null)foreach(var layer in layers){if(loadsRegistered)Fs20VaporDriver.Unregister(layer);layer.Sheet?.Dispose();}}
        private void StopLayers()
        {
            if (layers == null) return;
            foreach (var layer in layers) {layer.Demand=0f;layer.Sheet?.Hide();if (layer.Particles != null){var emission=layer.Particles.emission;emission.rateOverTime=0f;layer.Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);}}
        }
    }
}
