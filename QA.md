# FS-20 Vortex Family 0.7.0 — validation, 2026-11-18

Tested DLL SHA256: `ae06c05e010e3cc8a9a4f2b3c165816e85180497facfde972b7a3bae0a73f590`.

- 183 native Unity/Mirage contract assertions passed on the release DLL: compatibility/authentication rejection, authority, controls, part indices, slats, hook policy and structural clones. The fixture's wire is in memory.
- 922 assertions passed in a private copy of Nuclear Option 0.34.6 with Direct3D11: B/A/C/T/NA spawning, all 12 own paints, native distant vertex colors, identical mesh positions/normals/UVs/indices/bounds and alpha, shared distant meshes, renderer lifetime, native detachment, eviction and reload. Twenty-four native wet-weather renders cover A/T/NA near and reduced levels.
- A legitimate Steam client and hidden anonymous dedicated server passed 31 checks in separate native processes on this PC. Native authentication, FS-20 handshake, A/C/T/NA remote spawns, simulation ownership, exact A/T/NA body/wing texture references and T section detachment were observed. Three optional paints loaded asynchronously on each peer.
- All 21 existing BC7 mip payloads and import properties remain byte-identical: 117,441,072 bytes total. The corrected UV/normal/geometry assets are unchanged by demand loading and distant tinting.
- Startup paint objects fell from 21 to 6: about 112 to 32 MiB of compressed mip payload. This excludes native textures, normals, meshes, driver overhead and custom packs; it is not measured total VRAM. Optional paints use native async bundle/asset requests and shared ownership.
- A fresh minimal creator-kit Editor project exported its example .fs20paint successfully; the resulting pack passed 387 assertions in the private native game, including selection, distant colors, mesh sharing, detachment and reclamation.
- Installer transaction tests passed for locked-file refusal, injected mid-copy rollback, complete payload migration, custom paints/other plugins/config preservation and reinstallation. Final package extraction/install and source rebuild have separate release receipts.

## Limits

The Steam test used one computer and one licensed client plus an anonymous server. It is not two-PC Internet flight, human-versus-human combat, latency/late-join acceptance or every mission/mod combination. Matching-install multiplayer remains experimental. Native authentication was never bypassed.

Frame profiling used a hidden, controlled 1400x1000 native render-request workload. It is not normal fullscreen gameplay FPS or proof that all stutters are eliminated. Native B/C paths remain unchanged. A first B spawn and C paint conversion can still have their existing costs. The old preloaded and demand-loaded versions had similar warm spawn/burst timing in this limited run; the confirmed improvement is fewer startup paint assets and bounded optional loading.

Native distant body meshes use their original low-detail shader and average paint color, preserving vertex shading and alpha. They do not gain detailed camouflage/insignia texels. Detailed UVs, faction markings and BC7 atlas resolution are retained. No geometry was pruned to solve the material issue.

Historical QA under docs/qa/0.6.9 documents that release; it must not be treated as current 0.7.0 evidence.
