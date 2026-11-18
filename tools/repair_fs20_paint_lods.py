"""Blender background: reproject reduced UVs and refine high-error triangles.
Curved native roots use source-chart-preserving dissolution. LOD0, hulls,
ownership, rig and the physics tail remain byte-identical.
"""
import bpy,bmesh,struct,io,json,os,hashlib,math
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import barycentric_transform
from mathutils.bvhtree import BVHTree
root=Path(os.environ['FS20_PAINT_FIX']);source=Path(os.environ['FS20_PAINT_GEOMETRY'])
data=source.read_bytes();r=io.BytesIO(data);out=io.BytesIO();report=[]
def integer():return struct.unpack('<i',r.read(4))[0]
def text():return r.read(integer()).decode()
def mesh(visual):
 start=r.tell();nv,nf=integer(),integer();stride=8 if visual else 3
 vertices=[struct.unpack('<'+'f'*stride,r.read(stride*4)) for _ in range(nv)]
 faces=[struct.unpack('<iiii' if visual else '<iii',r.read(16 if visual else 12)) for _ in range(nf)]
 return {'vertices':vertices,'faces':faces,'raw':data[start:r.tell()]}
def encode(vertices,faces):
 b=io.BytesIO();b.write(struct.pack('<ii',len(vertices),len(faces)))
 for v in vertices:b.write(struct.pack('<8f',*v))
 for f in faces:b.write(struct.pack('<4i',*f))
 return b.getvalue()
def reproject(high,low,tolerance):
 positions=[Vector(v[:3]) for v in high['vertices']];trees={}
 for slot in set(f[0] for f in high['faces']):
  triangles=[f[1:] for f in high['faces'] if f[0]==slot]
  trees[slot]=(BVHTree.FromPolygons(positions,triangles,all_triangles=True),triangles)
 def sample(p,uv,normal,slot):
  tree,tris=trees[slot];nearest=tree.find_nearest(p)
  if nearest[0] is None:return Vector(uv)
  candidates=tree.find_nearest_range(p,max(.0005,nearest[3]+.0001));options=[]
  for point,n,idx,distance in candidates:
   if n.dot(normal)<.15:continue
   ids=tris[idx];predicted=barycentric_transform(point,*[positions[i] for i in ids],*[Vector((*high['vertices'][i][6:8],0)) for i in ids]);value=Vector(predicted[:2]);options.append((distance,(value-uv).length,value))
  if not options:return Vector(uv)
  # Near coincident chart edges retain the source island closest to old UV.
  distance=min(x[0] for x in options);return min((x for x in options if x[0]<=distance+.0001),key=lambda x:x[1])[2]
 records=[];faces=[];lookup={}
 def record(v):
  key=tuple(v)
  if key not in lookup:lookup[key]=len(records);records.append(key)
  return lookup[key]
 def refine(vs,slot,depth):
  pp=[Vector(v[:3]) for v in vs];uv=[Vector(v[6:8]) for v in vs];faceNormal=(pp[1]-pp[0]).cross(pp[2]-pp[0]).normalized();mids=[];error=0
  for a,b in [(0,1),(1,2),(2,0)]:
   p=(pp[a]+pp[b])*.5;u=(uv[a]+uv[b])*.5;new=sample(p,u,faceNormal,slot);error=max(error,(new-u).length*2048);n=(Vector(vs[a][3:6])+Vector(vs[b][3:6])).normalized();mids.append(tuple(p)+tuple(n)+tuple(new))
  if error>tolerance and depth<1:
   a,b,c=vs;ab,bc,ca=mids
   for tri in [(a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)]:refine(tri,slot,depth+1)
  else:faces.append((slot,*[record(v) for v in vs]))
 for slot,a,b,c in low['faces']:
  vs=[low['vertices'][i] for i in (a,b,c)];normal=(Vector(vs[1][:3])-Vector(vs[0][:3])).cross(Vector(vs[2][:3])-Vector(vs[0][:3])).normalized()
  revised=[v[:6]+tuple(sample(Vector(v[:3]),Vector(v[6:8]),normal,slot)) for v in vs]
  refine(revised,slot,0)
 return {'vertices':records,'faces':faces,'raw':encode(records,faces)}
def simplify(m,tolerance):
 bm=bmesh.new();layer=bm.verts.layers.int.new('source');uv=bm.loops.layers.uv.new('UV')
 verts=[];weld={}
 for i,p in enumerate(m['vertices']):
  key=tuple(round(x,6) for x in p[:3])+tuple(round(x,4) for x in p[3:6])+tuple(round(x,6) for x in p[6:8])
  if key not in weld:
   v=bm.verts.new(p[:3]);v[layer]=i;weld[key]=v
  verts.append(weld[key])
 for slot,a,b,c in m['faces']:
  try:f=bm.faces.new([verts[a],verts[b],verts[c]])
  except ValueError:
   # Preserve intentional coincident/back-facing shells instead of deleting them.
   fresh=[]
   for index in (a,b,c):v=bm.verts.new(m['vertices'][index][:3]);v[layer]=index;fresh.append(v)
   f=bm.faces.new(fresh)
  f.material_index=slot
  for loop in f.loops:loop[uv].uv=m['vertices'][loop.vert[layer]][6:8]
 bm.normal_update();free=[]
 for e in bm.edges:
  safe=len(e.link_faces)==2
  if safe:
   a,b=e.link_faces
   safe=a.material_index==b.material_index and a.normal.dot(b.normal)>.999999
   if safe:
    for f,g in [(a,b),(b,a)]:
     base=list(f.loops);pos=[x.vert.co for x in base];tex=[Vector((*x[uv].uv,0)) for x in base]
     for loop in g.loops:
      if abs((loop.vert.co-pos[0]).dot(f.normal))>1e-6:safe=False;break
      predicted=barycentric_transform(loop.vert.co,*pos,*tex)
      if (predicted-Vector((*loop[uv].uv,0))).length>tolerance/2048:safe=False;break
     if not safe:break
  e.seam=not safe
  if safe:free.append(e)
 # Delimiters protect every attribute discontinuity; no silhouette collapse.
 if free:bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=free,use_dissolve_boundaries=False,delimit={'SEAM','UV','MATERIAL','NORMAL'})
 bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
 records=[];lookup={};faces=[]
 for f in bm.faces:
  ids=[]
  for loop in f.loops:
   original=m['vertices'][loop.vert[layer]];record=tuple(loop.vert.co)+original[3:6]+tuple(loop[uv].uv)
   if record not in lookup:lookup[record]=len(records);records.append(record)
   ids.append(lookup[record])
  if len(ids)==3:faces.append((f.material_index,*ids))
 bm.free();return {'vertices':records,'faces':faces,'raw':encode(records,faces)}
assert integer()==0x46535431;version=integer();assert version in (2,3);text();count=integer();out.write(data[:r.tell()])
for i in range(count):
 start=r.tell();name=text();path=text();parent=text();owner=text();motion=text();replace=integer();materials=[text() for _ in range(integer())];out.write(data[start:r.tell()])
 lods=[mesh(True) for _ in range(3)];out.write(lods[0]['raw'])
 for level,tol in [(1,2.),(2,6.)]:
  if name.startswith('wingRoot_'):
   # Curved native root charts meet multiple coincident shells. Preserve their
   # chart boundaries rather than projecting onto a neighbouring shell.
   revised=simplify(lods[0],.05 if level==1 else .15)
  else:revised=reproject(lods[0],lods[level],tol) if any(x in ('SmallFighter1_skin','__deltaSkin') for x in materials) else lods[level]
  assert len(revised['faces'])<=4*len(lods[level]['faces'])
  out.write(revised['raw']);report.append({'piece':name,'level':level,'lod0':len(lods[0]['faces']),'previous':len(lods[level]['faces']),'current':len(revised['faces'])})
 start=r.tell();nh=integer()
 for _ in range(nh):mesh(False)
 out.write(data[start:r.tell()])
out.write(data[r.tell():]);dest=root/'Fs20TFinish.bin';dest.write_bytes(out.getvalue())
(root/'lod-rebuild.json').write_text(json.dumps({'baselineSha256':hashlib.sha256(data).hexdigest(),'candidateSha256':hashlib.sha256(out.getvalue()).hexdigest(),'pieces':count,'visualLods':report,'lod0AndNonvisualBytesPreserved':True},indent=2))
print('FS20_UV_LODS_REBUILT',count,[(level,sum(x['current'] for x in report if x['level']==level)) for level in [1,2]],flush=True)
