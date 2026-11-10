# NO Manual Tailhook 0.1.1

Optional plugin by **Jpgamer4045d** for manual control of an equipped Nuclear
Option tailhook. All HUD/log messages and configuration descriptions are English.

- **Left Ctrl+H**: deploy or retract. Retracting a caught hook releases its cable.
- **Left Ctrl+Left Shift+H**: restore automatic control.
- **F1 > NO Manual Tailhook**: change shortcuts with BepInEx Configuration Manager.

Automatic control remains active until the first manual command and returns
when you change aircraft. Only the locally piloted aircraft is controlled;
AI aircraft retain their own behavior. No hook hardware is added. The FS-20C
adapter retains native hook-bay door sequencing.

## Download and install

[Download NO-Manual-Tailhook-0.1.1.zip](https://github.com/jptecr2314/FS-20-Vortex-Family/releases/download/manual-tailhook-v0.1.1/NO-Manual-Tailhook-0.1.1.zip).

Requires BepInEx 5. Close the game, then extract the ZIP's `BepInEx` folder into
the game directory. This installs `BepInEx/plugins/ManualTailHook`. Do not install
a second copy of the DLL. Remove only that folder to uninstall.

Vortex Family is optional; this download contains only Manual Tailhook.
It does not update the Vortex aircraft plugin. Use single-player; multiplayer
synchronization is unverified and no network protocol is added by this plugin.
Target: Nuclear Option 0.34.6, Steam build 26327221.

## Build and verify

From the repository root, with PowerShell and .NET Framework 4.7.2 or later:

```powershell
./addons/manual-tailhook/build.ps1 -GamePath 'PATH_TO_GAME'
./addons/manual-tailhook/test-offline.ps1 -GamePath 'PATH_TO_GAME'
```

The build restores the pinned compiler through the root restore script unless
`-CompilerPath` is supplied. Output: `build/NO-Manual-Tailhook-0.1.1.zip`.
Licensed game assemblies and BepInEx dependencies must already be installed;
they are never bundled. The test harness uses real Harmony with simulated
game and Unity objects; native API contracts are inspected separately.

Baseline 0.1.0 passed 20 offline assertions. Version 0.1.1 compiled against
the installed game; English DLL strings, unchanged configuration bindings and
package/installed hashes were verified. Windows Application Control blocked
rerunning the harness for 0.1.1; the protection was preserved.
Keyboard/HUD behavior, animation and physical arrestment remain untested in-game.
The baseline Vortex Family gameplay record does not validate this new plugin.

Source and own contributions use the repository's MIT license. Substantial AI
assistance is declared in `AI_ASSISTANCE.md`; see `THIRD_PARTY_NOTICES.md`.
