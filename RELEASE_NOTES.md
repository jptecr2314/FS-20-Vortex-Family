# FS-20 Vortex Family 0.7.2

- Revised player FCS on A/C/T/NA: continuous pitch reference, measured-rate damping and reserved differential elevon travel. Protection and native trim remain active, without the previous repeating pull-command resets. No additional low-AoA approach cutoff.
- C and T have six underwing stations. The new pair supports the tested native single missile and 250/500 kg bomb carriers. C also gains two wingtip missile stations; its NAV lights are relocated and loaded parking spacing is adjusted. Previous loadout choices are retained; new stations start empty.
- Removed the obsolete B-style wing VTOL outlet fairings from A/C, closed both fixed skin sections and corrected the surrounding lower-surface UVs. Detached skin remains owned by its original part. B/T retain their VTOL hardware.
- All 12 existing paints, demand loading, variant prices and matching-install multiplayer are retained. The separate FS-20A VS model study is not included.

Install the full package with the game closed. Keep BuiltinPaints beside the DLL. BepInEx 5 and Blueprinter 2.1.1 or newer are separate dependencies. Every multiplayer participant must use the same 0.7.2 package and matching game/mod versions.

Local native tests covered the FCS delta, station firing/release and ownership, old loadout migration, moving-surface clearance, A/C painted skins and detachment. This revision does not certify every loaded high-G/VTOL configuration or a two-PC Internet flight; multiplayer remains experimental. See QA.md for exact coverage and limits.
