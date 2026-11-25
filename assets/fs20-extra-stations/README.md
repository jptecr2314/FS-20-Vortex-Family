# External stations and wingtip compatibility

C/T middle pylons share the actual inner-station choices, including late mod registrations. Loadout menu reordering changes visual siblings only, preserving native saved and network station indices. Pylon materials follow the selected livery and damage slots.

A/C wingtip options use registered native WeaponMount definitions. A compatible option must be a single MountedMissile with air-primary effectiveness and external-aircraft mounting provenance. Ground/ship-only and magazine/bay-only definitions do not qualify. Existing IDs remain unchanged. Native availability runs before visible identity-based deduplication; saved aliases and server checks retain valid registered definitions. Synchronization runs at native menu, AI, save, spawn and validation boundaries, not every frame.

Each rail allows at most 240 kg inclusive, measured as emptyMass + massPerRound in double precision. Negative/nonfinite components are rejected; the cached mount.mass is not authoritative. Previously saved overweight rail choices normalize to empty. This ceiling is a station specification, not a measured rupture load. Other stations and B/T/NA are outside this rail policy.

Alignment uses the native AAM2_single geometry as the measured longitudinal/axis datum before MountedMissile.AttachToHardpoint caches mountedPosition. It preserves provider radial standoff, scale, carrier hardware, parenting and shared prefabs. Scope uses the supplied aircraft and native station membership, including physics-reparented owners. The operation runs only on attachment. Recheck the donor and clearances when geometry changes; it is not a universal aircraft datum.

See [QA.md](../../QA.md) for evidence and limits, including actual native geometry and save/admission checks. Matching peers still require matching weapon mods; arbitrary missile motion/firing clearance is not certified.
