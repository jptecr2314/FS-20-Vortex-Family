# FS-20 Vortex Family 0.6.8 — multiplayer candidate

A/B/C/T only, based on published 6.0.7 for Nuclear Option 0.34.6 and Blueprinter 2.1.1. FS-20NA is excluded. Existing 6.1.x development and the installed plugin are preserved.

Use a private lobby with the same 0.6.8 DLL, game build and mod set on the host and every client. Enable **Multiplayer → Enable matching modded sessions** on all participants. Hosts must explicitly enable the aircraft. A missing or mismatched installation is refused; this does not make the aircraft compatible with an unmodded server.

The native game continues checking Steam, password and game build. A separate exchange checks plugin binaries and network definition order, prefab hashes and FS-20 part layouts. Matching third-party plugin DLLs do not prove matching third-party content or every configuration setting; install those identically too. This is a compatibility check, not an anti-cheat system.

The host chooses shared airframe stock and C flap policy for the whole session. Changes take effect after restarting the lobby. Client settings on disk are preserved. Native controls, position, weapon release, damage, part detachment and supply replication remain in use. A bounded presentation stream carries measured C/T vapor loads, slats and arresting hook states at five updates per second to existing native observers. Remote copies do not run the FS-20 flight controller or arresting forces. Stale vapor samples expire.

Keep only one VortexConventional DLL per profile. Backups belong outside BepInEx/plugins. The requested version is literally **0.6.8**: it sorts below 6.0.7/6.1.x, so do not expect a version-based mod manager to offer it as an automatic upgrade from those releases.

See QA.md for what was actually verified and what remains unverified. No installation or publication is performed by the build script.

## Source and local verification

Build with PowerShell 7 and a licensed local game installation:

```powershell
./build.ps1 -GamePath 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
./test-family.ps1 -GamePath 'C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option'
```

The build restores the pinned compiler when necessary; native game/Unity assemblies and dependencies are not distributed. The family script checks compiled family helpers, stock configuration persistence and the T pitch governor. It does not perform a multiplayer flight. The former five-hook single-player-only guard test belongs to earlier releases and is replaced by the network checks described in QA.md. Native Unity fixture source is in `tests/multiplayer/NativeBench.cs`; it needs the locally imported licensed Blueprinter authoring references. Machine-readable historical results for this binary are under `docs/qa/0.6.8/`.
