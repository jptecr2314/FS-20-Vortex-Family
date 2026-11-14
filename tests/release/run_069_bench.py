"""Local, hidden Unity checks, with private imports restored afterwards."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STAGE = Path('D:/Artifacts/fs20-multiplayer/0.6.9')
GAME = Path('C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--mode', choices=('MP', 'NA', 'Dynamic'), required=True)
    mode = parser.parse_args().mode
    project = Path('D:/Build/fs20-multiplayer-068' if mode == 'MP' else 'D:/Build/nuclear-option-qol/fs20na-bench')
    processes = subprocess.check_output(['pwsh', '-NoProfile', '-Command',
        "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Select-Object CommandLine | ConvertTo-Json -Compress"], text=True).strip()
    if processes:
        items = json.loads(processes)
        if isinstance(items, dict): items = [items]
        assert not any(str(project).replace('\\','/').lower() in (i.get('CommandLine') or '').replace('\\','/').lower() for i in items), 'Private project is in use'
    assert sha(project/'Packages/nuclearoption/Assembly-CSharp.dll') == sha(GAME/'NuclearOption_Data/Managed/Assembly-CSharp.dll')
    build = json.loads((STAGE/'BUILD.json').read_text())
    dll = Path(build['dll'])
    assert sha(dll) == build['dllSha256']
    output = STAGE/('bench-'+mode.lower())
    output.mkdir(parents=True, exist_ok=True)
    if mode in ('NA','Dynamic'):
        for name in ('native-meshes.bin','native-joints.json','native-uvs.bin','native-normals.bin','native-gun-doors.json'):
            ref = Path('D:/Artifacts/fs20na-model/revision-24')/name
            assert ref.exists(), ref
            target = STAGE/name
            if not target.exists(): shutil.copy2(ref,target)
    target = project/('Assets/PrivateRuntime/VortexConventional.dll' if mode=='MP' else 'Assets/FS20Validation/PrivateRuntime/VortexConventional.dll')
    changes = {target: dll}
    if mode=='MP':
        changes[project/'Assets/MPBench/Editor/NativeBench.cs'] = STAGE/'source/tests/multiplayer/NativeBench.cs'
        method = 'FS20MultiplayerBench.NativeBench.Run'
    else:
        na_editor=ROOT/'unity/Blueprinter-Editor/Assets/FS20Validation/Editor'
        names=([p.name for p in na_editor.glob('ReviewNA*.cs')]+['RenderNASkinRuntime.cs'] if mode=='NA' else ['ReviewDynamicTakeoff.cs','ReviewTakeoffDistances.cs'])
        for name in names:
            source = ROOT/('unity/Blueprinter-Editor/Assets/FS20Validation/Editor' if mode=='NA' else 'tools')/name
            changes[project/'Assets/FS20Validation/Editor'/name] = source
        method = 'FS20Validation.CompareVortexAirframes.'+('ReviewNA' if mode=='NA' else 'ReviewDynamicTakeoff')
    original = {p: p.read_bytes() if p.exists() else None for p in changes}
    env = dict(os.environ,FS20_MP_OUTPUT=str(output),FS20_MP_DLL=str(target),
               FS20_TOOLS_OUTPUT=str(output),FS20_TOOLS_PLUGIN=str(target),FS20_GAME=str(GAME),
               FS20_RELEASE_SOURCE=str(STAGE/'source'),
               FS20_DYNAMIC_GRIDS=str(STAGE/'source/assets/fs20-ai/Fs20TakeoffGrids.json'))
    env.pop('FS20_TOOLS_RENDER',None)
    log = output/('unity-'+datetime.now().strftime('%Y%m%d-%H%M%S')+'.log')
    unity = 'D:/SDK/Unity/2022.3.62f2/Editor/Unity.exe'
    # The NA palette path reads non-readable native textures through a local
    # render target. Null graphics silently produces invalid comparison pixels.
    # Batchmode remains hidden; it does not open the game or an editor window.
    args = [unity,'-batchmode']+([] if mode=='NA' else ['-nographics'])+['-quit','-projectPath',str(project),'-executeMethod',method,'-logFile',str(log)]
    try:
        for p, src in changes.items(): shutil.copy2(src,p)
        if mode=='NA':
            fixture=project/'Assets/FS20Validation/Editor/ReviewNA.cs'
            text=fixture.read_text(encoding='utf-8-sig')
            before='foreach(string key in new[]{"TailHook_Multirole1","bomb_penetrator1_internalx2"})NAList(enc,"weaponMounts").Add(Asset(key));'
            after='''foreach(string key in new[]{"TailHook_Multirole1","bomb_penetrator1_internalx2"}) {
                    var mount=Asset(key);
                    if(key=="TailHook_Multirole1") {
                        mount=Object.Instantiate(mount);
                        var hookFixturePrefab=Object.Instantiate((GameObject)Member(mount,"prefab"),holder.transform,false);Set(mount,"prefab",hookFixturePrefab);
                        var hook=hookFixturePrefab.GetComponent(Utilities.game_type("TailHook"));
                        var arms=hookFixturePrefab.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="hook").ToArray();
                        if(arms.Length!=1)throw new Exception("Private native hook hydration is ambiguous");
                        Set(hook,"hook",arms[0]);
                    }
                    NAList(enc,"weaponMounts").Add(mount);
                }'''
            assert text.count(before)==1
            fixture.write_text(text.replace(before,after),encoding='utf-8')
            factions=project/'Assets/FS20Validation/Editor/ReviewNAFactionLiveries.cs'
            text=factions.read_text(encoding='utf-8-sig')
            before='string root=Directory.GetParent(Application.dataPath).Parent.Parent.FullName;'
            assert text.count(before)==1
            factions.write_text(text.replace(before,'string root=Environment.GetEnvironmentVariable("FS20_RELEASE_SOURCE");'),encoding='utf-8')
        receipt=output/({'MP':'native-bench.json','NA':'na.json','Dynamic':'dynamic-takeoff.json'}[mode])
        if receipt.exists(): receipt.rename(receipt.with_name(receipt.stem+'-'+datetime.now().strftime('%Y%m%d-%H%M%S')+'.json'))
        generated=project/'Library/ScriptAssemblies/Assembly-CSharp.dll'
        if generated.exists(): generated.chmod(0o666)
        bootstrap=list(args);bootstrap[bootstrap.index('-executeMethod')+1]='Blueprinter.GameAssemblySync.Synchronize'
        bootstrap[bootstrap.index('-logFile')+1]=str(output/'bootstrap.log')
        for command in (bootstrap,args):
            result=subprocess.run(command,env=env,creationflags=subprocess.CREATE_NO_WINDOW)
            if result.returncode: raise RuntimeError(f'Unity exited {result.returncode}; inspect {output}')
        data=json.loads(receipt.read_text())
        assert (data.get('completed') or data.get('passed')) and not data.get('error'), data.get('error')
        assert data['pluginSha256'].lower()==sha(dll)
        print(json.dumps(dict(mode=mode,checks=data['checks'],sha256=sha(dll),passed=True)))
    finally:
        for p,data in original.items():
            if data is None:
                if p.exists(): p.unlink()
            else: p.write_bytes(data)
        (output/'private-restoration.json').write_text(json.dumps({str(p): sha(p) if p.exists() else None for p in original},indent=2))

if __name__=='__main__': main()
