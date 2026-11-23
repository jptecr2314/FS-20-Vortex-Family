# FS-20 Vortex Family 0.7.2 validation

The production source is assembled from the exact manifests of the locally installed candidate, not the mixed authoring checkout. Only the public plugin version and network signature change from 0.7.1 to 0.7.2 during release preparation. Binary IL/resources are compared against that tested candidate; a separate source checkout rebuilds the released binary.

## Release confirmation

The 0.7.2 binary passed 2,811 reference behavior checks and five fresh private native A/C/B/T/NA spawn/skin/detachment cases. All 1,711 methods match the installed candidate after normalizing only the public version string, and all 17 embedded resources are identical. Private fixtures restored their previous state.

## Relevant local evidence

- FCS: 2,811 executable reference checks, five Unity profiles/776 native control-filter samples, 24 native free-flight cases plus six repeated high-speed NA roll reversals. The final matrix detected zero abrupt pitch-reference cuts and zero detached parts. Worst transient load was 11.94 G, not a certified instantaneous 11 G cap. Tests used full throttle, 25%/100% fuel and 120/300/450 m/s with no external weapons. No new AI/deck-landing or full loaded-flight acceptance is implied.
- Stations: ten native C/T cases covered five supported new single carriers, actual fire/release, old loadout migration and structural ownership. C tip missiles/rails/NAV followed the detached tip. Geometry checks found no intersections in the tested neighboring loads and 206 C/774 T movable-surface poses. T outlet clearance is a geometric reserve, not thermal or loaded-hover certification.
- A/C outlet cleanup: five native private spawns A/C/B/T/NA; only A/C received the cleanup. Six affected meshes per A/C; 153 other A meshes and 154 C meshes unchanged, plus all exported B/T meshes. Native painted renders and skin detachment inspected. Atlas chart splitting adds 738 triangles per A/C, with no new texture, shader, renderer or per-frame component loop. Lower paint fields share existing intact upper atlas regions; not every Workshop livery was inspected.
- Earlier 0.7.1 paint and network evidence remains historical: 3,556 rendered checks, 183 native contracts and 31 native network checks on one PC. Those are not new 0.7.2 network results. All 13 external paint bundles remain byte-identical.

Matching-build multiplayer remains experimental. Two-PC Internet latency/late join, every weapon combination, asymmetric-damage combat, pilot GLOC tolerance and a new FPS benchmark remain outside this delta validation.
