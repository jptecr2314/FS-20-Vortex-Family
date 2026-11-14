using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Japensou.NuclearOption.VortexConventional
{
    internal static class Fs20NAGeometry
    {
        internal static void Install(Transform root)
        {
            var fins = root.GetComponentsInChildren<AeroPart>(true).Where(p => p.name.StartsWith("vstab_", StringComparison.Ordinal)).ToArray();
            if (fins.Length != 2) throw new InvalidOperationException("FS-20NA expected two native vertical stabilizers.");
            var removed = new HashSet<UnitPart>(fins.SelectMany(p => p.GetComponentsInChildren<UnitPart>(true)));
            var renderers = new HashSet<Renderer>(fins.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)));
            foreach (var part in root.GetComponentsInChildren<AeroPart>(true)) {
                if (removed.Contains(part)) continue;
                Fs20Tuning.Set(part, "joints", part.Joints.Where(j => !removed.Contains(j.connectedPart) && !removed.Contains(j.tensor)).ToArray());
            }
            foreach (var group in root.GetComponentsInChildren<LODGroup>(true)) {
                if (fins.Any(p => group.transform.IsChildOf(p.transform))) continue;
                var levels = group.GetLODs();
                for (int i = 0; i < levels.Length; i++) levels[i].renderers = levels[i].renderers.Where(r => r != null && !renderers.Contains(r)).ToArray();
                group.SetLODs(levels);
            }
            foreach (var part in root.GetComponentsInChildren<AeroPart>(true).Where(p => !removed.Contains(p))) {
                var damage = (DamageMaterial)Fs20Tuning.Field(typeof(UnitPart), "damageMaterial").GetValue(part);
                if (damage == null || damage.renderers == null) continue;
                var indices = damage.indices ?? Array.Empty<byte>();
                var keep = Enumerable.Range(0, damage.renderers.Count).Where(i => damage.renderers[i] != null && !renderers.Contains(damage.renderers[i])).ToArray();
                damage.indices = keep.Select(i => i < indices.Length ? indices[i] : (byte)0).ToArray();
                damage.renderers = keep.Select(i => damage.renderers[i]).ToList();
                Fs20Tuning.Set(part,"damageMaterial",damage);
            }
            foreach (var fin in fins) UnityEngine.Object.DestroyImmediate(fin.gameObject);
            // Native NavLight.Initialize dereferences its renderer. Removing the
            // fin must also remove the entries that referred to its deleted mesh.
            foreach(var lights in root.GetComponentsInChildren<NavLights>(true)) {
                var field=Fs20Tuning.Field(typeof(NavLights),"navLights");var entries=(Array)field.GetValue(lights);
                var keep=entries.Cast<object>().Where(e=>(Renderer)Fs20Tuning.Field(e.GetType(),"renderer").GetValue(e)!=null).ToArray();
                var result=Array.CreateInstance(entries.GetType().GetElementType(),keep.Length);
                for(int i=0;i<keep.Length;i++)result.SetValue(keep[i],i);field.SetValue(lights,result);
            }
            Fs20NATailClosure.Install(root);
            FitTrailingEdges(root);
            int count = 0;
            foreach (var marker in root.GetComponentsInChildren<Fs20TElevon>(true).Where(m => m.Outer)) {
                var surface = marker.GetComponent<ControlSurface>();
                InstallSplit(root, surface, marker.Side); count++;
            }
            if (count != 2) throw new InvalidOperationException("FS-20NA outer drag rudders could not be bound.");
            InstallOptics(root);
            Fs20NAEotsHousing.Install(root);
        }

        private sealed class SkinSection
        {
            internal Vector3[] Points;internal int[] Triangles;
            internal SkinSection(Mesh mesh,Matrix4x4 map){Points=mesh.vertices.Select(map.MultiplyPoint3x4).ToArray();Triangles=mesh.triangles;}
        }
        private static bool SectionAt(IEnumerable<SkinSection> skins,float x,float z,float center,out float low,out float high)
        {
            low=float.PositiveInfinity;high=float.NegativeInfinity;
            foreach(var skin in skins)for(int i=0;i<skin.Triangles.Length;i+=3){
                var a=skin.Points[skin.Triangles[i]];var b=skin.Points[skin.Triangles[i+1]];var c=skin.Points[skin.Triangles[i+2]];
                float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<1e-9f)continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/d;
                float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/d;
                if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                float y=u*a.y+v*b.y+(1-u-v)*c.y;if(Mathf.Abs(y-center)>.13f)continue;
                low=Mathf.Min(low,y);high=Mathf.Max(high,y);
            }
            return high-low>.01f;
        }
        private static void FitTrailingEdges(Transform root)
        {
            // The hinge axes already align in plan. The exported moving nose is
            // thinner than the fixed trailing edge, exposing an upper/lower step.
            // Fit a short rounded nose to that real section, leaving the pivot,
            // trailing outline, area and native control/mixing contract intact.
            var fixedSkin=new List<SkinSection>();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="wing1b_L"||f.name=="wing1b_R"||f.name=="wing2b_L"||f.name=="wing2b_R"||f.name=="wingtip_L"||f.name=="wingtip_R")){
                var mesh=Fs20Geometry.ReadableCopy(f.sharedMesh);fixedSkin.Add(new SkinSection(mesh,root.worldToLocalMatrix*f.transform.localToWorldMatrix));UnityEngine.Object.DestroyImmediate(mesh);
            }
            var listed=new HashSet<Renderer>(root.GetComponentsInChildren<LODGroup>(true).SelectMany(g=>g.GetLODs().SelectMany(l=>l.renderers)));
            foreach(var marker in root.GetComponentsInChildren<Fs20TElevon>(true)){
                var control=marker.GetComponent<ControlSurface>();var part=marker.GetComponent<AeroPart>();
                var visible=((GameObject)Fs20Tuning.Field(typeof(ControlSurface),"visibleMesh").GetValue(control)).transform;
                var pivot=root.InverseTransformPoint(visible.position);var axis=root.InverseTransformDirection(visible.right);
                var native=visible.GetComponent<MeshFilter>();var readable=Fs20Geometry.ReadableCopy(native.sharedMesh);
                var own=new[]{new SkinSection(readable,root.worldToLocalMatrix*visible.localToWorldMatrix)};UnityEngine.Object.DestroyImmediate(readable);
                float minX=own[0].Points.Min(p=>p.x)+.001f,maxX=own[0].Points.Max(p=>p.x)-.001f;
                Vector3 Fit(Vector3 p){
                    float z=pivot.z+(p.x-pivot.x)*axis.z/axis.x;float behind=z-p.z;if(behind>.14f||behind<-.006f)return p;
                    float x=Mathf.Clamp(p.x,minX,maxX),hz=pivot.z+(x-pivot.x)*axis.z/axis.x;
                    float cy=pivot.y+(x-pivot.x)*axis.y/axis.x;
                    bool supported=SectionAt(fixedSkin,x,hz+.003f,cy,out var lo,out var hi);
                    for(int step=1;!supported&&step<=4;step++){
                        float sx=Mathf.MoveTowards(x,pivot.x,.02f*step),sz=pivot.z+(sx-pivot.x)*axis.z/axis.x;
                        float sy=pivot.y+(sx-pivot.x)*axis.y/axis.x;
                        supported=SectionAt(fixedSkin,sx,sz+.003f,sy,out lo,out hi);if(supported){lo+=cy-sy;hi+=cy-sy;}
                    }
                    if(!supported)return p;
                    bool found=SectionAt(own,x,hz-.003f,cy,out var ol,out var oh);
                    // Chamfered end faces have no full vertical slice exactly at
                    // their tip. Sample just inboard, transporting the section
                    // along the same hinge instead of leaving that vertex unfit.
                    for(int step=1;!found&&step<=4;step++){
                        float sx=Mathf.MoveTowards(x,pivot.x,.02f*step),sz=pivot.z+(sx-pivot.x)*axis.z/axis.x;
                        float sy=pivot.y+(sx-pivot.x)*axis.y/axis.x;
                        found=SectionAt(own,sx,sz-.003f,sy,out ol,out oh);if(found){ol+=cy-sy;oh+=cy-sy;}
                    }
                    if(!found)return p;
                    float center=(lo+hi)*.5f,half=(hi-lo)*.5f,normalized=(p.y-(ol+oh)*.5f)/((oh-ol)*.5f);
                    if(behind<.004f&&Mathf.Abs(normalized)>.8f)normalized=Mathf.Sign(normalized);
                    float weight=Mathf.SmoothStep(0f,1f,1f-Mathf.Clamp01(behind/.14f));
                    p.y=Mathf.Lerp(p.y,center+normalized*half,weight);
                    if(behind<.004f)p.z+=Mathf.Sqrt(Mathf.Max(0f,1f-normalized*normalized))*half*.95f*(1f-Mathf.Clamp01(behind/.004f));
                    return p;
                }
                Mesh FitMesh(Mesh source,Transform frame){
                    var mesh=Fs20Geometry.ReadableCopy(source);mesh.name="FS20NA fitted "+source.name;
                    var toRoot=root.worldToLocalMatrix*frame.localToWorldMatrix;var back=frame.worldToLocalMatrix*root.localToWorldMatrix;
                    mesh.vertices=mesh.vertices.Select(p=>back.MultiplyPoint3x4(Fit(toRoot.MultiplyPoint3x4(p)))).ToArray();mesh.RecalculateBounds();mesh.RecalculateNormals();if(mesh.uv.Length==mesh.vertexCount)mesh.RecalculateTangents();return mesh;
                }
                foreach(var f in visible.GetComponentsInChildren<MeshFilter>(true))if(f==native||listed.Contains(f.GetComponent<Renderer>()))f.sharedMesh=FitMesh(f.sharedMesh,f.transform);
                var bounds=(Bounds)Fs20Tuning.Field(typeof(UnitPart),"bounds").GetValue(part);
                foreach(var collider in part.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.GetComponentInParent<AeroPart>()==part)){
                    collider.sharedMesh=FitMesh(collider.sharedMesh,collider.transform);
                    foreach(var p in collider.sharedMesh.vertices)bounds.Encapsulate(part.transform.InverseTransformPoint(collider.transform.TransformPoint(p)));
                }
                part.GetComponent<Fs20TPartPhysics>().StoreCollisionBounds(bounds);
                foreach(var group in visible.GetComponentsInChildren<LODGroup>(true))group.RecalculateBounds();
            }
        }

        private static void InstallSplit(Transform root, ControlSurface surface, float side)
        {
            // Reuse the native split-drag actuator and its damage owner. Each half
            // contains one side of the existing shell, not two overlapping full wings.
            var visible = (GameObject)Fs20Tuning.Field(typeof(ControlSurface), "visibleMesh").GetValue(surface);
            var filters = visible.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            var upper = new GameObject("FS20NA split upper").transform; upper.SetParent(visible.transform, false);
            var lower = new GameObject("FS20NA split lower").transform; lower.SetParent(visible.transform, false);
            foreach (var filter in filters) {
                var old = filter.GetComponent<Renderer>(); if (old == null) continue;
                // Native orphan LOD children are not members of the replacement
                // delta's LODGroup. Do not turn them into always-visible halves.
                bool listed=root.GetComponentsInChildren<LODGroup>(true).Any(g=>g.GetLODs().Any(l=>l.renderers.Contains(old)));
                if(filter.gameObject!=visible && !listed){old.enabled=false;continue;}
                var mesh = Fs20Geometry.ReadableCopy(filter.sharedMesh);
                var matrix = visible.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var points = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                float center = (points.Min(v => v.y) + points.Max(v => v.y)) * .5f;
                var halves = new Renderer[2];
                for (int half = 0; half < 2; half++) {
                    var copy = UnityEngine.Object.Instantiate(mesh); copy.name = "FS20NA split skin"; copy.vertices = points;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++) {
                        var indices = mesh.GetTriangles(sub); var kept = new List<int>();
                        for (int i = 0; i < indices.Length; i += 3) {
                            float y = (points[indices[i]].y + points[indices[i+1]].y + points[indices[i+2]].y) / 3f;
                            if ((y >= center) == (half == 0)) kept.AddRange(new[] { indices[i], indices[i+1], indices[i+2] });
                        }
                        copy.SetTriangles(kept, sub);
                    }
                    copy.RecalculateBounds(); copy.RecalculateNormals();
                    Fs20NASplitShell.Close(copy,old.sharedMaterials);
                    var node = new GameObject(filter.name + (half == 0 ? " upper" : " lower"));
                    node.layer = visible.layer; node.transform.SetParent(half == 0 ? upper : lower, false);
                    node.AddComponent<MeshFilter>().sharedMesh = copy;
                    var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterials = old.sharedMaterials;
                    halves[half] = renderer;
                    Fs20HookInterior.RegisterDamageRenderer(surface.transform, renderer);
                }
                foreach (var group in root.GetComponentsInChildren<LODGroup>(true)) {
                    var levels = group.GetLODs(); bool changed = false;
                    for (int i = 0; i < levels.Length; i++) if (levels[i].renderers.Contains(old)) {
                        levels[i].renderers = levels[i].renderers.Where(r => r != old).Concat(halves).ToArray(); changed = true;
                    }
                    if (changed) group.SetLODs(levels);
                }
                old.enabled = false;
            }
            Fs20Tuning.Set(surface, "splitUpper", upper); Fs20Tuning.Set(surface, "splitLower", lower);
            Fs20Tuning.Set(surface, "maxSplit", 45f);
            Fs20Tuning.Set(surface, "yawSplitFactor", side);
            // Native split drag is angle * coefficient * rho * V^2. Estimate
            // the two exposed leaves as flat plates using this surface's real area.
            float area=surface.GetComponent<AeroPart>().GetWingArea();
            Fs20Tuning.Set(surface, "splitDrag", Mathf.Max(.0035f,area*Mathf.Sin(45f*Mathf.Deg2Rad)*1.1f/45f));
        }

        private static void InstallOptics(Transform root)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "FS20NA optical glass"; material.color = new Color(.018f,.035f,.044f,1f);
            material.SetFloat("_Smoothness", .78f); material.SetFloat("_Metallic", .15f);
            var samples = new[] {
                ("Forward port",new Vector3(-.8f,.35f,5.8f),Vector3.right),
                ("Forward starboard",new Vector3(.8f,.35f,5.8f),Vector3.left),
                ("Dorsal",new Vector3(0,2f,.3f),Vector3.down),
                ("Ventral",new Vector3(0,-2f,2.8f),Vector3.up),
                ("Aft port",new Vector3(-2f,.35f,-3.2f),Vector3.right),
                ("Aft starboard",new Vector3(2f,.35f,-3.2f),Vector3.left),
                ("DAS forward upper",new Vector3(0,2,2.4f),Vector3.down),
                ("DAS forward lower",new Vector3(0,-2,5.4f),Vector3.up),
                ("DAS beam port",new Vector3(-2,.55f,1.5f),Vector3.right),
                ("DAS beam starboard",new Vector3(2,.55f,1.5f),Vector3.left),
                ("DAS aft upper",new Vector3(0,2,-3.5f),Vector3.down),
                ("DAS aft lower",new Vector3(0,-2,-3.5f),Vector3.up) };
            var firstLod = new HashSet<Renderer>();var otherLod = new HashSet<Renderer>();
            foreach(var group in root.GetComponentsInChildren<LODGroup>(true)) {
                var levels=group.GetLODs();if(levels.Length==0)continue;
                foreach(var r in levels[0].renderers)firstLod.Add(r);
                foreach(var level in levels.Skip(1))foreach(var r in level.renderers)otherLod.Add(r);
            }
            var source = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.GetComponent<Renderer>() != null &&
                f.GetComponent<Renderer>().enabled && (!otherLod.Contains(f.GetComponent<Renderer>()) || firstLod.Contains(f.GetComponent<Renderer>())) &&
                f.GetComponent<Renderer>().sharedMaterials.Any(m => m != null && m.name.Contains("skin"))).Select(filter => {
                    var mesh=Fs20Geometry.ReadableCopy(filter.sharedMesh);
                    var map=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var points=mesh.vertices.Select(map.MultiplyPoint3x4).ToArray();var materials=filter.GetComponent<Renderer>().sharedMaterials;
                    var indices=Enumerable.Range(0,mesh.subMeshCount).Where(s => s<materials.Length && materials[s]!=null && materials[s].name.Contains("skin"))
                        .SelectMany(s=>mesh.GetTriangles(s)).ToArray();
                    UnityEngine.Object.DestroyImmediate(mesh);
                    return (Filter:filter,Points:points,Indices:indices);
                }).ToArray();
            float RoofY(float x,float z) {
                float highest=float.NegativeInfinity;
                foreach(var surface in source)for(int i=0;i<surface.Indices.Length;i+=3){
                    var a=surface.Points[surface.Indices[i]];var b=surface.Points[surface.Indices[i+1]];var c=surface.Points[surface.Indices[i+2]];
                    if(Vector3.Cross(b-a,c-a).y<=1e-7f)continue;
                    float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<1e-9f)continue;
                    float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/d;
                    float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/d;
                    if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                    highest=Mathf.Max(highest,u*a.y+v*b.y+(1-u-v)*c.y);
                }
                if(float.IsNegativeInfinity(highest))throw new InvalidOperationException("Dorsal optical aperture has no supporting skin.");
                return highest;
            }
            var ports = new List<Transform>(); var owners = new List<AeroPart>();
            var dasPorts=new List<Transform>();var dasOwners=new List<AeroPart>();
            foreach (var sample in samples) {
                float nearest = float.PositiveInfinity; Vector3 hit = default, normal = default; MeshFilter owner = null;
                foreach (var surface in source) {
                    var filter=surface.Filter;var vertices=surface.Points;var ids=surface.Indices;
                    for (int i = 0; i < ids.Length; i += 3) {
                        var a = vertices[ids[i]]; var b = vertices[ids[i+1]]; var c = vertices[ids[i+2]];
                        var n = Vector3.Cross(b-a,c-a).normalized; float denominator = Vector3.Dot(n,sample.Item3);
                        if (denominator >= -1e-5f) continue;
                        float t = Vector3.Dot(n,a-sample.Item2)/denominator; if (t <= 0f || t >= nearest) continue;
                        var p = sample.Item2 + sample.Item3*t;
                        if (Vector3.Dot(Vector3.Cross(b-a,p-a),n)<-1e-5f || Vector3.Dot(Vector3.Cross(c-b,p-b),n)<-1e-5f || Vector3.Dot(Vector3.Cross(a-c,p-c),n)<-1e-5f) continue;
                        nearest=t;hit=p;normal=n;owner=filter;
                    }
                }
                if (owner == null) throw new InvalidOperationException("FS-20NA optical aperture surface missing: " + sample.Item1);
                var part = owner.GetComponentInParent<AeroPart>(true);
                bool das=sample.Item1.StartsWith("DAS ");
                bool dorsal=sample.Item1=="Dorsal"||sample.Item1=="DAS forward upper"||sample.Item1=="DAS aft upper";
                if(dorsal){
                    // A single triangle at the dorsal centre seam gave a tilted
                    // normal. LookRotation's default up also left the in-plane
                    // orientation ambiguous for an almost vertical normal.
                    const float step=.05f;
                    float dx=(RoofY(hit.x+step,hit.z)-RoofY(hit.x-step,hit.z))/(2*step);
                    float dz=(RoofY(hit.x,hit.z+step)-RoofY(hit.x,hit.z-step))/(2*step);
                    normal=new Vector3(-dx,1f,-dz).normalized;hit.y=RoofY(hit.x,hit.z);
                }
                var node = new GameObject(das?"FS20NA "+sample.Item1:"FS20NA EO " + sample.Item1); node.layer = root.gameObject.layer;
                node.transform.SetParent(part.transform,false);
                node.transform.position = root.TransformPoint(hit + normal*(das||dorsal?.002f:.006f));
                node.transform.rotation = root.rotation*(dorsal?Quaternion.LookRotation(normal,Vector3.ProjectOnPlane(Vector3.forward,normal).normalized):Quaternion.LookRotation(normal));
                var meshWindow = new Mesh { name = "Flush six-sided optical aperture" };
                var points = new Vector3[7]; var triangles = new int[18];
                for(int i=0;i<6;i++) { float angle=i*Mathf.PI/3;points[i+1]=new Vector3(Mathf.Cos(angle)*(das?.095f:.115f),Mathf.Sin(angle)*(das?.065f:.075f),0);
                    triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%6+1; }
                if(dorsal)for(int i=0;i<points.Length;i++){
                    var p=root.InverseTransformPoint(node.transform.TransformPoint(points[i]));p.y=RoofY(p.x,p.z)+.002f;
                    points[i]=node.transform.InverseTransformPoint(root.TransformPoint(p));
                }
                meshWindow.vertices=points;meshWindow.triangles=triangles;meshWindow.RecalculateNormals();meshWindow.RecalculateBounds();
                node.AddComponent<MeshFilter>().sharedMesh=meshWindow;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                Fs20HookInterior.RegisterDamageRenderer(part.transform,renderer);
                var disintegration=Fs20Tuning.Field(typeof(UnitPart),"disintegrateObjects");
                disintegration.SetValue(part,((GameObject[])disintegration.GetValue(part)??Array.Empty<GameObject>()).Concat(new[]{node}).ToArray());
                if(das){dasPorts.Add(node.transform);dasOwners.Add(part);}else{ports.Add(node.transform);owners.Add(part);}
                Fs20ExpandedBays.AddMassAt(root,part,das?2f:5f,hit);
            }
            // One native visual search, not six redundant battlefield scans.
            // This uses the game's abstract passive-visual/LoS contract; it does
            // not claim camera feeds, thermal imaging or a per-pixel DAS model.
            var array = new GameObject("FS20NA passive optical array");array.transform.SetParent(root,false);
            array.AddComponent<Fs20NAOpticalArray>().Configure(root,ports.ToArray(),owners.ToArray());
            var dasArray=new GameObject("FS20NA distributed infrared array");dasArray.transform.SetParent(root,false);
            dasArray.AddComponent<Fs20NADAS>().Configure(root,dasPorts.ToArray(),dasOwners.ToArray());
        }
    }

    public sealed class Fs20NAOpticalArray : TargetDetector
    {
        [SerializeField] private Transform[] apertures;
        [SerializeField] private AeroPart[] owners;
        private static readonly System.Reflection.FieldInfo Fragmented=Fs20Tuning.Field(typeof(UnitPart),"fragmented");
        private int cursor;
        [SerializeField] private Fs20NARecon recon;
        internal Transform[] Apertures=>apertures;
        internal AeroPart[] Owners=>owners;
        internal void Bind(Fs20NARecon value){recon=value;}
        internal void Configure(Transform root,Transform[] ports,AeroPart[] parts)
        {
            apertures=ports;owners=parts;
            Fs20Tuning.Set(this,"attachedUnit",root.GetComponent<Aircraft>());
            Fs20Tuning.Set(this,"part",root.GetComponent<AeroPart>());
            Fs20Tuning.Set(this,"scanner",ports[0]);Fs20Tuning.Set(this,"visualRange",12000f);
            Fs20Tuning.Set(this,"magnification",1.5f);Fs20Tuning.Set(this,"maxSpeed",1600f);
            Fs20Tuning.Set(this,"checkInterval",1.5f);Fs20Tuning.Set(this,"alertCheckInterval",.75f);
            Fs20Tuning.Set(this,"shared",false);
        }
        protected override void TargetSearch()
        {
            if(recon!=null)recon.Search();
        }
        internal bool TrySelectAperture()
        {
            for(int i=0;apertures!=null && i<apertures.Length;i++) {
                cursor=(cursor+1)%apertures.Length;
                if(apertures[cursor]==null || owners[cursor]==null || owners[cursor].IsDetached() || (bool)Fragmented.GetValue(owners[cursor]))continue;
                Fs20Tuning.Set(this,"scanner",apertures[cursor]);return true;
            }
            return false;
        }
    }
}
