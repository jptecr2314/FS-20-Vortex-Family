# FS-20-Vortex-Family 0.5.10

0.5.8 restores every original Outer loadout alongside Auger in Inner, with load-specific rack poses and connected supports. Guided release may visually intersect the airframe; the author accepted this compromise. Native active-projectile geometry was checked after the rail. See QA.md for evidence and gameplay scope.

Single-player FS-20A and FS-20C variants alongside the stock B, with a native-style selector, C tailhook and optional shared A/B/C stock in one plugin. C: 110/165 kN; A/C: up to +11 G. Online sessions are blocked.

**Experimental single-player prerelease. Multiplayer is intentionally blocked.**
Author: Jpgamer4045d. See AI_ASSISTANCE.md, THIRD_PARTY_NOTICES.md and QA.md.
Not affiliated with or endorsed by Shockfront Studios. Licensed game required.

## Related independent mod

[NO Manual Tailhook](https://github.com/jptecr2314/NO-Manual-Tailhook) is maintained
in its own repository, with its own source, build and downloads. It adds manual
deploy/retract control and an optional FS-20C adapter. Install it separately.

## Aircraft comparison

![FS-20A, FS-20B and FS-20C — top-view comparison](docs/images/fs20-abc-comparison.png)

![FS-20 family — textured 3D renders](docs/images/fs20-abc-renders.png)

Same-scale model renders with base textures and nominal mod specifications.

## Identifier and NOMM updates

The plugin ID is `fs20vortexfamily`. Existing NOMM users: update the
installed entry, launch the game once, close it, then refresh NOMM. Do not
install a second copy. The previous ID remains in the catalog only as an
update bridge. Your previous settings are retained; see RELEASE_NOTES.md.

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
The build uses the ordered source/resource list in build-spec.json and deterministic compilation. The exported 0.5.8 source build was checked against the distributed DLL. Its dependency hashes are part of
local release evidence; different game/library versions may produce different DLLs.

## Compatibility and support

Target game: 0.34.6. Requires BepInEx 5 and Blueprinter 2.1.1.
This release supports single-player only. The multiplayer menu, remote client
connections, non-Offline hosting and listening transport servers are blocked.
Native local Offline sessions remain permitted. Disable the mod and restart
the game to play online. No host/client compatibility is claimed.

Tests: ./test-offline.ps1 and ./test-stock-option.ps1 -GamePath 'PATH_TO_GAME'.
The second command checks actual configuration and single-player guard prefixes
against local game libraries, without opening sockets or launching the game.
Gameplay features have been tested in-game by the author during development.
Automated checks cover flight/control policies, inventory, saved settings and
single-player guard prefixes. See QA.md for the validation record.
For bug reports include version, minimal mod list, steps, and a redacted log;
do not send account credentials or personal paths. See .github/ISSUE_TEMPLATE.


## Internal-bay revision

Select **GPO-2P Auger** in the existing **Weapon Bays (Inner)** loadout group. The original paired hardpoints carry one Auger per bay, two total. The Outer group remains available with Auger: AAM-29, MMR-S3, IRM-S2, AAM-36, PAB-125 x2 and PAB-80LR x2. Paired poses keep the stored models separated. Some guided release paths intersect the aircraft visually; native projectile damage/collision handling remains active. There are no extra Auger-only fields. Retract the landing gear before release.

Paired Auger selections saved in the two old 0.5.0/0.5.1 fields migrate into the original inner group. A previous one-sided Auger selection must be reselected; it is not silently doubled. Native legacy loadout keys and the C hook slot remain in place.

A keeps 3,100 kg internal fuel; C keeps 2,800 kg through internal repartition. Nominal dry masses: A 10,200 kg; C 9,720 kg, or 9,820 kg with the optional 100 kg moving hook. The fixed recessed well is already included in C dry mass.

C retains the native tailhook selector, takeoff/go-around lock and cable interface. Its cover opens before hook deployment. An overload above 900 kN releases the arrestor and disables the hook until respawn. These are simulation design limits; cable-stop performance remains for the author to test in game.
