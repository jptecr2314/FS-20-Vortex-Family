# Validation record

The author reports in-game gameplay testing throughout development. The
checklist below tracks release regression evidence; an unchecked item does not
mean that feature has never been tried in game. Automated guard checks execute
the actual prefix decisions against native method contracts.

Use a clean test profile with the licensed game, BepInEx 5, Blueprinter 2.1.1
and only the candidate plugins. Back up missions/configuration. The local prep
script does not launch the game, install mods or send any request to a registry.

## Aircraft family

- [ ] Cold start, correct version in LogOutput.log, no family/geometry exceptions.
- [ ] FS-20B opens the native-style A/B/C overlay; no overlapping text at 100%,
      150%, 200% UI scale; correct focus, outside click and UI Cancel behavior.
- [ ] A/B/C previews, purchase, requisition and actual spawns retain their identity.
- [ ] A is excluded from ship hangars; C can use eligible ship hangars.
- [ ] A/C vapor initializes and looks correct without undue particle cost.
- [ ] C hook door opens before extension, closes after stow; native arresting
      landing works; cavity opaque, support visible, no clipping in motion/damage.
- [ ] A extra fuel actually fills/consumes/leaks; mass updates correctly.
- [ ] A/C +11 G assistance, unloading and assist-off checked across fuel/loadouts.
- [ ] 0.3.10 hover tooltip renders above the expanded variant tiles.
- [ ] 0.3.10 ventral hook well/door/support UV appearance checked in game.
- [ ] A/C gear-down approach and flare remain controllable; gear-up restores
      combat response; assist-off preserves direct input.
- [ ] C hook stays stowed at departure and go-around, rearms on approach,
      and the departure guard never releases an already caught wire.
- [ ] Native AI runway approach/touchdown observed for A/C; AI carrier recovery
      remains unverified unless separately demonstrated.
- [ ] C 110/165 kN configuration checked; earlier 180 kN Cursor report is not
      evidence for this revision's short-deck takeoff.
- [ ] Reload mission, change aircraft, respawn and return to main menu repeatedly.

## Shared airframes

- [ ] Only VortexConventional.dll is installed; legacy Fs20SharedAirframes.dll
      is removed. One FS-20 plugin appears in Configuration Manager.
- [ ] F1 > FS-20 Vortex Family > Inventory > Shared airframe stock toggles
      pooled/separate counters and persists after restart. A/B/C stay selectable.
- [ ] Switch modes with uneven stock/owned entries; no cells are created,
      converted or redistributed and separate balances remain intact.

- [ ] Faction starts with 48 B: A/B/C display the same 48; requisition one ->47.
- [ ] Owned B cell spawns an actual C; exactly one owned cell consumed.
- [ ] Recovery returns one cell, with original reserved flag; donation/sale cannot
      sell a reserved-only balance or multiply money/stock.
- [ ] Exhaust 48 cells; 49th unavailable. Mixed A/B/C raw rows sum once.
- [ ] Separate players/factions remain isolated; save/load preserves raw balances.
- [ ] With the plugin removed, native raw records remain usable.

## Single-player-only release

- [x] Guard prefixes allow native Offline hosting and non-listening local servers.
- [x] Guard prefixes refuse every online SocketType and unknown transport values.
- [x] Multiplayer menu and both client connection entry points are blocked.
- [x] Actual native target contracts and the single integrated plugin are checked offline.
- [ ] In-game local mission startup and visual blocked-multiplayer notice observed.
- [ ] In-game attempts through multiplayer menu, invite/direct connect and hosting observed.

No multiplayer support is advertised. The offline prefix tests execute the guard
branches without opening sockets or running native game methods. The public build is an experimental single-player prerelease.
Gameplay testing and automated contract checks are distinct evidence. Disable this mod and restart to play online.

Keep dated logs, genuine screenshots and a short screen recording of the current
candidate. Do not use the old blue-card offline mockup as a gameplay screenshot.
