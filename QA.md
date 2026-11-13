# 0.6.8 validation — 2026-11-13

Scope: isolated A/B/C/T candidate from published 6.0.7. FS-20NA is absent. No installation, Nuclear Option process, Steam login, public lobby or two-PC flight was used.

Observed checks against the final candidate:

- **105 Unity/native integration checks**, using the real Mirage MessagePacker, MessageHandler and NetworkPlayer over an in-memory wire. Matching builds connect through the fixture, differing builds/native authentication failure/tampered acknowledgement are rejected, and a fresh connection works after disconnect. Authentication results in this fixture are controlled inputs, not real Steam authentication.
- **26 patched runtime methods** bound in Unity; **24 Harmony patch-class contracts** independently inspected against the installed native game and Mirage assemblies. The original native authentication result is awaited; Steam, password and game-build verification are unmodified.
- **503 family pool/menu checks**, **131 existing T pitch-governor checks**, and **70 bounded surface-packet checks** passed. Flight tuning and all seven authored resources remain from 6.0.7.
- Host stock/flap policy reaches the client fixture while its original configuration bytes remain unchanged and its preferences return after disconnect.
- Remote C native slats and hook deployment/overload animate; remote arresting forces and A/C/T assistance are suppressed. T slats move without changing remote wing area and stop being animated after detachment/fragmentation.
- Actual T geometry was applied to the inactive native donor and cloned twice: **42 parts**, repeatable hierarchy order, **8 independent damage owners**, and structural references confined to each clone. Runtime registration/spawning and native damage RPC execution were not simulated by that geometry check.
- Surface packets round-trip through the native serializer and are rejected by Mirage before authentication. Application-level validation also checks the accepted session, native simulation owner, visible observers, payload bounds, indices, finite values, sequence and cadence. Those ownership/visibility branches were reviewed in source; this bench does not claim a live flight/visibility test.

The local fixture caught and corrected the old prototype's inverted `allowUnauthenticated` flag. Compatibility messages must be allowed before admission; surface data must require native authentication. A malformed acknowledgement also returns failure immediately. Compatibility waiting uses unscaled time.

Presentation sends complete snapshots at 5 Hz, bounded to 659 payload bytes (about 3.3 KB/s per aircraft per observing peer before transport overhead). This is a designed bound, not a measured multiplayer bandwidth/FPS benchmark. Measurements come from completed aerodynamic jobs independently of the owner's rendering distance. Stale received vapor loads expire after 0.45 seconds; remote copies retain native controls/damage/detachment. No forces or health values are accepted from the presentation packet.

## Remaining acceptance

A real private Steam session on two installations is still required to validate lobby entry, selection and spawn of all variants, observer updates, remote vapor appearance, weapons, shared-stock requisition/return, damage/detachment, in-progress joining and reconnect under latency. Current evidence does **not** certify those end-to-end behaviors or general compatibility with arbitrary third-party mods. Shared-stock algorithms passed locally; multiplayer supply RPC convergence remains part of that session test.

Receipts: `D:/Artifacts/fs20-multiplayer/0.6.8/BUILD.json`, `native-bench.json`, `NATIVE_CONTRACTS.json`, `validation.json` and `delivery.json`. The older protocol-only TCP run belongs to the prior prototype; it is not counted as final game-network validation. A later standalone protocol-executable rebuild was denied by Windows file access; it was not rerun under another filename. The current protocol was exercised by the native Unity fixture.

The public source includes the machine-readable reports under `docs/qa/0.6.8/`. Paths above identify the original local evidence locations; they are not required for building the plugin.

Historical 6.0.7 verification is retained in [the prior QA record](docs/qa/6.0.7/QA-history.md); those results do not certify multiplayer.
