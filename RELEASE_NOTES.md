# FS-20 Vortex Family 0.7.0

Five variants remain in the existing family selector: A, native B, C, T and NA.

- Dedicated A/T/NA UV paints with native BDF/PALA faction insignia: two per faction per variant. A uses air-force markings. Body and extension atlases are 2K BC7 with mipmaps; 4K authoring masters remain separate.
- Optional paints load asynchronously from per-paint bundles. Three default coats remain ready; native handles and renderer ownership retain paints on live, hidden and detached parts. Idle optional coats are reclaimed without forcing global GC.
- Corrected distant body color through the native vertex-color shader, with shared color-only mesh copies. Native geometry, shading/alpha, detailed UVs, colliders and damage owners are preserved.
- Includes the previously tested A/C/NA AI calibration and NA gun-capture refinement; accepted T flight tuning remains intact. Load-aware takeoff estimates and variant-specific pricing/ownership remain available.
- Matching-install multiplayer enabled by default, with one-time migration from the previous disabled default. Native Steam/password/build authentication, host policy and simulation authority remain required.

## Update

Extract the complete BepInEx folder or use Install.ps1 with the game closed. Keep BuiltinPaints beside VortexConventional.dll: copying only the DLL is insufficient. Preserve custom .fs20paint packs and configuration. The installer validates bundle hashes, backs up replaced files and restores them if copying fails. BepInEx 5 and Blueprinter 2.1.1 are separate dependencies.

## Validation and scope

183 native contract checks, 922 checks in the private rendered game and 31 checks with separate native Steam server/client processes passed. The latter used one PC; two-PC Internet flight, latency/late joins and every weapon/mission combination remain unverified. This is a multiplayer prerelease. See QA.md for the measured limits. The 112→32 MiB figure describes startup compressed paint payload, not whole-game VRAM or a promised FPS gain.

The optional creator kit includes editable templates, new-surface UV references and a standalone .fs20paint exporter. It is a separate download, not required for players. Standard B/C native paints remain unchanged. Historical releases and download links are preserved.
