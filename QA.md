# Validation record

The author reports in-game gameplay testing throughout development. The
checklist below tracks release regression evidence; an unchecked item does not
mean that feature has never been tried in game. Automated guard checks execute
the actual prefix decisions against native method contracts.

Use a clean test profile with the licensed game, BepInEx 5, Blueprinter 2.1.1
and only the candidate plugins. Back up missions/configuration. The local prep
script does not launch the game, install mods or send any request to a registry.

## Aircraft family

### 0.4.2 local candidate (2026-11-09)

- [x] Native mesh fixture: 21 checks cover exact removal of 1,182 VTOL-only
      triangles across eight material slots, seam handling and mismatch rejection.
      The shared main-engine intake ducts and all exterior skin are retained.
- [x] C slat pose comparison at 0/25/50/75/100%: closed mesh unchanged,
      rigid dimensions retained, lower full-deployment fit error on all four slats.
      This measures a rigid approximation of the widened native rig, not CFD.
- [ ] Observe this candidate's slat travel and A/C residual-hardware removal in game.

### Gameplay regression checklist

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


## 0.5.0 local integration

New A/C bay geometry, native-UV hatch cuts, side-specific native Auger mounts and the internal C hook were integrated locally. Fuel capacity is preserved by moving 190 kg of allocation from the aft fuselage cell to central-side cells; conservative 40 mm pocket screening includes 35 mm clearance and 25% internal reserve. Nominal empty budgets include all new fixed structures separately from the optional hook.

The production C# splitter was executed on native exterior triangles. All 24 bay-leaf footprints and the C hook opening retain their projected skin coverage without duplicated inner surfaces. The authored resource contains no native weapon/hook/skin meshes or textures. Offline sweeps include the generated native-UV panels, actual left/right native carriers, full flight fins, gear-up geometry and revised C hook well. These checks do not claim live initialization, aerodynamic separation, or a cable stop. The author handles the new gameplay tests.

Final candidate: 40,975 managed checks passed, including the compiled rack offsets against the geometry solver; four Auger release sweeps and 80 legacy-store stow/release-corridor cases passed. The liners clear the retained engine, intake and retracted gear geometry. Existing native loadout keys are preserved. The same 0.5.0 DLL was installed in the main game and trial with backups; neither game was launched by the installer. Local installation evidence is kept outside the release package.

## 0.5.1 registration hotfix

The author's next game session exposed a 0.5.0 registration failure in `Fs20DuctIslandSpec.RemoveCenter`: the code applied all seven center-duct island selectors to each material slot. Five islands belong to `jet_mechanical` and two to `aircraft_structure`. Both A/C registrations aborted with `FS-20 VTOL mesh layout changed`, so the family overlay had no variants to display. The earlier offline geometry and packaging checks did not establish successful game initialization.

0.5.1 routes each selector to its native material slot. The new regression reproduces the old exception in both slots, executes the corrected compiled selectors after native VTOL removal, and verifies exactly 224 structural and 176 mechanical triangles removed with every unrelated triangle retained in order. The existing 40,975 managed checks also pass. Bay/resource geometry and flight settings are unchanged; prior geometry sweeps remain applicable. Confirmation of A/C registration and overlay appearance requires the next game initialization by the author.

## 0.5.2 bay and spawn revision

The author observed the extra Auger selectors, overlapping old bay hardware and fuselage damage immediately at spawn in 0.5.1. The revision returns Auger to the native paired inner-bay group and reserves the outer group only while Auger is selected. Native A/C group counts and the optional C hook index are preserved. Paired old Auger presets migrate; a one-sided old preset must be selected again.

The old curved-leaf bounding boxes overlap native hulls in 20 closed-door cases. These added boxes were removed; separately authored native collision hulls are retained. Native collision damage, breakable joints and cable forces remain enabled. This reproduces an interference mechanism; it does not establish the result of a live PhysX spawn.

Current compiled surface/loadout checks passed 48,234 assertions using 59,676 native triangles, including the entire retained lift-fan subtree, fixed skin and wing-root frames. The checks preserve 14,440 upper-skin faces and fixed exterior area outside the portals. Four Auger sweeps and 80 legacy-store release-corridor cases passed with the corrected retained surfaces. They are offline geometry/kinematics checks, not aerodynamic separation tests.

Blender inspection uses native model geometry and textures. Closed/open views include the C hook region. Eight authored forward-panel instances have complete projected area and valid native UV samples. The C hook hatch covers its 0.439391 square metre opening; its UVs match the source skin within 0.000000060 atlas units. The forward extensions use clean native panel paint; all other exterior patches retain interpolated native UVs. No native texture or aircraft mesh is embedded in the package.

Windows App Control blocked the updated standalone managed UV-check executable. It was not rerun or bypassed. The current DLL executes normally in the existing full-surface/loadout checker; the separate Blender asset inspection and static compiled-contract checks passed. These are recorded separately rather than reported as a successful managed UV check. The new spawn behavior and final appearance in the game are left to the author; this revision does not invalidate earlier gameplay reports for other features.

The identical 0.5.2 DLL and packaged files were installed in the main game and trial, with 0.5.1 DLL backups retained. Both installations were hash-checked against the staged package. Neither game was launched or stopped. This local candidate has not been uploaded as a new public release.

## 0.5.3 continuous doors and lining

Each A/C bay now has one native BayDoor controller and one continuous lining, with all exterior skin patches attached to that hinge. A opens 90 degrees and C 110 degrees toward the centre. Opening them toward the outside was rejected because the larger panel obstructed the rolled Auger rail. The two sides remain geometrically separate. Native loadout groups, mass/fuel budgets, breakable parts and the stock B are unchanged.

The C hook cover now rotates about a fixed longitudinal axis. Two captive-pin hinges and attachment tabs connect it to the compartment; there is no preliminary vertical translation. The native hook waits for the cover to open and the cover closes after stow. The small perimeter clearance is 2 mm; exterior UVs still match the native skin. The same interior material covers the well and hatch backing.

The previous untextured Gunmetal lining and tiny per-triangle atlas were replaced for bay walls and door interiors. A shared material uses owned, procedurally authored panel/fastener maps with metallic, smoothness, neutral-normal and occlusion maps, sampled at one tile per 0.50 m. UV projection uses aircraft-space coordinates so adjacent coplanar faces keep the same scale. Native exterior materials remain separate.

The compiled partition/loadout checker passes 48,234 checks against the retained native mesh scope. Four bay door sweeps sample 56 poses each; the hook hatch sweep samples 81 poses against fixed skin, the well, stowed hook and closed bay doors. All are clear, and the hinge axis has zero translation/drift. Four Auger release sweeps and 80 original-store release-corridor cases pass with the new continuous doors. Blender closed/open renders inspect the actual extracted skins and authored geometry. These are offline checks; they do not claim a live Unity material rendering or firing/spawn result. The separate managed UV-check executable previously blocked by Windows App Control was not used or bypassed.

The 0.5.3 package was installed and hash-verified in both the main game and trial. Previous 0.5.2 DLLs were backed up. No game was opened or stopped and no public upload was performed.

## 0.5.4 conventional paint

Native UV mapping locates two FAN EXHAUST stencils on the former ventral fan doors and one FAN INTAKE stencil on the upper fan cover. These three regions are cleared only on the A/C skin material copies. The BEWARE OF BLAST labels map to the retained tail/engine region and remain unchanged, as do canopy and ejection warnings. The paint component is installed only by the A/C registrations and refreshes after the native LiveryBehaviour.SetLivery callback.

The compiled pixel routine passes 14 checks on the native base/default-livery atlas and synthetic scaled fixtures. Each native atlas changes 12,411 pixels inside the three rectangles, with zero changes outside. The bright obsolete stencil pixels fall from 3,679 to zero. Source pixel arrays stay unchanged and repeat application is stable. Damage, normals, occlusion and metallic textures remain native. The material readback preserves and restores the render target and sRGB-write state, following the [Unity Graphics.Blit colour-space guidance](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/graphics/blit).

Native-model studio renders inspect the upper and lower skin with the compiled output. The upper view uses the original native canopy/material reference rather than the generic non-skin collision-fixture material. This is not a live Unity GPU readback or observation of in-game livery switching. No new aircraft geometry was generated for this revision.

All 39 unchanged source files, the two registration methods apart from the isolated paint install/patch calls and version, and the geometry resource were compared against the installed 0.5.3 baseline. Existing door/weapon/structure checks are retained as 0.5.3 evidence instead of rerunning unrelated geometry sweeps. The current compiled contract inspection confirms the A/C-only paint install and guarded livery callback.

The same 0.5.4 DLL and packaged files were installed and hash-verified in the main game and trial, with 0.5.3 DLL backups. Neither game was launched or stopped; no public upload was performed.

## 0.5.5 inactive-prefab repair

The author's 0.5.4 session loaded the expected DLL but failed in Fs20HatchHinges.Primitive with a NullReferenceException. The child searched for an AeroPart through inactive parents, received null, and threw before its caller could scale the primitive. C had already been added to the encyclopedia. The tailhook option, Auger additions and C paint setup did not finish, leaving a default-sized cylinder on a partially registered aircraft. This runtime report supersedes any assumption that the earlier offline checks established successful C initialization.

Hinge parts now receive the validated root AeroPart directly and are posed/scaled before renderer registration. Three skin/controller owner queries now pass includeInactive=true, matching the [Unity 2022 parent-search contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Component.GetComponentInParent.html). Fixed native door-skin fragments can therefore be attached to their structural owner before the original controller is retired. Hook, weapon options and paint initialization finish before the C definition is published.

The lining uses lighter grey primer and explicit low-metallic/rough opaque settings. This addresses the inherited dark/glossy appearance; its visibility under actual game lighting is not yet confirmed for this revision. No emissive lighting, invulnerability or native collision suppression was added.

Compiled-contract inspection verifies all three inactive-aware lookups, explicit hinge ownership, primitive scaling before registration and hook setup before aircraft publication. The current candidate passes 48,234 managed checks plus native-UV surface coverage and offline door/hatch motion checks. Hinge pose constants, the pure cutter geometry, release offsets and the authored geometry resource were compared against the earlier versions; existing release-corridor and paint-pixel results are retained for those unchanged functions.

A native-prefab validation harness was prepared for the installed Unity 2022.3.62f2 editor and compiles against its editor API. The editor exited before project/test execution because it had no active license. No licensing controls were bypassed and Nuclear Option was not launched. Successful native C registration, visible hook selection and the new material in game still require the next author test. This specific validation gap is recorded separately from the passed managed/offline checks.

The identical 0.5.5 DLL and packaged files were installed and hash-verified in the main game and trial. Both previous 0.5.4 DLLs were backed up. Nuclear Option remained closed, and no public upload was performed.


### 0.5.6 local regression (2026-11-10)

- The author's 0.5.5 game log confirms C registration completed with hook=True,
  six groups and paint=True. Their screenshots identify repeated deployment
  notices at spawn and visible interior UV discontinuities.
- 3,010 production departure-state/eligibility checks: prolonged ground spawn
  produces zero eligible automatic deployments; an established approach produces
  one transition; gear-up, go-around, overload and caught-wire cases are covered.
  These are state/threshold scenarios, not Unity animation or cable tests.
- Compiled inspection verifies the exact installed TailHook.Start scheduler
  signature, registered callback guard, C-only missing-controller fallback,
  scoped part-livery callback and exact embedded geometry/UV bytes.
- Shipped UV asset review covers all five interior meshes. No collapsed UV
  triangles or discontinuities inside a chart remain; seams at end/rim faces
  are intentional. Curved door sections use developed cross-section distance.
- Blender renders read the actual authored UV bytes. Geometry, exterior UVs,
  fuel/mass/thrust, collider hulls and release paths retain 0.5.5 evidence; the
  current geometry loader also passes the 48,234 managed geometry checks.
- Acceptance of this revision's cockpit notifications and final material/livery
  appearance remains with the author. The game is not launched or closed by the
  preparation/install scripts. Unity Editor execution remains unavailable
  without an active license; no licensing workaround was attempted.


### 0.5.7 local regression (2026-11-10)

- The author clarified that the grey checker was visible: the map was loading,
  but the generated finish was inadequate. The C preset also selected
  aryx_navex_bomb_penetrator1_internalx1 alongside the custom Auger choice.
- Replace the generated finish with a matched native mechanical atlas region:
  albedo, packed normal, metallic/smoothness and AO; opaque material, continuous
  metric UVs. Inspect all four source crops and a Blender render using those
  exact regions. These are native game assets read at runtime, not bundled maps.
- 48,244 compiled managed checks cover duplicate same-info/native/Aryx/private
  carriers, stable option order/Empty, one canonical inner choice, exclusion
  from outer bays, saved-key migration and the existing full-surface geometry.
  Registered selector population and SetValue hooks are verified against the
  installed native signatures. The author's config file is left unchanged.
- Native fixture count is 59,248 triangles after removing 428 obsolete fan-cover
  internal triangles. Main intake island remains 1,404 triangles. Native skin is
  retained; fixed output loses 543 clipped mechanical triangles per variant,
  adds none, and moving bay/hook patches are identical to 0.5.6. Existing door,
  hook and weapon release clearance evidence is retained by this subset proof.
- Retire rotor LOD/damage references and check again during initial spawn frames.
  Current in-game residual-fan visibility and final material/selector appearance
  require author confirmation; static checks and renders do not establish that.
- No game launch/close or publication. The known Unity Editor license limitation
  remains; no licensing or Windows App Control workaround was attempted.


### Inner/Outer payload audit — 2026-11-10, against 0.5.7

Read-only study; no plugin, configuration or available weapon option changed.
83 licensed native game carriers (ammo 1–6), one extracted complete IRM-S1
launcher and derived tandem concepts: 736 station/variant/side placements,
1,650 coupled Inner/Outer pairs and 116 detailed exit scenarios. Both A/C and
both sides are included. Installed third-party weapon models are outside scope.

Inner and Outer occupy the same chamber on each side. Native station X/roll
remain fixed; the study does not allocate the full chamber twice. Standalone
fits are conditional and are never added together without a coupled check.
The existing Auger policy reserves both Inner/Outer resources on that side.

- 2 AGM-48 in line in Inner plus 2 in Outer: clear in both A/C and both sides;
  four per side / eight total. Both firing orders clear the sampled folded
  geometry corridor. This carrier has not been enabled by this study.
- 2 AGM-48 Inner plus 1 IRM-S2 Outer: coupled fit and sampled exits pass.
- 1 ARAD-45 Inner plus 1 MMR-S3 Outer: coupled fit; the sampled corridor
  requires Inner to clear first. This is not arbitrary-order firing approval.
- Three complete single IRM-S1 launchers in line fit statically in Inner.
  Their native carrier/exit conflicts remain; do not count them as usable
  capacity or add an untested Outer maximum.
- Two IRM-S2, MMR-S3, AAM-29 or AAM-36 missiles nose-to-tail exceed the
  4.744 m chamber length even before other station reservations. Existing
  native multi-missile packages arrange rounds laterally/staggered.
- Four ATP-1 fit Inner only with a proposed 0.10 m vertical rack change.
  Forward launch/remaining rounds still need a new verified installation.

Checks use the current compiled retained skin, authored lining/door geometry,
engine/intakes, retracted gear, native carriers and the C hook well. Closed
fit samples require 5 mm; paired payload samples require 10 mm, with triangle
intersection and complete-containment rejection. Five door poses and 23
folded-geometry exit steps over 1.7 m are sampled. That exit corridor extends
beyond some native rail lengths: it does not simulate powered flight, full
fin deployment or live firing. Native rail directions were checked against
MountedMissile.Fire/RailLaunch in the installed game assembly.

The previous extra-store-fit-study.json remains a historical whole-chamber
screen; it must not be used as an independent Inner/Outer capacity table.
Full current evidence is in inner-outer-fit-audit.json in the existing local
weapon-bay study integration output. No game was opened for this audit.

Native carrier placement screen below: Yes means both sides found a static
pose on that station, with any declared shims/recentering. It is not a coupled
loadout, launch result, or an enabled option; No means no fit in this bounded
search, rather than a proof that another redesigned rack is impossible.

| Native carrier | Rounds | A Inner | A Outer | C Inner | C Outer |
|---|---:|:---:|:---:|:---:|:---:|
| PAB-125 x2 (`bomb_125_internalx2`) | 2 | Yes | No | Yes | No |
| PAB-125 x2 (`bomb_125_internalx2_line`) | 2 | Yes | Yes | Yes | Yes |
| PAB-125 x4 (`bomb_125_internalx4`) | 4 | Yes | No | Yes | No |
| PAB-125 x6 (`bomb_125_internalx6`) | 6 | No | No | No | No |
| PAB-250 (`bomb_250_internal`) | 1 | Yes | Yes | Yes | Yes |
| PAB-250 x2 (`bomb_250_internalx2`) | 2 | Yes | No | Yes | No |
| PAB-250 x3 (`bomb_250_internalx3`) | 3 | No | No | No | No |
| PAB-250LR (`bomb_250_glide_internal`) | 1 | Yes | Yes | Yes | Yes |
| PAB-250LR x2 (`bomb_250_glide_internalx2`) | 2 | No | No | No | No |
| PAB-250LR x3 (`bomb_250_glide_internalx3`) | 3 | No | No | No | No |
| GPO-500 (`bomb_500_internal`) | 1 | Yes | No | Yes | No |
| GPO-500 x2 (`bomb_500_internalx2`) | 2 | No | No | No | No |
| GPO-500 x4 (`bomb_500_internalx4_flat`) | 4 | No | No | No | No |
| GPO-500 x4 (`bomb_500_internalx4_stack`) | 4 | No | No | No | No |
| GPO-500 x6 (`bomb_500_internalx6`) | 6 | No | No | No | No |
| GBM-500LR (`bomb_500_glide_internalx1`) | 1 | Yes | No | Yes | No |
| GBM-500LR x4 (`bomb_500_glide_internalx4_flat`) | 4 | No | No | No | No |
| GBM-500LR x6 (`bomb_500_glide_internalx6`) | 6 | No | No | No | No |
| CBO-400 (`bomb_cluster1_dual_internal`) | 2 | No | No | No | No |
| CBO-400 (`bomb_cluster1_single_internal`) | 1 | Yes | No | Yes | No |
| Demolition Bomb (`bomb_demolition_internal`) | 1 | No | No | No | No |
| Demolition Bomb (`bomb_demolition_internalx1`) | 1 | No | No | No | No |
| Demolition Bomb x2 (`bomb_demolition_internalx2`) | 2 | No | No | No | No |
| GPO-2P Auger x2 (`bomb_penetrator1_internalx2`) | 2 | No | No | No | No |
| GPO-2P Auger x3 (`bomb_penetrator1_internalx3`) | 3 | No | No | No | No |
| GPO-2P Auger x4 (`bomb_penetrator1_internalx4`) | 4 | No | No | No | No |
| GPO-N (1.5kt) (`nuclearBomb1_internal`) | 1 | Yes | No | Yes | No |
| GPO-N (1.5kt) x2 (`nuclearBomb1_internalx2`) | 2 | No | No | No | No |
| GPO-N (1.5kt) x4 (`nuclearBomb1_internalx4`) | 4 | No | No | No | No |
| GPO-N (1.5kt) x4 (`nuclearBomb1_internalx4_stack`) | 4 | No | No | No | No |
| GPO-N (250kt) (`nuclearBomb1_strategic_internal`) | 1 | Yes | No | Yes | No |
| GPO-N (250kt) x2 (`nuclearBomb1_strategic_internalx2`) | 2 | No | No | No | No |
| GPO-N (250kt) x4 (`nuclearBomb1_strategic_internalx4`) | 4 | No | No | No | No |
| GPO-N (250kt) x4 (`nuclearBomb1_strategic_internalx4_stack`) | 4 | No | No | No | No |
| MMR-S3 x2 (`AAM1_double_internal`) | 2 | Yes | No | Yes | No |
| MMR-S3 x3 (`AAM1_internalx3`) | 3 | No | No | No | No |
| MMR-S3 (`AAM1_single_internal`) | 1 | Yes | Yes | Yes | Yes |
| AAM-29 Scythe x2 (`AAM2_double_internal`) | 2 | No | No | No | No |
| AAM-29 Scythe (`AAM2_single_internal`) | 1 | Yes | Yes | Yes | Yes |
| AAM-29 Scythe x3 (`AAM2_triple_internal`) | 3 | No | No | No | No |
| IRM-S2 x2 (`AAM3_double_internal`) | 2 | Yes | No | Yes | No |
| IRM-S2 x4 (`AAM3_quad_internal`) | 4 | No | No | No | No |
| IRM-S2 (`AAM3_single_internal`) | 1 | Yes | Yes | Yes | Yes |
| IRM-S2 x3 (`AAM3_triple_internal_compact`) | 3 | Yes | No | Yes | No |
| IRM-S2 x3 (`AAM3_triple_internal_flat`) | 3 | Yes | No | Yes | No |
| AAM-36 Scimitar x2 (`AAM4_double_internal`) | 2 | No | No | No | No |
| AAM-36 Scimitar x2 (`AAM4_double_internal_compact`) | 2 | No | No | No | No |
| AAM-36 Scimitar (`AAM4_single_internal`) | 1 | Yes | Yes | Yes | Yes |
| AAM-36 Scimitar x3 (`AAM4_triple_internal`) | 3 | No | No | No | No |
| AGM-48 x2 (`AGM1_double_compact`) | 2 | Yes | No | Yes | No |
| AGM-48 x2 (`AGM1_double_line`) | 2 | Yes | Yes | Yes | Yes |
| AGM-48 x4 (`AGM1_quad_internal`) | 4 | No | No | No | No |
| AGM-48 (`AGM1_single`) | 1 | Yes | Yes | Yes | Yes |
| ATP-1 x4 (`AGM2_internalx4`) | 4 | Yes | No | Yes | No |
| AGM-68 (`AGM_heavy_internal`) | 1 | Yes | No | Yes | No |
| AGM-68 x2 (`AGM_heavy_internalx2`) | 2 | No | No | No | No |
| AGM-68 x4 (`AGM_heavy_internalx4`) | 4 | No | No | No | No |
| AGM-68 x6 (`AGM_heavy_internalx6`) | 6 | No | No | No | No |
| ARAD-116 x2 (`ARM1_internalx2`) | 2 | No | No | No | No |
| ARAD-116 x4 (`ARM1_internalx4`) | 4 | No | No | No | No |
| ARAD-45 (`ARM2_single`) | 1 | Yes | No | Yes | No |
| ARAD-45 (`ARM2_single_internal`) | 1 | Yes | Yes | Yes | Yes |
| AShM-300 (`AShM1_internal`) | 1 | No | No | No | No |
| AShM-300 x2 (`AShM1_internalx2`) | 2 | No | No | No | No |
| AShM-300 x4 (`AShM1_internalx4_flat`) | 4 | No | No | No | No |
| AShM-300 x4 (`AShM1_internalx4_stack`) | 4 | No | No | No | No |
| AShM-300 x6 (`AShM1_internalx6`) | 6 | No | No | No | No |
| AGM-99 (`AShM2_internal_single`) | 1 | Yes | No | Yes | No |
| AGM-99 x2 (`AShM2_internalx2_stack`) | 2 | No | No | No | No |
| AGM-99 x4 (`AShM2_internalx4`) | 4 | No | No | No | No |
| AGM-99 x6 (`AShM2_internalx8`) | 6 | No | No | No | No |
| Tusko-B (HE) x2 (`AShM3_internalx2`) | 2 | No | No | No | No |
| Tusko-B (HE) x4 (`AShM3_internalx4_flat`) | 4 | No | No | No | No |
| Tusko-B (HE) x4 (`AShM3_internalx4_stack`) | 4 | No | No | No | No |
| Piledriver TBM x2 (`BallisticMissile1_internalx2`) | 2 | No | No | No | No |
| Piledriver TBM (20kt) x2 (`BallisticMissile1_tacNuke_internalx2`) | 2 | No | No | No | No |
| ALM-C450 (`CruiseMissile1_internal`) | 1 | No | No | No | No |
| ALM-C450 x2 (`CruiseMissile1_internalx2`) | 2 | No | No | No | No |
| ALM-C450 x3 (`CruiseMissile1_internalx3`) | 3 | No | No | No | No |
| ALM-C450 x4 (`CruiseMissile1_internalx4`) | 4 | No | No | No | No |
| ALM-C450 x6 (`CruiseMissile1_internalx6`) | 6 | No | No | No | No |
| ALND-4 (20kt) x2 (`CruiseMissile20kt_internalx2`) | 2 | No | No | No | No |
| IRM-S1 x2 (`IRMS1_internalx2`) | 2 | Yes | No | Yes | No |


### Auger/Outer coexistence follow-up — 2026-11-10

The author clarified that Auger in Inner should preserve Outer on the same
side. The installed 0.5.7 remains unchanged during the geometry study.
Current Auger placement intersects all five native Outer
packages tested; the blanket restriction had compensated for that layout.

A prototype with Auger at |X|=0.48 m, Y=-0.96 m, Z=-0.50 m, 30-degree roll,
retargeted support links and Outer IRM-S2 shifted 0.02 m outboard passes all
four A/C/side cases: closed volume, payload separation, five door poses, both
folded-model exit corridors and eleven post-rail native Auger flight-mesh
poses with Outer still loaded. That candidate is not installed or approved
as the final redesign. Native larger Outer packages did not obtain a clear
solution in the bounded searches, including a prospective upper recess.

Evidence: auger-outer-coexistence.json, auger-outer-pose-search.json,
auger-outer-refined-solutions.json, auger-outer-headroom-solutions.json and
auger-outer-candidate-qa.json in the existing local integration output.
No game launch, publication or installed plugin change for this follow-up.

#### AAM-29 target and bay redesign — 2026-11-10

The author selected **Auger in Inner + AAM-29 Scythe in Outer on the same
side**, and requested bay redesign to preserve larger missiles. The earlier
IRM-S2 prototype is not the accepted target.

Additional searches use the complete native folded weapons/carriers at
actual scale. The current lining, retained skin/frame, engine, intake,
retracted landing gear, hook well, five door poses and the other loaded
station remain collision constraints. Original support links are omitted
while searching new carrier poses; they must be retargeted and checked
before any candidate can pass final geometry QA. Right-side searches alone
are not acceptance evidence for both sides.

- The broader Outer roll search found seven static pairs, but no pair with
  both release paths clear.
- A focused position search found 120 static pairs. All failed the Scythe
  release corridor; a separate ejector-angle search also found no solution.
- Finer Outer rolls found 478 static pairs, with no complete release solution.
- Opposite Auger carrier orientations were checked with a prospective
  downward bomb ejector; no static pair survived that bounded search.
- A relaxed physical-space search found 72 prospective release arrangements
  after omitting the current liner and retained skin/frame. **Those are
  exploratory layouts, not feasible completed bays**: the omitted surfaces
  must be restored or redesigned and checked.
- A narrow forward pocket prototype still blocked release. A wider pocket
  extending into the bay shoulders interfered with native gear, engine and
  duct geometry. Both prototypes are rejected; neither changes the runtime.

Evidence: `auger-scythe-roll-search.json`, `auger-scythe-fine-search.json`,
`auger-scythe-ejector-solutions.json`, `auger-scythe-roll-refine.json`,
`auger-scythe-auger-roll.json`, `auger-scythe-pocket-search.json`,
`auger-scythe-pocket-required-changes.json`,
`auger-scythe-reprofile-diagnostics.json` and
`auger-scythe-study-verification.json` in the existing local integration
output. Reproduction scripts are `tools/study_fs20_auger_scythe.py` and
`tools/prototype_fs20_scythe_bay.py`.

This result does not prove that the target is impossible. It rules out the
tested arrangements and simple shoulder expansions. A valid redesign still
needs a collision-free liner/aperture, connected supports and doors, native
flight-fin separation checks on A/C left and right, and runtime integration.
The current 0.5.7 Outer restriction remains active. No claim of increased
usable capacity, unchanged fuel budget or live-game validation is made.

#### 0.5.8 — original Outer options restored with Auger

The author subsequently accepted weapon clipping during release and requested
all original Outer loadouts. This changes the release acceptance criterion
used by the preceding study; it does not certify collision-free rail travel.

- Remove the mod's blanket Outer block and preserve Outer selections through
  menu generation, native/Aryx Auger alias migration and saved-loadout loading.
  Retain all six native options: AAM-29, MMR-S3, IRM-S2, AAM-36, two PAB-125
  and two PAB-80LR. Add no selector groups; retain the C hook slot.
- Move Auger to |X|=0.48, Y=-0.96, Z=-0.50 m, roll 30 degrees. Apply paired
  Outer poses only while Auger is equipped. Instance-owned support meshes
  follow the native Auger rack, preserving wall anchors; other Inner loads
  restore the original supports and native Outer poses.
- All 24 A/C/left/right/Outer stored combinations pass full-size native-model
  separation, current closed envelopes, retained skin/duct/engine/retracted
  gear/hook constraints, retargeted supports and five door poses. Geometry
  uses offsets/rolls exported from the compiled production methods.
- Keep native damage/collision handling. Outer rounds paired with Auger use
  at least 1.5 m of guided rail travel before the game spawns the active
  projectile. MMR-S3 uses its outward/downward local rail axis. Door hold
  covers rail delay, travel time and 0.3 s margin. Render meshes may intersect
  the airframe along that rail, as accepted by the author.
- Active native projectile geometry passes 21 frozen-attitude positions per
  Outer round after the rail, with the Auger and remaining carrier/rounds
  retained; Auger passes 11 post-rail poses with each Outer load present.
  These checks are offline geometry, not observed live firing or proof that
  physics initialization cannot cause damage.
- Finite bay joint force rises to 350 kN. The existing 485 kg bay budget plus
  1,000 kg Auger and the heaviest original 375 kg Outer package gives about
  301 kN at +11 G with a 1.5 design factor. Mass, fuel, thrust, exterior skin,
  door/hook geometry, interior texture/UV resources and stock B are unchanged.

Evidence: 48,276 managed option/migration/support/geometry checks,
`compiled-revision-contracts.json`, `shared-bay-layout-058-qa.json` and
`runtime-regression-058-qa.json`. Native rail-to-projectile ordering was
inspected in the installed game assembly. The main/trial games are not
launched or closed by this update; current gameplay acceptance stays with
the author. No GitHub or NOMM publication is part of this local revision.


## 0.5.10 identifier migration

Built from the published 0.5.8 source, with changes limited to plugin and
Harmony IDs, the startup identity migration and distribution metadata/docs.
No aircraft, weapon, saved definition key, physics or single-player guard
implementation was changed. The newer T and multiplayer builds are excluded.

NOMM source commit 8786356fd9426c261bec6fed168f25c2f1fee815 identifies installed
mods by meta.json.id and matches updates by that same catalog key. Its
installer overwrites package metadata with the requested catalog ID.
Accordingly, retaining the old entry is necessary: deleting or renaming it
would strand installations that have not yet run the migration.

The build and static assembly checks verify the new plugin GUID, the old-ID
incompatibility guard, migration before configuration bindings, unchanged
native definition keys, unchanged authored resources and the retained
single-player guard. The BepInEx constructor was inspected: it does not
create a missing configuration file, so the startup copy precedes bindings.
The install/update lifecycle is simulated separately using the inspected
NOMM directory, metadata overwrite, enable/disable and version rules.
These are offline checks; no live NOMM installation, game launch or Unity
JSON call was exercised for this release. Previous test/gameplay evidence
above remains historical evidence from the respective versions.
