# FS-20-Vortex-Family 0.4.0

Adds FS-20A and FS-20C alongside the stock B, with a native-style selector, C tailhook and optional shared A/B/C stock in one plugin. C: 110/165 kN; A/C: up to +11 G.

**Local prerelease candidate. Live UI/flight/recovery and multiplayer acceptance are pending.**
Author: Jpgamer4045d. See AI_ASSISTANCE.md, THIRD_PARTY_NOTICES.md and QA.md.
Not affiliated with or endorsed by Shockfront Studios. Licensed game required.

## Install

1. Install BepInEx 5 for the game; install Blueprinter 2.1.1 for Vortex Family.
2. Close the game. Remove the old Fs20SharedAirframes.dll before upgrading; its features are now integrated.
3. Extract this mod's BepInEx folder into the Nuclear Option game directory,
   or import its ZIP into NOMM. Do not place a second copy of the same DLL elsewhere.
4. Restart the game yourself. Check BepInEx/LogOutput.log for the mod/version.
5. Remove only BepInEx/plugins/VortexConventional to uninstall. Keep dependencies
   needed by other mods. The optional Install.ps1 script backs up the previous DLL and removes the legacy standalone DLL without launching the game. Saved missions using A/C need Vortex Family to load.

## Shared stock (F1)

With BepInEx Configuration Manager installed, press F1 and open FS-20 Vortex
Family > Inventory > Shared airframe stock. Default: enabled. All three
variants remain selectable in either mode. Enabled: 48 B cells can supply
A/B/C, and taking one leaves 47. Disabled: each variant uses its own native
stock and ownership counts. Switching modes does not duplicate, convert or
redistribute existing cells. The setting is saved in the plugin config.
The old standalone Shared Airframes plugin must not remain installed.

## Build source

Windows, PowerShell 7, .NET Framework 4.7.2 or later, licensed game with BepInEx 5.
Set NUCLEAR_OPTION_PATH or pass -GamePath. restore-compiler.ps1 downloads the
pinned Microsoft compiler from NuGet and verifies its SHA-256; it is not bundled.

    ./build.ps1 -GamePath 'PATH_TO_GAME'

All aircraft and inventory code compiles into one DLL. Outputs are under build/BepInEx/plugins/VortexConventional.
The build uses deterministic compilation. Its dependency hashes are part of
local release evidence; different game/library versions may produce different DLLs.

## Compatibility and support

Target game: 0.34.6. BepInEx 5, Blueprinter 2.1.1 (family), matching plugin
versions and stock settings on host and clients. **Not a client-only mod and not yet multiplayer-certified.**
Do not use on public servers before the host/refusal cases in QA.md pass.
Native server checks remain, but no completed host-consent handshake test is claimed.
Inventory toggle test: ./test-stock-option.ps1 -GamePath 'PATH_TO_GAME' reads local game libraries without launching the game.
Opening sources or installing the package is not proof of registration, safe
multiplayer behavior, flight stability, low RCS or carrier-landing performance.
For bug reports include version, minimal mod list, steps, and a redacted log;
do not send account credentials or personal paths. See .github/ISSUE_TEMPLATE.
