# FS-20 native AI calibration

## NA pursuit and fine gun alignment — 2026-11-17, current local candidate

The current candidate builds on the A/C/NA calibration below (2F7AEF30…968616).
Tested/rebuilt DLL SHA256:
`97F5D6938F92DB723060CD2731A572111A7349165DCE47406E9D8BC569EB18A0`.
It is not installed or published. The installed family remains 814DD9C3…CF7F5.

`Fs20NAAimPolicy` progressively refines pitch/yaw near gun alignment using
target line-of-sight rate minus actual body rate. It releases on large bank,
rapid roll, abrupt target motion, stale aim points, low energy or low height.
Existing coarse-axis gains and roll output remain unchanged. The correction is
bounded to 0.35 normalized command times capture weight before native clamping;
native integrators, physical authority, FBW, G limits and damage are retained.
An approach throttle ceiling uses actual closure/time to contact and releases
when energy or geometry requires it; it never raises the native throttle request.

Native `UseFixedGuns` has a ballistic point followed by extra steering-only
anticipation. The observer reconstructs the first point from that exact native
calculation, preserving original execution and firing eligibility. Recomputing
ballistics or damping against zero angular rate would lose moving-target aim.
Capture is restricted to authoritative NA AI gun pursuit, with gear up and
FBW/assist enabled. Player, remote, A/C/T/B and runway paths are excluded.

Four native rendered NA/B duels completed at 1x, full initial fuel, gun-only,
1100 km/h spawn and infinite ammunition, with only three plugins. NA remained
active in all four; B became inactive in three and one reached the time limit.
This is an operational result, not four confirmed gun kills or a balance study.
Actual PID witnesses recorded 318 bounded capture corrections, 4,023 unchanged
outputs (including roll during capture) and 921 unchanged strong commands
outside capture. Reconstructed ballistic-point mean error was below 0.002 deg
in every case. All 15 assets and previous coarse gain IL are identical.

The standalone test executable compiled but Windows Application Control blocked
execution; those assertions are not counted as passed. Native testing proceeded
independently. No all-loadout, landing, multiplayer or frame-time certification
is claimed. Physical G transients exist; this is not a hard instantaneous G cap.
Evidence/source/build: `D:/Artifacts/fs20-ai-control/2026-11-17/na-capture/`;
see `REPORT.md`, `delivery-verification.json`, `delta-verification.json`,
`baseline/`, `trial-1/` and `repeat/`. The private runtime and installed DLL were
restored; no game process was left running.

## Base A/C/NA revision — 2026-11-17, superseded local candidate

User scope: adapt A/C/NA to their capabilities and preserve the accepted T.
The candidate starts from the installed own-livery revision, SHA256
`814DD9C3C3708120556720A43769C0C4012723203C21A50DBC21CE5BF41CF7F5`.
It is **not installed or published**. Final tested/rebuilt DLL SHA256:
`2F7AEF30A87C6264BB930956F87A1434290EEFA79D2CD6FAC2C0915265968616`.

`src/Fs20AiCalibration.cs` retains T at its installed 100 m/s corner / 300 m
radius, superseding the uninstalled T change from the older proposal below.
A/C/NA use 220/200/200 m/s energy references; C/NA radius metadata is 450 m.
`src/Fs20AiGuidance.cs` calibrates native outer-loop axes before command clamping:
A pitch/roll .81818/.87266; C .81818/.65450. These are partial-response gains,
not reduced G limits, forces or available actuator travel. The native B roll
setting was measured at 5 rad/s; the 4.18879 rad/s settled-response reference
used for A/C damping must not be described as the stock B setting.

NA schedules pitch/roll from .5/.5 at 160 m/s EAS to .81818/1.19366 at 220 m/s
EAS, and normalizes the finless yaw allocator's .65 rad/s command contract in
the fast regime. Player/remote/B/T paths are excluded; gear-down paths stay
native and gains blend back near the ground/low speed. Native PID state,
inner FBW, physical authority and damage remain intact.

Final native rendered-game validation: six mirrored full-fuel gun duels,
1x, 1100 km/h spawn, native movable camera, unlimited ammunition and exactly
three loaded plugins. A/C/NA fired in both of their final rounds; **18 actual
PID-output checks** and AI/manual/null/B guards passed. C's pre-first-shot
absolute AoA P95 changed from 31.25/22.17 to 11.28/17.75 degrees. Final rounds
fired A 456/350, C 80/586, NA 306/434. Shot counts are not hit counts or a
statistical ranking; post-hit and landing minima are not clean flight metrics.

The compiled delta preserves the exact T/B profile case blocks, all existing
physical/player controller IL and all 15 embedded resources. Only AI profile
constants, the two AI hook registrations and the added guidance policy differ.
The installed DLL, private addons and temporary test files were restored.
Evidence, rejected attempts, trace interpretation and remaining scope limits:
`D:/Artifacts/fs20-ai-control/2026-11-17/REPORT.md`, `delivery-verification.json`,
`final-delta-verification.json` and `candidate-3-duels/`.
The final candidate source is under that evidence root; only its two AI files
were integrated into the workspace, with pre-revision copies retained.

## Native gun-duel investigation — 2026-11-17

The current mission-combat investigation uses the installed paint revision of
0.6.9, SHA256 `814DD9C3C3708120556720A43769C0C4012723203C21A50DBC21CE5BF41CF7F5`.
It does **not** contain the uninstalled control candidate described below.
Runtime corner-speed metadata remains A/C/T/NA = 140/120/100/100 m/s, with stock B
at 180 m/s. Do not attribute its results to the newer source-only profile.

`tools/run-fs20-isolated-gun-duels.ps1` runs the native rendered game in the
existing private `FS20AIControl` copy/profile. It temporarily parks that copy's
Blueprinter addons and copies the installed family DLL, keeping only Blueprinter,
the family and `NativeFs20GunDuels` telemetry. It restores private addons and removes
its temporary DLLs/mission in `finally`; the installed game is not modified.

The user requested B/B and A/C/T/NA versus B, mirrored sides, full initial internal
fuel and gun-only loadouts. The revised viewing/test conditions are **1x time,
1100 km/h spawn velocity and unlimited gun ammunition**. The native orbital camera
is selected once per round; the probe does not overwrite its pose every frame.
Unlimited ammunition replenishes only owned duel guns after their native firing
tick, restores the removed ammunition mass through native `Gun.Rearm`, and records
actual rounds fired separately. It leaves heat, fire rate, recoil, projectiles,
damage and native AI decisions intact. This is a deliberately modified ammunition
condition, not a finite-magazine endurance or weapon-economy test.

The mission uses native combat pilot states, aircraft physics, joints and damage.
Native HQ tracking is refreshed equally for both opponents to isolate flight/gun
decisions from sensor acquisition. Requested altitude is 3000 m above the reference
airbase and spawn separation is 2500 m; one second of native settling occurs before
telemetry release, so actual release speed/separation and initial fuel must be read
from receipts. Fuel burns normally. Two rounds per pairing with 120 simulated
seconds maximum are diagnostic screening, not statistical combat balance evidence.

Evidence root: `D:/Artifacts/fs20-gun-duels/2026-11-17/isolated/`.
The first isolated run was interrupted by the mission Quit button and is incomplete;
earlier all-mod runs also had superseded tracking instrumentation. Do not use them
as a completed baseline. The revised run is `normal-speed-infinite-run1`; inspect
`duels.json`, per-round JSONL, `Player.log` and `restoration.json` before claiming
completion. `analyze_fs20_gun_duels.py OUTPUT_DIRECTORY` produces screening metrics;
filtered command suppression by itself does not establish a defective limiter.

The revised run completed all ten rounds. `verification.json` passed: native
rendering, only three loaded plugins, no accelerated samples, native camera,
full initial fuel, gun-only loadouts, ammunition replenishment, matching native
managed assembly, unchanged installed DLL and completed restoration. Actual
firing counters totaled 5646, while sampled ammo remained 500. See the dated
run's `REPORT.md` and `metrics.json` for exact participant results and limits.
C showed pre-first-shot absolute AoA P95 of 31.25 degrees in round 0; NA stayed
active in both rounds but fired zero. T stayed active and fired in both. These
are priorities for further diagnosis, not a certified ranking or proof of one
specific controller root cause. No production AI correction was applied.

## Historical local control candidate — 2026-11-15

This correction starts from the published **0.6.9** source snapshot, not the
newer unrelated authoring work in the workspace. It is **not installed or
published**. Evidence: `D:/Artifacts/fs20-ai-control/2026-11-15/`.

The previous planning-only calibration was insufficient evidence of actual AI
flight. Native free-body tests reproduced excessive altitude excursions and a
full-fuel NA stall during a close-target turn reversal/climb. Stock B is the
unchanged control. A four-G trim threshold had been used as `cornerSpeed`, but
native AutoAim also uses this field for bank scheduling and close-target
pull-up; native combat logic uses it for energy/throttle decisions.

| Variant | Published corner (m/s) | Local corner (m/s) | Local turn-radius metadata |
|---|---:|---:|---:|
| A | 140 | 220 | 550 m |
| C | 120 | 200 | 450 m |
| T | 100 | 200 | 450 m |
| NA | 100 | 200 | 450 m |
| B | 180 | unchanged | unchanged |

Nine-G static, full-internal-fuel, clean trim at 500 m found feasible sampled
references A/C/T/NA at 220/200/160/160 m/s with alpha <=18 degrees and pitch
demand <.9. T/NA require additional transient reserve: adopting 160 directly
still failed coupled full-fuel reversals. These are guidance references, not
stall speeds, takeoff requirements, certified structural limits or sustained
turn ratings. Landing, takeoff, recovery-distance and cruise-throttle fields
are unchanged. T retains VTOL.

NA additionally uses **0.5 gain on its native outer attitude-loop pitch/roll
outputs**, before native command clamping. `Fs20AiGuidance` scopes this to the
specific native AeroPID instances inside an authoritative AI-owned NA's
AutoAim, with exception-safe restoration. It retains integrator/derivative
history and leaves native yaw, the inner FBW, physical forces and actuators
unchanged. Player-controlled/remote aircraft and A/B/C/T receive unity gain.
The gain blends back to native near the ground and at low speed. No shared
PIDFactors are modified and there is no extra physics simulation.

Current native close-target results, **full fuel**, 160 m/s requested:

| Variant | Mean altitude error, published -> local | Peak absolute AoA, published -> local |
|---|---:|---:|
| A | 25.7 -> 18.9 m | 33.7 -> 31.2 deg |
| C | 30.3 -> 18.4 m | 35.4 -> 31.1 deg |
| T | 288.5 -> 24.3 m | 13.5 -> 12.1 deg |
| NA | 292.6 -> 29.3 m | 63.3 -> 12.4 deg |

NA minimum speed improved 82.2 -> 152.6 m/s. Ten paired cases cover B/A/C/T/NA
at 35/100% fuel. All completed the controlled route; actual fuel was checked.
The private probe checks live pitch/roll PID results (20 checks), rather than
only confirming Harmony registration. It also checks AI/manual/null eligibility.
A/C still have brief AoA transients above the nominal envelope: do not call the
entire mission AI certified from this bounded result.

The test uses the native game, free rigidbodies, actual forces, joints, damage,
actuators and AutoAim. Its own test pilot state holds requested speed and asks
for level flight, +45-degree turn, 90-degree reversal and +150 m climb. It does
not exercise native mission combat/weapon decisions, takeoff contact, multi-ship
formation convergence, VTOL transition or a landing-state/contact cycle.
The second paired native sweep used 240 m/s, full fuel and nose tracking:
mean altitude errors A/C/T/NA improved 181.6/247.6/229.2/236.9 m to
25.7/26.3/24.9/31.6 m. B remained 33.1 m. All five routes completed without
the low-energy NA departure; live output/eligibility checks passed again.
Combined evidence is 15 paired candidate cases and 30 actual PID-output checks.
Final DLL SHA256: `CE08C28A5466FABADB67D85FC194381741867A6073F611FEA93C2BA26673E9A5`.
The same hash was reproduced by a second build. Receipts:
`delivery-verification.json`, `delivery-delta-verification.json`,
`final-close-comparison.json` and `final-fast-comparison.json` in the evidence root.

Reproduce with `tools/run-fs20-native-ai-control.ps1`; the existing private copy
and profile are `D:/Build/nuclear-option-qol/fs20-ai-control/game` / FS20AIControl.
The runner refuses concurrent use, stores source/hash/log/CSV receipts and
restores private plugins. A graphics device is required for initialization;
it runs hidden in batch mode and does not change the installed game. Null-GPU
initialization failures are infrastructure failures, not flight evidence.
`tools/analyze-fs20-ai-control.py` compares paired traces, including phase RMS
and tail convergence. `tools/verify-fs20-ai-control-delta.ps1` verifies the
compiled change scope and all 14 embedded resources against published 0.6.9.

Rejected experiments remain in the evidence directory: extra pitch feedforward,
blanket half gains, a reordered native bank clamp, static-only T/NA corner
references, a low NA bank cap and AI yaw suppression. None is shipped in this
candidate. The earlier reduced single-axis bench stabilized every variant and
therefore did not reproduce the coupled failure by itself.

## Historical planning study — 2026-11-14

The following profiles and worktree version names document the earlier study.
Its corner-speed/turn-radius recommendations are superseded above; its measured
scope limitations and independent takeoff/landing work remain relevant.

Local revision: 2026-11-14. Implementation: `src/Fs20AiCalibration.cs`.
This is a planning/navigation calibration, not a replacement combat AI.

**Dynamic loadout-aware advisor, 2026-11-14:** `src/Fs20DynamicTakeoff.cs` and
`src/Fs20TakeoffPlanner.cs` now provide an on-demand A/C/NA prediction and an AI
takeoff-state-entry advisory. This adds fuel, actual mass/CG/pitch inertia,
external-store drag and deck-relative headwind; it does not command rotation,
veto launches or change `takeoffDistance`. Current evidence and limits:
`D:/Artifacts/fs20-dynamic-takeoff/2026-11-14/REPORT.md`, `summary.json` and
`validation.json`. 660 main scenarios plus 1,512 C wind-sweep samples passed
3,520 checks using the embedded production calculator. Fuel was 20/35/60/100%,
with clean plus 17 legal symmetric native external mount pairs per variant.
The C sweep samples headwind at 1 m/s resolution through 20 m/s. It establishes
conditional model limits, not a certified Cursor departure or native mission
test. Runtime snapshot extraction and the complete preflight invocation still
need mission integration validation; Harmony binding was checked locally.
The installed game was not changed. The generic-pilot takeoff study below is
historical and superseded for this advisor by the wider force domain and
nominal-native-P/D/I reduced pilot.

For the later afterburner/rotation ground-run study and its Cursor comparison,
see [FS20_TAKEOFF_BENCH.md](FS20_TAKEOFF_BENCH.md). Those reduced-model distances
remain separate from the recovery-selection metadata below.

**Runway-policy correction, 2026-11-14:** the earlier 1,400/1,000/800 m values
were incorrectly promoted from a simplified dry-thrust estimate to operational
requirements. They are withdrawn as takeoff minima. Current candidate and delta
receipt are in `D:/Artifacts/fs20-ai-calibration/2026-11-14/runway-review/`.

## Profiles

Speeds below are **m/s**. Landing and approach references must not be read as
actual touchdown speeds: the native landing state computes
`adjustedLandingSpeed = landingSpeed * sqrt(currentMass / aircraftInfo.maxWeight)`.
The installed native maximum-weight reference is 16,000 kg for these donor-derived
definitions. The aircraft's G limit, maximum weight, fuel, physical surfaces and
hover controllers were not increased or replaced by this calibration.

| Variant | Takeoff parameter | Landing reference | Approach reference | Corner | Existing recovery metadata | Turn radius | Cruise throttle |
|---|---:|---:|---:|---:|---:|---:|---:|
| A | 110 | 110 | 125 | 140 | Previous 800 retained | 550 m | 0.40 |
| C | 95 | 95 | 110 | 120 | Previous 50 retained | 400 m | 0.40 |
| T | Native VTOL threshold retained | 85 | 100 | 100 | Native VTOL value retained | 300 m | 0.45 |
| NA | 85 | 85 | 100 | 100 | Previous 800 retained | 300 m | 0.40 |
| B | Unmodified native VTOL control reference | | | | | | |

The recovery column contains inherited `takeoffDistance` metadata. Despite its
name, the inspected native consumers use it for recovery/base selection and UI;
`AIPilotTakeoffState` and `AutopilotPlane` do not read it. It is not a rotation
command or a measured physical takeoff distance. A/NA retain the pre-calibration
800 metadata for return/base searches; this does **not** assert that either needs
800 m to take off. The C's legacy 50 likewise does not prove a 50 m ground roll.
The user reported successful C takeoff from the Cursor; that evidence must be
preserved, without extending it to AI-controlled launch or Cursor recovery.

The calibration profile now preserves caller-supplied `takeoffDistance`; its old
800 m floor and its conversion from dry-roll estimates have been removed. Existing
factory recovery policies remain separate. Native suitability also checks runway
type/level and can use arrestor, hook and relative landing speed; this single field
does not establish carrier capability. The simplified estimate omitted rotation,
afterburner and deck motion and is now **advisory only**, stored separately as
`advisoryDryRollEstimate`. Physical takeoff minima for A/NA remain unmeasured.

Full-internal-fuel, clean-airframe trim at 500 m produced minimum sampled speeds
below 10 degrees AoA and 85% pitch demand of: A 77.5, C 67.5, T 57.5 and NA 57.5 m/s.
Planning targets add margin and are rounded upward. Four-G symmetric trim cases
set a conservative initial corner/turning-radius reference. Cruise settings use
trimmed drag/dry thrust plus reserve. Flight-controller gains, terrain avoidance,
weapon decisions and hover PIDs remain native. The previous A/C pitch fix remains
included in the candidate.

## Local verification and exact limits

Current candidate DLL and delta results:
`D:/Artifacts/fs20-ai-calibration/2026-11-14/runway-review/candidate/VortexConventional.dll`
and `D:/Artifacts/fs20-ai-calibration/2026-11-14/runway-review/validation.json`.
The earlier full-bench receipt below belongs to the pre-correction candidate;
physical/control paths and embedded assets were verified unchanged for reuse.
The narrow policy correction passed 15 native parameter checks, including custom
caller values and VTOL threshold preservation. No takeoff-distance measurement
is claimed by those checks.

The private headless Unity bench completed 49 phase/profile checks across B/A/C/T/NA,
with 190 trim/force cases and 25 native autopilot command-response cases:

- Takeoff: advisory maximum-weight, one-dimensional dry-thrust runway-roll estimate and a
  native forward-flight climb command at the wingborne planning speed.
- Turns: static four-G force/moment trim plus native right-turn command direction.
- Formation: native left-offset join command direction. No multi-aircraft formation
  convergence, collision avoidance or moving-slot tracking is claimed.
- Combat: native climb/turn interception command direction. No target selection,
  missile evasion, weapon release, damage or kill result is claimed.
- Landing: static approach trim margin, correct maxWeight normalization and native
  descent-command direction. No complete landing-state cycle, flare, wheel contact,
  braking, arresting-wire recovery or carrier suitability is claimed.

The native command tests call `AutopilotPlane.AutoAim` and its native input-filter
path on the production-derived airframes. They hold pose/velocity constant while
sampling responses: these are **not closed-loop flight tracking tests**. Controlled
sensor caches supply zero wind and no terrain warning. This is necessary to isolate
guidance in an editor fixture without a running mission; it is not a change to the
installed game. The T's VTOL capability flag and takeoff thresholds are preserved,
but hover/transition/contact performance was not validated by this forward-flight
bench. External stores, changed CG, weather, high-altitude airfields and damage
are outside the force fixture.

The bench uses private imported native meshes, joints and UV references. Missing
serialized hook references are hydrated only in private copies using the observed
unique native hook node. A/T factories now guard `DontDestroyOnLoad` with
`Application.isPlaying`, as NA already did; gameplay behavior is unchanged by that
editor-lifecycle guard.

## Reproduce

`tools/run-fs20-ai-calibration.ps1` preserves the user's authoring scene and uses
the private cached project `D:/Build/nuclear-option-qol/fs20na-bench`. It refuses
to launch when that project is already running. Bootstrap it once after changing
the fixture source, then run the native method with `-ApplyCalibration`. All methods
are headless and do not open Nuclear Option. Local reference files
`native-meshes.bin`, `native-joints.json` and `native-uvs.bin` must be siblings of
the selected output folder. They remain private and are not shipped with the mod.

The runner checks receipt completion, the candidate DLL hash and every phase result.
`tools/verify-fs20-ai-calibration.ps1` also checks profile coverage, mode preservation,
mass-scaled landing margins, the compiled patch scope and embedded asset hashes.
Use `-Mode RunwayPolicy` for the narrow metadata fixture and
`tools/verify-fs20-ai-runway-policy.ps1` for its delta verification. Existing older
full receipts encode the withdrawn metadata and are historical; do not relabel
their hash as the current candidate or silently edit their results.
The full results remain in `final/ai-calibration.json`, including unsuccessful low
speed trim samples; those samples are not silently counted as successful flights.

The family source is 6.3.2. This candidate is not installed or published. The installed
6.3.1 DLL remains unchanged. Full mission/closed-loop behavior remains an integration
validation task; no pilot learning, combat competence or automatic success guarantee
is implied by these planning profiles.
