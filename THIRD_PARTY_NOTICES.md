# Third-party dependencies and asset boundary

This package includes the author's code, authored vapor shader and modified
FS-20 geometry/UV resources used by the integrated T and internal surfaces.
Game-derived geometry and paint references retain Shockfront Studios' original
rights; the MIT license covers the author's contributions only. A licensed
game installation supplies the remaining runtime assets.
No Nuclear Option/Unity assemblies, Blender references, BepInEx, Harmony,
Blueprinter or compiler toolchain are distributed.
No ownership or redistribution permission over those game assets is asserted.

- Nuclear Option and its installed content: Shockfront Studios, original terms.
- Unity and Mirage assemblies: references from the user's installed game, not bundled.
- BepInEx 5: https://github.com/BepInEx/BepInEx (LGPL-2.1, upstream license).
- Harmony: https://github.com/pardeike/Harmony (MIT, upstream license).
- Blueprinter 2.1.1: nikkorap, https://github.com/nikkorap/NOBlueprinter-Releases.
  Required by the aircraft family; installed separately, not redistributed.
- Microsoft.Net.Compilers.Toolset 4.8.0: Microsoft/Roslyn (MIT), fetched separately
  from NuGet by the optional restore script and verified by SHA-256.

The unrelated NeutralQoL/MPL cargo code and other product modules in the private
workspace are deliberately not part of either FS-20 source export or package.
The plugin license applies only to its author's contributions. A future addition
of third-party assets/code requires its own provenance, license and permissions.
