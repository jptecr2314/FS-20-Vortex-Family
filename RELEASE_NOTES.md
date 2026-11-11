# FS-20 Vortex Family 0.5.10

Changes the plugin/package ID to `fs20vortexfamily`. Aircraft, weapons,
physics, shared-stock behavior and the single-player boundary remain as in
0.5.8. This release does not include the separate multiplayer or T studies.

## Updating an existing NOMM installation

Update your existing FS-20 entry. Do not install a second copy from the new
entry. The old catalog ID remains as an update bridge; it downloads the
same 0.5.10 plugin. NOMM writes the old ID into its installation metadata,
so the plugin migrates that metadata on its first enabled game launch.
After closing the game, refresh NOMM to see `fs20vortexfamily`.

Existing BepInEx settings are copied only if the new configuration does
not exist. The old configuration is kept. Replaced NOMM metadata receives
a backup alongside its original file. The migration touches only this
plugin's ancestor metadata, never another mod or the catalog itself.
If a file cannot be updated, an English warning is logged and the old
catalog entry continues to provide the update. Disabled installations
migrate when they are enabled and the game is next started.

For a fresh installation, use the `fs20vortexfamily` catalog entry.
Keep only one installed copy. The native aircraft/weapon definition keys
are unchanged, preserving existing A/C mission and loadout references.

This is still an experimental single-player release. Migration was checked
against the NOMM source and compiled plugin metadata; the game was not
launched for this identifier-only release. See QA.md for the precise scope.
