# Release acceptance — pending live checks

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

- [ ] Faction starts with 48 B: A/B/C display the same 48; requisition one ->47.
- [ ] Owned B cell spawns an actual C; exactly one owned cell consumed.
- [ ] Recovery returns one cell, with original reserved flag; donation/sale cannot
      sell a reserved-only balance or multiply money/stock.
- [ ] Exhaust 48 cells; 49th unavailable. Mixed A/B/C raw rows sum once.
- [ ] Separate players/factions remain isolated; save/load preserves raw balances.
- [ ] With the plugin removed, native raw records remain usable.

## NOMNOM multiplayer acceptance blocker

- [ ] Matched host/client family and shared-plugin versions, with host features
      enabled: synchronized counts and correct prefab identities on both peers.
- [ ] Host with family only/client with shared inventory: client must NOT gain
      stock or attempt unauthorized requests that could trigger native anti-cheat.
- [ ] Unmodded or mismatched host: no unauthorized flight/stock changes, invalid
      prefab requests, desync or crashes. Verify refusal before exposing actions.
- [ ] Host-side feature-disable path/consent and client behavior documented and
      demonstrated. Current native server guards alone are NOT a completed test.

Do not submit to NOMNOM until these required policy cases pass. If the refusal
or consent cases fail, implement a host capability/version handshake before
distribution, or a tested single-player-only gate. This preparation does not
claim those controls already exist. Do not advertise client-only compatibility.

Keep dated logs, genuine screenshots and a short screen recording of the current
candidate. Do not use the old blue-card offline mockup as a gameplay screenshot.
