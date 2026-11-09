# FS-20-Vortex-Family 0.3.11

Adds the runway FS-20A and enlarged-wing FS-20C alongside the stock B. C: 110/165 kN; A/C: up to +11 G. Native-style variant selector and optional covered C tailhook.

**Local prerelease candidate. Live UI/flight/recovery and multiplayer acceptance are pending.**
Author: Jpgamer4045d. See AI_ASSISTANCE.md, THIRD_PARTY_NOTICES.md and QA.md.
Not affiliated with or endorsed by Shockfront Studios. Licensed game required.

## Install

1. Install BepInEx 5 for the game; install Blueprinter 2.1.1 for Vortex Family.
2. For Shared Airframes, install Vortex Family 0.3.11 first. Close the game.
3. Extract this mod's BepInEx folder into the Nuclear Option game directory,
   or import its ZIP into NOMM. Do not place a second copy of the same DLL elsewhere.
4. Restart the game yourself. Check BepInEx/LogOutput.log for the mod/version.
5. Remove only BepInEx/plugins/VortexConventional to uninstall. Keep dependencies
   needed by other mods. Saved missions using A/C need Vortex Family to load.

## Build source

Windows, PowerShell 7, .NET Framework 4.7.2 or later, licensed game with BepInEx 5.
Set NUCLEAR_OPTION_PATH or pass -GamePath. restore-compiler.ps1 downloads the
pinned Microsoft compiler from NuGet and verifies its SHA-256; it is not bundled.

    ./build.ps1 -GamePath 'PATH_TO_GAME'

Shared Airframes additionally accepts -VortexAssembly 'PATH_TO_VortexConventional.dll'.
Build Vortex Family first. Outputs are under build/BepInEx/plugins/VortexConventional.
The build uses deterministic compilation. Its dependency hashes are part of
local release evidence; different game/library versions may produce different DLLs.

## Compatibility and support

Target game: 0.34.6. BepInEx 5, Blueprinter 2.1.1 (family), matching family/shared
versions on host and clients. **Not a client-only mod and not yet multiplayer-certified.**
Do not use on public servers before the host/refusal cases in QA.md pass.
Native server checks remain, but no completed host-consent handshake test is claimed.
Opening sources or installing the package is not proof of registration, safe
multiplayer behavior, flight stability, low RCS or carrier-landing performance.
For bug reports include version, minimal mod list, steps, and a redacted log;
do not send account credentials or personal paths. See .github/ISSUE_TEMPLATE.
