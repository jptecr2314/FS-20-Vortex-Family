# FS-20C / FS-20NA — Penumbra compatibility

Adds the FS-20C (`JP_FS20_CTOL`) or FS-20NA (`JP_FS20_NA`) definition to the supported external
`.catapults` format of Aryx FS-41 Eclipse 1.1.9 / Penumbra. It uses the same
intake-relative nose-wheel attachment and launch settings as the native
FS-20B rule: 90 m/s release, 5 G acceleration cap and 0.5 s throttle delay.
These are the catapult's existing settings, not measured launch performance.

The FS-20 family plugin already adds C and NA to hangars accepting the stock B.
This file supplies the separate catapult rule; it does not enable C in
helicopter-only hangars, add A to carriers, or alter aircraft physics.

With the game closed, run the product's `tools/install_fs20_penumbra.ps1`
with the desired `-GamePath` and `-AircraftVariant NA` for Night Attack.
Omitting the variant retains the existing C installation behavior.
The installer finds the installed Aryx module
and places this file beside its `example.catapults`. Previous copies of
this specific file are backed up. No DLL or other aircraft rule is changed.
The game is left closed. The native loader reads the rule on its next scan.

The NA Unity bench executes the installed Aryx parser, aircraft definition
lookup, flexible transform resolver and offset-clutch creation against the
production NA clone. The retained intake/nose-gear reference is verified.
The installer was also exercised twice in an isolated file fixture, preserving
the C rule and both dependency DLLs. Equip the internal arresting hook when
preparing for arrested landings, as on C. No live selection, launch or landing
test is claimed. Historical C selection QA is not a new NA flight test.

To remove a variant's integration, remove only its `FS20C.catapults` or
`FS20NA.catapults` from that folder.
If a mod-manager update replaces the Aryx folder, rerun the installer.

Installation status is separate from the source/parser contract. Run
`tools/check_fs20_penumbra.ps1 -AircraftVariant NA -RequireInstalledRule` to
require the actual matching file beside the installed Aryx module. A plain
contract pass can report `installationReady: false`; that is not an installed
integration. The installer now performs this strict check after copying.
