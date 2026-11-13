# 0.6.8

- Isolated multiplayer adaptation of the published 6.0.7 A/B/C/T family.
- Matching-build and definition-order exchange after native game authentication.
- Host opt-in and session-owned stock/flap policy without overwriting client configuration.
- Owner-only A/C/T flight assistance and arresting forces.
- Remote C/T slat, hook and physics-driven vapor presentation at a bounded cadence.
- No FS-20NA, geometry, textures or flight tuning from 6.1.x.

## 6.0.7

- Include the FS-20T as a standard member of the A/B/C/T family in one DLL.
- Retain the four-variant selector and configurable shared or separate stock.
- Retire the old T experimental-enable gate without changing aircraft keys.
- Include the latest C/T volumetric vapor, rear-panel/elevon coverage and
  corrected density-advection direction from the 0.8.11 development build.
- Replace the T's repeated pitch cut/rebuild near the flight envelope with a
  continuous native-FBW rate reference. Retain immediate stick release and
  hard protection for actual G/AoA exceedance; VTOL and manual input bypass it.
- Retain the reviewed geometry, other flight paths, damage states, detachment
  behavior and single-player boundary.
- Use version 6.0.7 consistently in plugin and package metadata.
- Preserve the canonical `fs20vortexfamily` ID and legacy configuration migration.

This is a complete family package. Do not install separate A/C/T aircraft
DLLs alongside it. Publication and catalog acceptance are separate from
local package preparation; see the release receipt for current delivery status.


## 0.5.10

- Rename the plugin/package ID to `fs20vortexfamily`.
- Preserve the previous catalog ID as an update bridge.
- Retain existing settings and atomically migrate this installation's NOMM metadata, with a backup.
- Keep 0.5.8 aircraft/gameplay and single-player behavior.

# 0.5.8 - Auger plus original Outer loadouts

- Restore all six native Outer options alongside Auger in Inner, including saved presets. Keep the original selector groups and C hook slot.
- Repose Auger and paired Outer racks; retarget per-aircraft support meshes and restore default poses for other Inner loads. All 24 stored combinations pass native geometry checks on A/C and both sides.
- Accept author-approved visual rail clipping. Preserve native damage handling, extend paired Outer guided rails and match door hold to travel. Check active native projectile geometry after rail exit.
- Set finite bay joint force to 350 kN for the combined load budget. Preserve flight tuning, mass, fuel, exterior/UV assets and stock B. Current live firing acceptance remains with the author; no game is launched or closed.

# 0.5.7 — native bay finish, one Auger choice, VTOL remnants

- The author confirmed that the visible grey grid was the previous generated finish. Replace it with a structural-liner region of the installed jet_mechanical albedo, normal, metallic/smoothness and AO maps. Keep the continuous interior UV charts and use matching physical scale across maps. No native bitmap is distributed.
- C's saved preset selected the inherited Aryx Auger carrier. Consolidate same-info/native/Aryx/private Auger choices into the one fitted paired inner-bay carrier, exclude them from the outer bay and migrate known old selections. Preserve other weapons, outboard slots and the C hook slot; config files are not rewritten.
- Remove 428 additional mechanical triangles inside the old upper/lower fan covers, retaining their native skin. Sanitize removed-rotor LOD/damage references and run a bounded cleanup after spawn initialization. Preserve the 1,404-triangle main intake and the stock B.
- The current compiled contracts and 48,244 managed option/migration/geometry checks pass. Native-map crops and Blender renders were inspected. Candidate fixed surfaces are a strict subset of 0.5.6 with no native-skin removal; doors/hook and authored geometry/UV resources are unchanged. Prior release clearance evidence remains applicable.
- Current in-game finish, selector and residual-fan acceptance remain with the author. No game is launched or closed by this update. Flight, fuel, mass and release tuning remain unchanged.

# 0.5.6 — scheduled hook guard and continuous interior UVs

- Guard the native scheduled tailhook callback during departure/spawn. Recover the C fairing controller for C-mounted hooks that arrive without it; leave other aircraft on their original callback. Keep approach arming, go-around relocking and the manual plugin bridge.
- Replace triangle-by-triangle dominant-axis interior projection with authored UV charts. Unfold bay roofs/sidewalls and curved door sections, with deliberate end/rim seams. Embed only new interior coordinates; retain existing geometry and native exterior UVs.
- Reapply conventional paint after part-level livery writes and at the end of initialization. Preserve current livery properties when reusing a cloned material. Log scheduled guard binding and actual GPU atlas pixel changes once per setup.
- Author's 0.5.5 log confirms complete C registration; screenshots report repeated hook notices and broken lining UVs. This revision passes departure scenarios, compiled native callback contracts, interior chart continuity and offline visual review. Gameplay acceptance of this delta remains with the author.
- Fuel, thrust, mass, release geometry, native collider hulls and the stock B assets are unchanged.

# 0.5.5 — inactive-prefab assembly repair

- The author's 0.5.4 game log exposed a NullReferenceException in hook hinge assembly: an inactive-parent search returned no AeroPart. Pass the validated root owner explicitly and pose/scale primitives before registering their renderers.
- Include inactive parents when identifying original bay controllers and structural owners. Preserve the fixed UV skin before retiring the old hinges and clear the intended internal hardware.
- Complete the hook, bay weapon options and paint setup before publishing the C definition, so a failed assembly cannot leave a selectable partial C. Add a completed-template diagnostic to the game log.
- Use lighter grey interior primer, lower metallic/smoothness and explicit opaque rendering instead of the donor's glossy cutout settings. Geometry, fuel, thrust and mass budgets are retained.
- Current compiled contracts, 48,234 managed geometry checks, surface UV/coverage and offline door sweeps pass. The prepared Unity 2022 editor validation could not execute because no active editor license was available; new in-game registration/material confirmation remains pending.

# 0.5.4 — conventional-airframe paint

- Remove both FAN EXHAUST labels and the upper FAN INTAKE warning from A/C paint. Preserve engine blast, canopy and ejection warnings.
- Clone only A/C skin materials and colour textures. Reconstruct the small stencil regions from the surrounding paint; keep native damage, normal, occlusion and metallic maps. No native texture is bundled.
- Reapply the correction after native livery changes. The stock B has no paint component and keeps its original assets.
- Geometry, door motion, flight settings, fuel and mass are unchanged from 0.5.3. Delta checks cover pixel isolation, repeated application, scaled atlases and A/C-only callback registration; previous geometry evidence is retained.

# 0.5.3 — continuous doors and interior finish

- Replace six independently moving leaves per bay with one continuous rigid door on each side, retaining the native exterior paint. Remove internal divider/end-wall seams from the moving lining.
- Use an inner hinge with opening angles of 90 degrees on A and 110 degrees on C, preserving clearance for Auger and the original store rails. No added door colliders.
- Keep the C hook hatch on a stationary hinge axis with captive pins and attachment tabs. Remove the 80 mm translation; retain the door-before-hook interlock and the conformal closed skin.
- Give the bay walls, door interiors and hook well a textured metal finish with panel seams and fasteners. Use metric repeating UVs, one 0.50 m tile, rather than tiny packed triangle islands. Textures are authored procedurally by the plugin; native game textures are not redistributed.
- Offline checks: all four door sweeps, the captive hatch sweep, four Auger release sweeps and 80 legacy-store release cases pass. Live gameplay appearance remains with the author.

# 0.5.2 — native bay selection, doors and spawn self-contact

- Auger uses the original paired inner-bay selector. Remove the two dedicated Auger-only groups; reserve the outer group only when Auger is selected. Native loadout indices and the C hook slot are retained.
- Resolve the correct left/right carrier and 45-degree pose when spawning Auger; restore the original rack position and rotation for other weapons.
- Retire the old door/linkage meshes after transferring their UV skin. Keep exterior strips attached to the fixed bay, including lower-detail geometry.
- Use conformal, single-sided forward skins with intact native door-panel UVs so the old fan/hinge artwork does not appear as holes or folded metal. Preserve the paint/livery materials and fixed-body UVs; rebuild the adjusted panel tangents.
- Scope internal-skin cuts to hatch portals so the wider structural cuts do not remove skin beside closed doors. Remove obsolete mechanical rails inside the new bay without cutting the shared intake/compressor.
- Remove added solid door boxes: 20 closed-leaf cases intersected native fuselage or wing-root collider hulls at spawn. Native part collision hulls, impact damage, breakable joints and hook/cable behavior remain active.
- Offline checks execute the full production skin partition on 59,676 native triangles, paired-loadout migration/exclusion and skin-area preservation. Auger and legacy-store sweeps include the retained native skin and frames. Live spawn confirmation remains with the author.

# 0.5.1 — restore A/C registration and variant selector

- Remove each center-duct island from its own native material submesh. The combined selector in 0.5.0 required nonexistent islands in each material and aborted registration of both A and C.
- Regression reproduces both old material-selection failures, then executes the corrected production selectors on native triangles after VTOL cleanup. Exactly 400 center-duct triangles are removed and unrelated geometry is retained.

# 0.5.0 — expanded internal bays and recessed C hook

- A/C: contoured enlarged weapons bays, raised internal intake route, native-UV moving panels and one native GPO-2P Auger per bay.
- Native gear safety retained. Auger: 0.20 s opening delay, 1.5 m / 2 m/s rail, 1.20 s door hold.
- Auger groups exclude the original inner/outer bay loadout groups; they cannot occupy the same volume together. Left and right Auger mounts retain their respective native carriers.
- Dry budget: A 10,200 kg; C 9,720 kg without the optional moving hook / 9,820 kg with it. The fixed C well weighs 20 kg, optional hook mechanism 100 kg.
- Fuel stays A 3,100 kg / C 2,800 kg. Repartition 190 kg capacity from the affected aft cell into central-side pockets; no added fuel capacity.
- Preserve native internal loadouts with small, mirrored rack adjustments; native weapon definitions and saved mount keys remain intact.
- Bay joints remain breakable: 250 kN force / 300 kN m torque. C hook releases on overload above 900 kN and remains unavailable until respawn.
- Stock B, shared-stock setting, engine thrust, gear and brakes are preserved. Existing C hook loadout index remains before the appended Auger groups.
- Author will conduct the new in-game release/arrestor tests. Offline checks cover compiled skin cuts, both-side release geometry, native API bindings and modeled load budgets.

# 0.4.2 — prepared 2026-11-09

Single-player FS-20A and FS-20C variants alongside the stock B, with a native-style selector, C tailhook and optional shared A/B/C stock in one plugin. C: 110/165 kN; A/C: up to +11 G. Online sessions are blocked.

0.4.2 aligns the C slat hinge axes with its enlarged wings throughout deployment. A/C no longer render the residual lift-fan casing, drive shaft or wing VTOL feeder pipes; shared engine intake ducts and skin are retained. Existing mass budgets and engine settings are unchanged. Mesh selectors were checked against licensed native geometry; this revision still needs an in-game visual check.

0.4.1 added single-player-only entry/transport guards. 0.4.0 merged aircraft and shared-stock code into one plugin. F1 Inventory > Shared airframe stock selects pooled or separate native counters without changing A/B/C availability. Legacy standalone plugin is incompatible and must be removed.
