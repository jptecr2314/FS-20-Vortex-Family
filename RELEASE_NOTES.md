# FS-20 Vortex Family 0.5.8

Expanded internal weapon bays for FS-20A/C now carry an Auger in Inner while
keeping every original Outer option available on the same side:

- AAM-29 Scythe, MMR-S3, IRM-S2 and AAM-36 Scimitar.
- PAB-125 x2 and PAB-80LR x2.

Rack positions and connected supports adapt to the combined load. Existing
presets keep their Outer selection; there are no extra Auger-only selectors.

This update also includes the changes developed since 0.4.1: enlarged bays
and continuous doors, a recessed FS-20C tailhook compartment with departure
interlocks, revised interior UVs/native-map finish, removal of remaining VTOL
hardware from A/C, and slat hinges adjusted to the C's larger wings.

**Known visual compromise:** some weapon meshes can pass through the bay
structure during guided release. Native damage/collision handling stays active.
Paired Outer rails keep the active projectile spawn farther from the airframe.
All 24 A/C/side/Outer stored combinations and post-rail native projectile
geometry were checked offline; 48,276 option/migration/support/geometry checks
passed. See [QA.md](https://github.com/jptecr2314/FS-20-Vortex-Family/blob/v0.5.8/QA.md)
for the validation record and remaining gameplay checks.

Single-player experimental prerelease for Nuclear Option 0.34.6. Install
BepInEx 5 and Blueprinter 2.1.1 separately, close the game, then extract the
ZIP's BepInEx folder into the game directory or import the ZIP into NOMM.
The aircraft family and optional shared A/B/C stock remain one plugin. Online
sessions are blocked. [Manual Tailhook](https://github.com/jptecr2314/NO-Manual-Tailhook)
is a separate optional mod.

Author: Jpgamer4045d. MIT license. AI assistance is declared in
AI_ASSISTANCE.md. No licensed game assets or dependency binaries are bundled.
