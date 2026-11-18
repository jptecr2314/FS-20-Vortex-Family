"""Verify and optionally package the isolated demand-loaded 0.7.0 candidate.

Never installs, publishes, changes tags, or writes to the author's game.
"""
import argparse
import hashlib
import io
import json
import shutil
import zipfile
from pathlib import Path

def sha(data):
    return hashlib.sha256(data).hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--stage',type=Path,default=Path('D:/Artifacts/fs20-publication/0.7.0'))
    parser.add_argument('--verify-only',action='store_true')
    args=parser.parse_args();stage=args.stage;source=stage/'source';qa=stage/'on-demand-20261118'
    build=json.loads((stage/'BUILD.json').read_text(encoding='utf-8-sig'));dll=Path(build['dll'])
    assert sha(dll.read_bytes())==build['dllSha256']
    delta=json.loads((qa/'compiled-delta.json').read_text(encoding='utf-8-sig'))
    assert delta['passed'] and delta['candidateSha256'].lower()==build['dllSha256']
    assert json.loads((qa/'paint-byte-equivalence.json').read_text())['compressedMipBytes']==117441072
    native=json.loads((qa/'lod-fix/native-final/streaming.json').read_text())
    assert native['completed'] and not native['error'] and native['assertions']==922
    assert json.loads((qa/'installer4-small/installer.json').read_text(encoding='utf-8-sig'))['passed']
    assert json.loads((qa/'lod-fix/lod-color-verification.json').read_text())['passed']
    for role in ['server','client']:
        assert json.loads((qa/'lod-fix/native-network-final'/f'{role}.json').read_text())['completed']
    contracts=json.loads((stage/'bench-mp/native-bench.json').read_text())
    assert contracts['passed'] and contracts['pluginSha256']==build['dllSha256']
    plugin=source/'build/BepInEx/plugins/VortexConventional'
    for name in ['README.md','QA.md','RELEASE_NOTES.md','CHANGELOG.md','meta.json','THIRD_PARTY_NOTICES.md','AI_ASSISTANCE.md','LICENSE']:
        shutil.copy2(source/name,plugin/name)
    files={p.relative_to(source/'build').as_posix():p.read_bytes() for p in plugin.iterdir() if p.is_file()}
    paints=source/'assets/fs20-paints/BuiltinPaints'
    catalog=json.loads((paints/'catalog.json').read_text())
    names={catalog['basisFile']:catalog['basisSha256']}
    for paint in catalog['paints']:names[paint['bundleFile']]=paint['bundleSha256']
    assert len(names)==13 and len(catalog['paints'])==12
    for name,expected in names.items():
        data=(paints/name).read_bytes();assert sha(data).lower()==expected.lower()
        files['BepInEx/plugins/VortexConventional/BuiltinPaints/'+name]=data
    files['BepInEx/plugins/VortexConventional/BuiltinPaints/catalog.json']=(paints/'catalog.json').read_bytes()
    files['Install.ps1']=(source/'Install.ps1').read_bytes()
    buffer=io.BytesIO()
    with zipfile.ZipFile(buffer,'w') as archive:
        for name,data in sorted(files.items()):archive.writestr(zipfile.ZipInfo(name,(2026,11,18,0,0,0)),data,compress_type=zipfile.ZIP_DEFLATED)
    buffer.seek(0)
    with zipfile.ZipFile(buffer) as archive:
        assert archive.testzip() is None
        assert set(archive.namelist())==set(files)
        for name,data in files.items():assert sha(archive.read(name))==sha(data)
    payload=buffer.getvalue();archive_path=qa/'FS-20-Vortex-Family-demand-candidate.zip'
    if not args.verify_only:
        assert not archive_path.exists(),'Preserve earlier candidate package.'
        assert shutil.disk_usage(qa).free>len(payload)+64*1024*1024,'Not enough D: space to safely write package.'
        archive_path.write_bytes(payload)
    receipt={'passed':True,'version':'0.7.0','dllSha256':build['dllSha256'],'zipSha256':sha(payload),'zipBytes':len(payload),'verifiedInMemoryOnly':args.verify_only,'archive':str(archive_path) if not args.verify_only else None,'files':{name:sha(data) for name,data in sorted(files.items())},'installed':False,'published':False,'publicationReady':True}
    (qa/'package-plan.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in receipt.items() if k!='files'},indent=2))

if __name__=='__main__':main()
