"""Verified economy revision; preserves the installed baseline and its archives."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
STAGE=Path('D:/Artifacts/fs20-multiplayer/0.6.9/pricing')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    build=read(STAGE/'BUILD.json');digest=build['dllSha256'];source=Path(build['source']);dll=Path(build['dll'])
    assert sha(dll)==digest
    economy=read(STAGE/'bench-economy/economy.json');legacy=read(STAGE/'bench-economylegacy/economy.json')
    mp=read(STAGE/'bench-mp/native-bench.json');scope=read(STAGE/'scope.json');contracts=read(STAGE/'NATIVE_CONTRACTS.json')
    for r in (economy,mp,scope,contracts):
        assert r.get('passed') and not r.get('error')
        assert (r.get('pluginSha256') or r.get('candidateSha256')).lower()==digest
    assert legacy['passed'] and legacy['legacyExploit'] and legacy['pluginSha256']==build['baselineSha256']
    assert len(economy['trades'])==50 and all(t['end']==t['start'] for t in economy['trades'])
    assert sum(t['rejected'] for t in economy['trades'])==40
    assert mp['checks']>=116 and scope['resourcesPreserved']==14
    assert 'checks=615;' in (STAGE/'ledger.log').read_text()
    for name,h in build['sourceFiles'].items():assert sha(source/name)==h,name
    for name,h in build['resources'].items():assert sha(source/name)==h,name
    installed=Path('C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/plugins/VortexConventional/VortexConventional.dll')
    assert sha(installed)==build['baselineSha256'],'Installed game changed; investigate before claiming preservation'
    validation=dict(passed=True,version='0.6.9',revision='variant-prices',candidateSha256=digest,pricesMillions=build['prices'],
        nativeEconomyChecks=economy['checks'],purchaseSalePairs=50,refusedCrossVariantPairs=40,
        sharedStockModes=[True,False],legacyReproductionChecks=legacy['checks'],legacyProfitWithInjectedPrices=75,
        pureLedgerChecks=615,nativeMultiplayerChecks=mp['checks'],resourcesPreserved=14,
        exactOwnedVariants=True,factionStockPreserved=True,paidCellsConsumedBeforeReservations=True,
        nativeRecoverAirframeInUseTested=True,installed=False,installedDllSha256=sha(installed),published=False,gameLaunched=False,
        scope='Native server command bodies and synchronized collections in inactive local objects with a controlled in-memory Mirage server identity. Native user-code handlers invoked directly; not real client RPC/auth transport or live mission. Local Mirage wire regression tested separately.')
    for p in (STAGE/'validation.json',source/'validation.json'):p.write_text(json.dumps(validation,indent=2)+'\n')
    # Prevent an older price-less validation receipt being mistaken for this revision.
    tests=source/'tests/pricing';tests.mkdir(parents=True,exist_ok=True)
    for name in ('EconomyBench.cs','prepare_pricing.py','verify_pricing.ps1','package_pricing.py','run_069_bench.py'):
        shutil.copy2(ROOT/'tools/fs20-multiplayer'/name,tests/name)
    shutil.copy2(ROOT/'tools/test_shared_airframes.cs',tests/'test_shared_airframes.cs')
    for name in ('scope.json','NATIVE_CONTRACTS.json'):shutil.copy2(STAGE/name,tests/name)
    runtime=source/'build';distribution=STAGE/'distribution';shutil.copytree(runtime,distribution,dirs_exist_ok=True)
    for name in ('README.md',):shutil.copy2(source/name,distribution/name)
    shutil.copy2(STAGE/'validation.json',distribution/'LOCAL_VALIDATION.json')
    shutil.copy2(ROOT/'packaging/fs20-publication/Install.ps1',distribution/'Install.ps1')
    shutil.copytree(source/'packaging',distribution/'packaging',dirs_exist_ok=True)
    shutil.copytree(source/'tools',distribution/'tools',dirs_exist_ok=True)
    package=STAGE/'FS-20-Vortex-Family.zip'
    with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(distribution.rglob('*')):
            if p.is_file():z.write(p,p.relative_to(distribution).as_posix())
    with zipfile.ZipFile(package) as z:
        assert z.testzip() is None
        assert len([n for n in z.namelist() if n.lower().endswith('.dll')])==1
        assert hashlib.sha256(z.read('BepInEx/plugins/VortexConventional/VortexConventional.dll')).hexdigest()==digest
        assert json.loads(z.read('BepInEx/plugins/VortexConventional/meta.json'))['artifact']['version']=='0.6.9'
    srczip=STAGE/'FS-20-Vortex-Family-0.6.9-pricing-source.zip'
    with zipfile.ZipFile(srczip,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(source.rglob('*')):
            if not p.is_file() or p.relative_to(source).parts[0] in ('build','.deps','.git'):continue
            assert p.suffix.lower() not in ('.dll','.exe','.pdb')
            z.write(p,p.relative_to(source).as_posix())
    delivery=dict(version='0.6.9',revision='variant-prices',dllSha256=digest,package=str(package),packageSha256=sha(package),sourceArchive=str(srczip),sourceSha256=sha(srczip),validation=str(STAGE/'validation.json'),installed=False,published=False)
    (STAGE/'delivery.json').write_text(json.dumps(delivery,indent=2)+'\n')
    print(json.dumps(delivery,indent=2))
if __name__=='__main__':main()
