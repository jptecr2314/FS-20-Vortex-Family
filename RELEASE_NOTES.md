# FS-20 Vortex Family 0.7.3

- A gains wingtip missile rails with relocated NAV lights. A/C rails now offer compatible single-round air-to-air missiles from installed weapon mods, filter out ship/ground-only launchers and duplicate visible choices, and align the round with the rail on attachment.
- A/C wingtips have a 240 kg limit per rail, including the missile and adapter. The limit applies to selection, AI, saved loads, spawning and native server admission. Previously saved overweight rail loads become empty; other stations keep their payload choices.
- C/T pylons follow the selected aircraft paint, T station spacing is improved, and middle pylons inherit the inner station's choices, including compatible installed mods. Loadout columns are ordered without changing saved or network station indices.
- A/C/T/NA stores MFDs reuse the game's native styling and profile assets, with paired right/left station rows and variant-specific stations. Selected weapon-group markers are bright; other attached markers stay dim in their state color. Detached markers disappear. Engine instruments remain separate.
- Fixed the family selector's native initialization hook so an optional UI integration failure does not prevent variant selection.

All 12 built-in paints and their 13 bundles are retained unchanged. No new texture or font is bundled for the MFD. This release contains A/B/C/T/NA; the separate FS-20A VS and FQ-20A studies are not included.

Install the full package with Nuclear Option closed. Requires Nuclear Option 0.34.6, BepInEx 5 and Blueprinter 2.1.1 or newer, supplied separately. Keep BuiltinPaints beside the DLL. Every multiplayer participant needs the same 0.7.3 package and compatible game/weapon mods. Matching-install multiplayer remains experimental.

The published build is compared with the tested local revision, rebuilt from the source ZIP and installed in an isolated fixture. Delta tests cover native save/admission and missile geometry; MFD state tests use reconstructed native UI with data stand-ins. They do not certify every third-party missile's motion/firing clearance or a two-PC Steam session. See QA.md for scope.
