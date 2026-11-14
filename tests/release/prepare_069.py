"""Freeze the current family plus the 0.6.8 network adapter as local 0.6.9.

Never changes the root production branch, the installed game or public remotes.
"""
import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STAGE = Path("D:/Artifacts/fs20-multiplayer/0.6.9")
SOURCE = STAGE / "source"
MP_BASE = Path("D:/Artifacts/fs20-multiplayer/0.6.8/source")

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def replace_once(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after, 1)

def main():
    build = (ROOT / "build-vortex-conventional.ps1").read_text(encoding="utf-8-sig")
    sources = re.findall(r"Join-Path \$PSScriptRoot '(src[^']+\.cs)'", build)
    sources = [s.replace("\\", "/") for s in sources if "Fs20SinglePlayer.cs" not in s]
    resources = [dict(path=p.replace("\\", "/"), name=n) for p, n in re.findall(
        r"'/resource:'\s*\+\s*\(Join-Path \$PSScriptRoot '([^']+)'\)\s*\+\s*',([^']+)'", build)]
    resources += [dict(path=f"assets/fs20na/Fs20NA_marks_{f}_{r}.png", name=f"Fs20NA_marks_{f}_{r}.png")
                  for f in ("bdf", "pala") for r in ("body", "wing")]
    assert len(resources) == 14 and len(sources) > 70
    managed = re.findall(r"'([^']+\.dll)'", build[build.index("$references ="):build.index("$references +=")])
    managed.append("Mirage.SocketLayer.dll")
    spec = dict(version="0.6.9", folder="VortexConventional", dll="VortexConventional.dll", parent=False,
                managed=managed, sources=sources, resources=resources)
    for name in sources + [r["path"] for r in resources]:
        target = SOURCE / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / name, target)
    frozen = {name: sha(SOURCE / name) for name in sources}
    for module in sorted((MP_BASE / "src/Multiplayer").glob("*.cs")):
        rel = "src/Multiplayer/" + module.name
        target = SOURCE / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(module, target)
        spec["sources"].append(rel)
    entry = SOURCE / "src/VortexConventionalPlugin.cs"
    text = replace_once(entry.read_text(encoding="utf-8-sig"), '"FS-20 Vortex Family", "6.3.2"', '"FS-20 Vortex Family", "0.6.9"')
    text = text.replace("Single-player guard could not be installed;", "Network compatibility hooks could not be installed;")
    entry.write_text(text, encoding="utf-8")
    path = SOURCE / "src/Multiplayer/Fs20NetworkSignature.cs"
    text = path.read_text().replace("FS20/network/2/0.6.8/ABC T", "FS20/network/2/0.6.9/A B C T NA")
    text = text.replace('!Encyclopedia.Lookup.ContainsKey(Fs20TAirframe.Key) ||',
                        '!Encyclopedia.Lookup.ContainsKey(Fs20TAirframe.Key) || !Encyclopedia.Lookup.ContainsKey(Fs20NAAirframe.Key) ||')
    text = text.replace('unit.jsonKey == Fs20TAirframe.Key)', 'unit.jsonKey == Fs20TAirframe.Key || unit.jsonKey == Fs20NAAirframe.Key)')
    path.write_text(text)
    path = SOURCE / "src/Multiplayer/Fs20NetworkSurfaces.cs"
    text = path.read_text().replace('aircraft.definition.jsonKey == Fs20TAirframe.Key);',
                                    'aircraft.definition.jsonKey == Fs20TAirframe.Key || aircraft.definition.jsonKey == Fs20NAAirframe.Key);')
    path.write_text(text)
    path = SOURCE / "src/Multiplayer/Fs20NetworkSession.cs"
    text = replace_once(path.read_text(), 'yield return AccessTools.Method(typeof(Fs20TFlightController), "PrepareControls");',
                        'yield return AccessTools.Method(typeof(Fs20TFlightController), "PrepareControls");\n                yield return AccessTools.Method(typeof(Fs20NAYawController), "Apply");')
    path.write_text(text)
    # NA's additional yaw allocator must obey the same simulation owner rule.
    # T controller/high-lift, native controls and hook cover the other NA paths.
    for name in ("restore-compiler.ps1", "AI_ASSISTANCE.md", "THIRD_PARTY_NOTICES.md"):
        shutil.copy2(ROOT / "packaging/fs20-publication" / name, SOURCE / name)
    shutil.copy2(MP_BASE / "LICENSE", SOURCE / "LICENSE")
    shutil.copy2(ROOT / "packaging/fs20-publication/build-public.ps1", SOURCE / "build.ps1")
    docs = ROOT / "tools/fs20-multiplayer/release-069"
    for name in ("README.md", "CHANGELOG.md", "RELEASE_NOTES.md", "QA.md"):
        shutil.copy2(docs / name, SOURCE / name)
    meta = json.loads((ROOT / "packaging/fs20-publication/meta.json").read_text(encoding="utf-8-sig"))
    meta["artifact"]["version"] = "0.6.9"
    (SOURCE / "meta.json").write_text(json.dumps(meta, indent=2) + "\n")
    (SOURCE / "build-spec.json").write_text(json.dumps(spec, indent=2) + "\n")
    # Optional authored rules; third-party catapult plugin is never bundled.
    for name in ("FS20C.catapults", "FS20NA.catapults", "README.md"):
        target = SOURCE / "packaging/fs20-penumbra" / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / "packaging/fs20-penumbra" / name, target)
    for name in ("install_fs20_penumbra.ps1", "check_fs20_penumbra.ps1"):
        target=SOURCE/"tools"/name
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(ROOT/"tools"/name,target)
    tests = SOURCE / "tests/multiplayer"
    tests.mkdir(parents=True, exist_ok=True)
    fixture = (ROOT / "tools/fs20-multiplayer/NativeBench.cs").read_text(encoding="utf-8-sig")
    fixture = fixture.replace('Check(plugin.GetType(Prefix+"Fs20NAAirframe")==null,"NA must be absent");',
        'Check(plugin.GetType(Prefix+"Fs20NAAirframe")!=null,"NA must be present");\n'
        '            Check(harmony.GetPatchedMethods().Any(m=>m.DeclaringType==P("Fs20NAYawController")&&m.Name=="Apply"),"NA yaw allocator needs ownership guard");\n'
        '            var na=ScriptableObject.CreateInstance<AircraftDefinition>();na.jsonKey="JP_FS20_NA";Set(aircraft,"_definition",na);\n'
        '            Check((bool)Call("Fs20NetworkSurfaces","Family",aircraft),"NA must enter remote mechanism presentation");\n'
        '            Object.DestroyImmediate(na);')
    # Actual backing field name is resolved below by the fixture helper.
    fixture = fixture.replace('Set(aircraft,"_definition",na)', 'Set(aircraft,"definition",na)')
    fixture = fixture.replace('owner-only A/C/T controllers; NA absent', 'owner-only A/C/T/NA controllers and NA mechanism presentation')
    fixture = fixture.replace('fs20.local.multiplayer.068', 'fs20.local.multiplayer.069')
    fixture = fixture.replace('private static void SurfaceMotionC(Report report,GameObject holder)',
                              'private static void SurfaceMotionC(Report report,GameObject holder,string key="JP_FS20_CTOL")')
    fixture = fixture.replace('def.jsonKey="JP_FS20_CTOL";((Unit)aircraft).definition=def;',
                              'def.jsonKey=key;((Unit)aircraft).definition=def;')
    fixture = fixture.replace('report.scenarios.Add("C native slat animation and hook deployment/overload; owner-only arresting force")',
                              'report.scenarios.Add(key+" native slat animation and hook deployment/overload; owner-only arresting force (native donor mechanism fixture)")')
    fixture = fixture.replace('SurfaceMotionC(report,holder);HostPolicy',
                              'SurfaceMotionC(report,holder);SurfaceMotionC(report,holder,"JP_FS20_NA");HostPolicy')
    (tests / "NativeBench.cs").write_text(fixture, encoding="utf-8")
    result = subprocess.run(["pwsh", "-NoProfile", "-File", str(SOURCE / "build.ps1"),
                             "-Compiler", str(ROOT / ".deps/compiler/tasks/net472/csc.exe")], capture_output=True, text=True)
    (STAGE / "build.log").write_text(result.stdout + result.stderr)
    if result.returncode:
        print(result.stdout + result.stderr)
        raise SystemExit(result.returncode)
    dll = SOURCE / "build/BepInEx/plugins/VortexConventional/VortexConventional.dll"
    receipt = dict(version="0.6.9", baseVersion="6.3.2", multiplayerBase="0.6.8", variants=["A", "B", "C", "T", "NA"],
                   source=str(SOURCE), dll=str(dll), dllSha256=sha(dll), originalSourceHashes=frozen,
                   sourceFiles={name: sha(SOURCE/name) for name in spec["sources"]},
                   resources={r["path"]: sha(SOURCE/r["path"]) for r in resources},
                   gameLaunched=False, installed=False, published=False)
    (STAGE / "BUILD.json").write_text(json.dumps(receipt, indent=2))
    print(json.dumps({k: receipt[k] for k in ("version", "variants", "dllSha256")}, indent=2))

if __name__ == "__main__":
    main()
