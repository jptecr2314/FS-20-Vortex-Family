# Native stores MFD adaptation

Fs20StoresMfd reuses the installed FS-20B screen widgets, native white front-profile sprite, font/material settings and sliced borders. The gun box/profile and engine instruments remain separate. No replacement texture, extracted native sprite or font is distributed.

A/C/T/NA use paired right/left rows, with columns BAY, INNER, CENTER, OUTER and TIP as present on that variant. Native HardpointIndex bindings deduplicate alternative bay carriers and exclude hook hardware. Existing loadout IDs, weapon definitions, firing and networking remain native. B and unrelated aircraft are untouched.

The profile mesh retains the native sprite UVs and adapts the wing projection to measured variant spans; NA removes retired fins. This is an adapted native diagram, not a new three-dimensional silhouette render. Recheck it when aircraft geometry changes.

Selected native WeaponStation members use full marker opacity; other attached stations remain visible at alpha 0.25 in their state color, including expended red. Detached stations are hidden. Selection updates at the next UI Update; state polling runs at 4 Hz. A native group may include several physical station pairs; this does not introduce separate physical-station selection.

See [QA.md](../../QA.md) for reconstructed native UI tests and their stand-in data limits.
