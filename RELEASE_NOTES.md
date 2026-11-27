# FS-20 Vortex Family 0.7.4

- Fixes the multiplayer lobby error caused by an empty Assembly.Location when BepInEx loads a DLL into memory. Compatibility checks use registered loader paths or the native managed game path, validate the assembly identity and retain full-file SHA256 checks.
- Leaves the native authentication task and Steam/password/build checks intact. After native authentication completes, game admission waits for the matching FS-20 handshake. This fixes the race where the game's Steam-session monitor could read authentication data before Mirage stored it.
- Adds explicit startup diagnostics and full exception logging. Missing or incorrect mod files remain rejected rather than silently skipped.
- Guards VTOL balance refresh during native aircraft/payload teardown, preventing an exception when the owning aircraft has already been destroyed.

All A/B/C/T/NA aircraft, paints, stations, 240 kg A/C rail policy and native stores MFD from 0.7.3 are retained. Flight tuning, weapon definitions and network IDs are unchanged.

Requires Nuclear Option 0.34.6, BepInEx 5 and Blueprinter 2.1.1 or newer, supplied separately. Install the complete package with the game closed; keep BuiltinPaints beside the DLL. Host and every client need the same 0.7.4 package and compatible game/weapon mods. Mixed family versions and unmodded servers are unsupported.

Verified with a hidden native Steam server and a legitimate client in separate processes on one PC, including byte-loaded plugin hashing, spawning the four modified variants, paint binding, eight missile launches/rearms, native T detachment and disconnect/reconnect/late spawn. This is not a two-PC Internet/latency or human combat certification; multiplayer remains experimental. See QA.md for exact coverage.
