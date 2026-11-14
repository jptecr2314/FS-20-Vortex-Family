"""Package the verified 0.6.9 artifact without installing or publishing it."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STAGE = Path('D:/Artifacts/fs20-multiplayer/0.6.9')

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))

def main():
    build=read(STAGE/'BUILD.json');dll=Path(build['dll']);source=Path(build['source'])
    assert build['version']=='0.6.9' and build['variants']==['A','B','C','T','NA']
    digest=sha(dll)
    assert digest==build['dllSha256']
    receipts={
        'multiplayer':read(STAGE/'bench-mp/native-bench.json'),
        'na':read(STAGE/'bench-na/na.json'),
        'dynamic':read(STAGE/'bench-dynamic/dynamic-takeoff.json'),
        'scope':read(STAGE/'scope.json'),
        'contracts':read(STAGE/'NATIVE_CONTRACTS.json'),
    }
    for key,r in receipts.items():
        assert (r.get('passed') or r.get('completed')) and not r.get('error'), key
        assert (r.get('pluginSha256') or r.get('candidateSha256')).lower()==digest,key
    assert receipts['multiplayer']['checks']>=116
    assert receipts['na']['checks']>=40000
    assert 0<receipts['na']['parts']<256
    dynamic=receipts['dynamic']
    assert dynamic['checks']==3520 and len(dynamic['cases'])==660 and len(dynamic['cursorLimits'])==72
    assert sum(len(r['samples']) for r in dynamic['cursorLimits'])==1512
    for name,h in build['sourceFiles'].items(): assert sha(source/name)==h, name
    for name,h in build['resources'].items(): assert sha(source/name)==h, name
    installed=Path('C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/plugins/VortexConventional/VortexConventional.dll')
    assert sha(installed).upper()=='A0E8960C949712B419F3167BD736D0A9E649FB13DD1B5975E9D9A53862C118D3'
    validation=dict(passed=True,version='0.6.9',candidateSha256=digest,variants=build['variants'],
                    nativeMultiplayerChecks=receipts['multiplayer']['checks'],nativePatchedMethods=receipts['multiplayer']['patches'],
                    naFactoryChecks=receipts['na']['checks'],naParts=receipts['na']['parts'],dynamicTakeoffChecks=dynamic['checks'],
                    dynamicMainCases=660,cursorWindSweepCases=1512,preservedResources=14,
                    installedDllSha256=sha(installed),installed=False,published=False,gameLaunched=False,
                    realSteamSessionTested=False,nativeTakeoffMissionTested=False,
                    limits=['In-memory native Mirage exchange, not two-installation Steam flight.',
                            'Inactive NA factory/clone validation, not live client appearance/firing.',
                            'Reduced longitudinal takeoff model; advisory, not launch clearance.'])
    (STAGE/'validation.json').write_text(json.dumps(validation,indent=2)+'\n')
    (source/'validation.json').write_text(json.dumps(validation,indent=2)+'\n')
    # Keep a reproducible copy of the scoped verification/preparation tools.
    tests=source/'tests/release';tests.mkdir(parents=True,exist_ok=True)
    for name in ('prepare_069.py','run_069_bench.py','verify_069_scope.ps1','verify_contracts.ps1','package_069.py'):
        shutil.copy2(ROOT/'tools/fs20-multiplayer'/name,tests/name)
    for name in ('ReviewDynamicTakeoff.cs','ReviewTakeoffDistances.cs'):
        shutil.copy2(ROOT/'tools'/name,tests/name)
    runtime=source/'build';distribution=STAGE/'distribution'
    shutil.copytree(runtime,distribution,dirs_exist_ok=True)
    shutil.copy2(ROOT/'packaging/fs20-publication/Install.ps1',distribution/'Install.ps1')
    shutil.copy2(source/'README.md',distribution/'README.md')
    shutil.copy2(STAGE/'validation.json',distribution/'LOCAL_VALIDATION.json')
    shutil.copytree(source/'packaging',distribution/'packaging',dirs_exist_ok=True)
    shutil.copytree(source/'tools',distribution/'tools',dirs_exist_ok=True)
    package=STAGE/'FS-20-Vortex-Family.zip'
    with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(distribution.rglob('*')):
            if p.is_file(): z.write(p,p.relative_to(distribution).as_posix())
    with zipfile.ZipFile(package) as z:
        assert z.testzip() is None
        assert len([p for p in z.namelist() if p.lower().endswith('.dll')])==1
        assert hashlib.sha256(z.read('BepInEx/plugins/VortexConventional/VortexConventional.dll')).hexdigest()==digest
        assert json.loads(z.read('BepInEx/plugins/VortexConventional/meta.json'))['artifact']['version']=='0.6.9'
        assert 'packaging/fs20-penumbra/FS20NA.catapults' in z.namelist()
    source_archive=STAGE/'FS-20-Vortex-Family-0.6.9-source.zip'
    with zipfile.ZipFile(source_archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(source.rglob('*')):
            if p.is_file() and p.relative_to(source).parts[0] not in ('build','.deps','.git'):
                assert p.suffix.lower() not in ('.dll','.exe','.pdb')
                z.write(p,p.relative_to(source).as_posix())
    delivery=dict(version='0.6.9',variants=build['variants'],status='prepared local candidate; real multiplayer session pending',
                  dllSha256=digest,package=str(package),packageSha256=sha(package),
                  sourceArchive=str(source_archive),sourceSha256=sha(source_archive),
                  validation=str(STAGE/'validation.json'),installed=False,published=False,gameLaunched=False)
    (STAGE/'delivery.json').write_text(json.dumps(delivery,indent=2)+'\n')
    print(json.dumps(delivery,indent=2))

if __name__=='__main__': main()
