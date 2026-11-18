"""Replace only reduced visual records in a frozen, private UV-review fixture."""
from pathlib import Path
import io,struct,json,argparse
p=argparse.ArgumentParser();p.add_argument('--asset',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
stream=io.BytesIO(a.asset.read_bytes());orig=Path('D:/Artifacts/fs20-own-liveries/2026-11-16')
def i():return struct.unpack('<i',stream.read(4))[0]
def txt():return stream.read(i()).decode()
def mesh(vis):
 nv,nf=i(),i();stride=8 if vis else 3
 return ([struct.unpack('<'+'f'*stride,stream.read(stride*4)) for _ in range(nv)],[struct.unpack('<iiii' if vis else '<iii',stream.read(16 if vis else 12)) for _ in range(nf)])
assert i()==0x46535431;version=i();txt();pieces=[]
for _ in range(i()):
 name=txt();[txt() for _ in range(4)];i();materials=[txt() for _ in range(i())];lods=[mesh(True) for _ in range(3)];[mesh(False) for _ in range(i())];pieces.append((name,lods,materials))
for variant,folder in [('A','geometry-a4'),('T','geometry'),('NA','geometry-na')]:
 d=json.loads((orig/folder/('geometry-'+variant+'.json')).read_text());hits=0
 for name,lods,mats in pieces:
  for level in [1,2]:
   vs,faces=lods[level]
   for slot in range(len(mats)):
    record=next((x for x in d['meshes'] if x['name']=='FS20T '+name+' LOD'+str(level)+'_'+str(slot)),None)
    if record is None:continue
    record['vertices']=[dict(zip('xyz',v[:3])) for v in vs];record['normals']=[dict(zip('xyz',v[3:6])) for v in vs];record['uv']=[dict(zip('xy',v[6:8])) for v in vs];record['triangles']=[idx for f in faces if f[0]==slot for idx in f[1:]];hits+=1
 out=a.output/folder;out.mkdir(parents=True,exist_ok=True);(out/('geometry-'+variant+'.json')).write_text(json.dumps(d));print(variant,'patched',hits)
