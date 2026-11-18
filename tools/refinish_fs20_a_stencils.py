"""Offline procedural coat repair at the retired VTOL stencils, in production UVs."""
from pathlib import Path
import ast,json,sys,os
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'.deps/engineering-plot'))
import numpy as np
from PIL import Image
project=Path(__file__).resolve().parents[1];stage=Path(os.environ['FS20_PAINT_INPUT'])
module=ast.parse((project/'tools/author_fs20_prepared_liveries.py').read_text())
scope={'np':np};functions=[n for n in module.body if isinstance(n,ast.FunctionDef) and n.name in ('hashgrid','noise')]
exec(compile(ast.Module(body=functions,type_ignores=[]),'verified-coat-field','exec'),scope)
sets=ast.literal_eval(next(n.value for n in module.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='SETS' for t in n.targets)))
cache=np.load('D:/Artifacts/fs20-own-liveries/2026-11-16/uv-world-A-body-2048.npz');world=cache['world'];valid=cache['valid'];x=np.abs(world[:,:,0]);z=world[:,:,2]+world[:,:,1]*.35;noise=scope['noise'];manifest=json.loads((stage/'manifest.json').read_text());rows=[]
regions=[(60,268,144,334),(1655,254,1741,321),(592,1226,643,1278)]
baseline=Path('D:/Artifacts/fs20-publication/0.7.0/paint-input')
for p in manifest['paints']:
 if p['variant']!='A':continue
 slug=p['id'].split('.',1)[1];palette=np.asarray(next(s[4] for s in sets['A'] if s[0]==slug),np.float32)
 if 'hex' in slug or 'digital' in slug or slug=='forest':
  qx=np.round(x/.22)*.22;qz=np.round(z/.19)*.19;n=noise(qx,qz,1.7,73)+.2*noise(qx,qz,.52,131)
 elif slug=='desert':n=noise(x+z*.22,z,2.1,191)+.22*noise(x,z,.68,52)
 else:n=noise(x,z,2.2,73)+.22*noise(x,z,.72,131)
 coat=palette[np.digitize(n,[.44,.61,.76])];original=np.array(Image.open(baseline/p['body']).convert('RGBA'));result=original.copy();mask=np.zeros(valid.shape,bool)
 for x0,y0,x1,y1 in regions:
  # Remove the glyph's baked blur halo as well as the lettering itself.
  x0=max(1,x0-16);y0=max(1,y0-16);x1=min(2047,x1+16);y1=min(2047,y1+16)
  local=np.zeros(valid.shape,bool);local[y0:y1,x0:x1]=True
  rim=np.zeros(valid.shape,bool);rim[y0-1:y1+1,x0-1:x1+1]=True;rim&=~local;rim&=valid
  factor=np.median(original[:,:,:3].astype(np.float32).sum(2)[rim]/np.maximum(coat.sum(2)[rim],1));factor=float(np.clip(factor,.35,1.5))
  selected=local&valid;result[:,:,:3][selected]=np.clip(np.rint(coat[selected]*factor),0,255).astype(np.uint8);mask|=selected
 target=stage/p['body'];Image.fromarray(result).save(target,compress_level=4)
 changed=np.any(original!=result,axis=2);assert not np.any(changed&~mask)
 rows.append({'id':p['id'],'changedPixels':int(changed.sum()),'outsideExpandedStencilRegions':0})
(stage/'offline-cleanup.json').write_text(json.dumps({'preparedA':len(rows),'proceduralFieldReused':True,'paddingPixels':16,'rows':rows},indent=2));print('PROCEDURAL_A_STENCILS_PASS',len(rows))
