# FS-20 Vortex Family 6.0.7

Four FS-20 variants in one aircraft family, one selector and one plugin.
Open the FS-20B tile to select A, B, C or T. Shared airframe stock can be
enabled or disabled from the mod configuration menu.

| Aircraft | Configuration |
|---|---|
| FS-20A | Conventional-flight variant with the original wing outline and additional internal fuel. |
| FS-20B | The game's original VTOL aircraft, retained in the family selector. |
| FS-20C | Conventional-flight variant with wider wings and a tailhook. |
| FS-20T | Integrated-delta VTOL variant, articulated slats, composite mass model and separately damageable wing sections. |

The T is included by default. It no longer needs the old **Enable experimental
variant** setting. Its aircraft key remains `JP_FS20_T`; saved aircraft and
loadout references retain the same keys. The C and T include volumetric wing
condensation, moving slat/elevon coverage and corrected nose-to-tail density
motion in forward flight. Condensation uses native aerodynamic loads and an
explicit assumed humidity setting; it does not change aircraft forces.

## Installation

Requires a licensed Nuclear Option installation, BepInEx 5 and Blueprinter
2.1.1. Target game version: 0.34.6. This is a single-player package.

1. Close the game and update your existing FS-20 Vortex Family installation.
2. Import the ZIP into NOMM, or extract its `BepInEx` folder into the game.
3. Keep one `VortexConventional.dll`. The old standalone Shared Airframes DLL
   is superseded by the integrated stock controls.
4. Start the game and check for `FS-20 Vortex Family 6.0.7` in the BepInEx log.

The canonical plugin ID is `fs20vortexfamily`. Older configurations are copied
only when the canonical configuration is absent. Previous settings are kept.
The installation metadata can migrate from `br.japensou.nuclearoption.vortexctol`
on startup; update the existing NOMM entry rather than installing a second copy.

## Inventory configuration

With BepInEx Configuration Manager, open **F1 > FS-20 Vortex Family > Inventory
> Shared airframe stock**. All four variants remain selectable in either mode.

- Enabled: A/B/C/T consume a single pool. With 48 B airframes and no other
  stock, taking any variant leaves 47 available to the family.
- Disabled: each variant uses its native stock and ownership counters.

Switching modes does not duplicate or redistribute existing aircraft. Existing
preferences are retained. The obsolete T experimental-enable flag is ignored.

## Source and validation

Build from the source export with PowerShell 7:

```powershell
./build.ps1 -GamePath 'PATH_TO_NUCLEAR_OPTION'
./test-family.ps1 -GamePath 'PATH_TO_NUCLEAR_OPTION'
```

The build uses the ordered source/resource manifest in `build-spec.json`.
Game libraries and the compiler are not distributed. `restore-compiler.ps1`
downloads the pinned compiler and verifies its package hash when needed.
See `QA.md` for the exact validation scope and inherited gameplay evidence.

Multiplayer remains disabled while this aircraft plugin is enabled. The
separate [NO Manual Tailhook](https://github.com/jptecr2314/NO-Manual-Tailhook)
plugin remains optional and is not bundled.

Author: **Jpgamer4045d**. Code license: MIT. AI assistance is declared in
`AI_ASSISTANCE.md`. Game-derived assets retain their original ownership;
see `THIRD_PARTY_NOTICES.md`. Not affiliated with Shockfront Studios.
