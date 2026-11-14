"""Build a local 0.6.9 pricing revision from its frozen, installed baseline."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
BASE=Path('D:/Artifacts/fs20-multiplayer/0.6.9')
STAGE=BASE/'pricing'
SOURCE=STAGE/'source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def replace(text,before,after):
    assert text.count(before)==1,before
    return text.replace(before,after,1)
def main():
    original=json.loads((BASE/'BUILD.json').read_text())
    assert original['dllSha256']=='790257fe9c5b998b79aa8ea20e4fbed37d5165a94c0356adc5bd593e3858ed1f'
    source_base=Path(original['source'])
    for p in source_base.rglob('*'):
        if not p.is_file() or p.relative_to(source_base).parts[0] in ('build','.deps','.git'):continue
        target=SOURCE/p.relative_to(source_base);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
    spec=json.loads((SOURCE/'build-spec.json').read_text())
    files=['src/Fs20VariantPricing.cs','src/SharedAirframes/AirframePool.cs','src/SharedAirframes/SharedAirframesPlugin.cs']
    for name in files:shutil.copy2(ROOT/name,SOURCE/name)
    if files[0] not in spec['sources']:spec['sources'].append(files[0])
    changes={
      'src/Fs20AirframeA.cs':('definition.code = "FS-20A";','definition.code = "FS-20A";\n                Fs20VariantPricing.Apply(definition);'),
      'src/Fs20TAirframe.cs':('definition.code = "FS-20T";','definition.code = "FS-20T";\n                Fs20VariantPricing.Apply(definition);'),
      'src/Fs20NAAirframe.cs':('definition.unitName = "FS-20NA Night Attack"; definition.code = "FS-20NA";','definition.unitName = "FS-20NA Night Attack"; definition.code = "FS-20NA";\n                Fs20VariantPricing.Apply(definition);')}
    for name,(before,after) in changes.items():
        p=SOURCE/name;p.write_text(replace(p.read_text(encoding='utf-8-sig'),before,after),encoding='utf-8')
    p=SOURCE/'src/VortexConventionalPlugin.cs';text=p.read_text(encoding='utf-8-sig')
    text=replace(text,'throw new InvalidOperationException("Native FS-20 definition is unavailable.");','throw new InvalidOperationException("Native FS-20 definition is unavailable.");\n            Fs20VariantPricing.Apply(original);')
    text=replace(text,'definitionCopy.code = "FS-20C";','definitionCopy.code = "FS-20C";\n                Fs20VariantPricing.Apply(definitionCopy);');p.write_text(text,encoding='utf-8')
    (SOURCE/'build-spec.json').write_text(json.dumps(spec,indent=2)+'\n')
    note='\n\n## Per-variant economy revision (local, not installed)\n\nBase airframes: A $75m, B $90m, C $105m, T $125m, NA $150m.\nFaction supply sharing remains optional. Purchased and reserved aircraft retain\ntheir exact variant for ownership, flight, sale and return. A purchase of A\ndoes not grant ownership of NA. Native server commands use the definition price;\nreserved aircraft remain ineligible for sale. No save inventory is rewritten.\n'
    for name in ('README.md','CHANGELOG.md','RELEASE_NOTES.md','QA.md'):
        p=SOURCE/name;p.write_text(p.read_text(encoding='utf-8-sig')+note,encoding='utf-8')
    result=subprocess.run(['pwsh','-NoProfile','-File',str(SOURCE/'build.ps1'),'-Compiler',str(ROOT/'.deps/compiler/tasks/net472/csc.exe')],capture_output=True,text=True)
    (STAGE/'build.log').write_text(result.stdout+result.stderr)
    if result.returncode:print(result.stdout+result.stderr);raise SystemExit(result.returncode)
    dll=SOURCE/'build/BepInEx/plugins/VortexConventional/VortexConventional.dll'
    receipt=dict(version='0.6.9',revision='variant-prices',variants=original['variants'],source=str(SOURCE),dll=str(dll),dllSha256=sha(dll),
        baselineDll=original['dll'],baselineSha256=original['dllSha256'],prices=dict(A=75,B=90,C=105,T=125,NA=150),
        sourceFiles={s:sha(SOURCE/s) for s in spec['sources']},resources={r['path']:sha(SOURCE/r['path']) for r in spec['resources']},installed=False,published=False)
    (STAGE/'BUILD.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print(json.dumps(dict(dll=str(dll),sha256=sha(dll),prices=receipt['prices']),indent=2))
if __name__=='__main__':main()
