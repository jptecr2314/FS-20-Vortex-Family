# FS-20 Vortex Family 0.7.1 validation

- 3,556 checks passed in a private Nuclear Option 0.34.6 Direct3D11 run. Coverage includes all 12 built-in paints, the selected wing atlas on T and NA, NA damage material slots, distant finish continuity, preservation of LOD geometry and alpha, and paint release after detachment and teardown.
- 183 Unity/Mirage contract checks passed on the same DLL, including the in-memory native wire fixture.
- 31 native network checks passed with a Steam client and an anonymous dedicated server running as separate processes on the same PC. Coverage includes authentication, the FS-20 handshake, remote spawns and T wing detachment.
- All 13 built-in bundles match 0.7.0 byte for byte. Startup held six prepared paint textures and teardown left zero instance references.
- The optional creator kit carries forward the previously validated templates and exporter with updated version references.

Coverage is limited to the controlled game workloads and contract fixtures. Matching-build multiplayer is experimental. The 0.7.0 verification records remain in docs/qa/0.7.0.
