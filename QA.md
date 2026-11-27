# FS-20 Vortex Family 0.7.4 validation

## Current candidate

DLL SHA256: `a675b8d1a2e13b50a48dc189cb763d0197c6897ec53c91e6cc2211479f06ff14`. Source starts at the public 0.7.3 commit. The compiled scope comparison preserves 1,777 methods outside admission, all 17 embedded resources, flight/weapon tuning and all 13 external paint bundles. Changes are confined to assembly fingerprint resolution, admission/diagnostics, version metadata and a teardown null guard.

## Executed checks

- 12 assembly-loading/file-hash regression checks, using actual byte-loaded and disk-loaded .NET assemblies: the previous failure is reproduced, valid file SHA256 is retained, changed bytes mismatch, missing/wrong files reject, and installation path names do not enter the signature.
- 124 compatibility-policy checks, including two TCP loopback exchanges, malformed packets, mismatched digests/protocol, nonces, native-authentication failure and host-policy enforcement; 70 bounded surface-packet checks. These are standalone protocol fixtures, not Steam tests.
- 185 Unity/native-Mirage checks and 11 in-memory wire packets; native game references, data fixtures and explicit authentication fixture results. Matching, mismatch, tampered ACK, native failure, repeated sessions, owner-only controls, C/T/NA presentation, damage/index contracts, bounded surface handling and retained compressed paints were checked. This does not simulate a successful Steam login.
- 23 static native hook contracts: the native authentication task is not patched/replaced; successful native metadata precedes game admission; identity/password/build checks and native definition indices are untouched. Static checks alone are not runtime proof.
- Real native game processes: hidden password-protected anonymous Steam server plus a legitimate client, on one PC. Final server/client receipts contain 37 / 41 assertions. Both execute the complete production signature with an actual byte-loaded temporary plugin and confirm it remains included in the digest; changing its file bytes changes the digest. Temporary registration is restored before the real handshake.
- The native client receives A/C/T/NA, verifies remote authority and selected paint texture references, observes eight AAM2_single missiles on A/C rails and C/T middle stations, and confirms native fire and rearm ammo states. Server and client use native weapon APIs/RPCs. A native T wing-section detachment reaches the client. The client then disconnects through native StopAsync, verifies policy reset, rejoins the same server and receives the variants again.
- Final native logs contain neither the empty-path error nor the observed authentication-data/VTOL-teardown null references. Fixtures restore their private plugins/mission and preserve the installed DLL. Other tasks' processes are not stopped.

Initial fixture attempts that did not complete, including a reused Unity component identity and a child-hierarchy store lookup invalidated by native reparenting, are excluded from these results. Final checks use a unique byte-loaded plugin type and WeaponManager hardpoint indices. Earlier 0.7.4 candidate results are not counted as current-binary acceptance.

## Delivery and limits

The package must pass archive CRC/hash checks, an isolated install preserving configuration/unrelated files and an identical source-archive rebuild before delivery. This release has not been published or installed as part of preparation. Full real-flight combat, every third-party weapon, asymmetric-loss/Internet latency and a two-PC Steam session remain untested. Matching installations are required; the original B remains native and was not a new variant-spawn case in this delta.

## Historical 0.7.3 evidence

# FS-20 Vortex Family 0.7.3 validation

The release source starts at the public 0.7.2 commit and includes the cumulative locally installed station, selector, MFD and wingtip revision. Publication changes only version metadata and the multiplayer build signature from that tested revision. A method/field/resource comparison checks that boundary; the source archive must rebuild an identical DLL. Packaging checks ZIP CRCs, dependencies, all paint bundle hashes and an isolated install. Release assets are downloaded anonymously and hashed after publication.

## Current delta evidence

- Wingtip role/provenance and alias routing: 56 helper checks, 41 Unity routing checks and six Harmony registration/injection checks under Unity Editor Mono. These use native-signature data stand-ins. Options must already be offered at an external aircraft station; role alone is insufficient. Available choices are deduplicated after native availability, retaining compatible saved aliases and registered definition IDs.
- Alignment: 64 placements using actual native/mod prefab geometry, 16 mounts and both sides of A/C; 645 checks. Inactive private game templates supplied the meshes. No contacts were found against tested neighboring tip/wing/NAV/slat/aileron geometry in the neutral pose. Attachment prefix registration and native attach/rearm/fire IL were inspected. This probe did not execute native firing/rearm/damage or a complete motion sweep for these arbitrary mod missiles.
- Mass ceiling: 111 helper/data checks and 83 checks executing native WeaponChecker.MountAllowedHardpoint and SavedLoadout.CreateLoadout on private inactive A/C templates. The copied weapon/aircraft addon fixture yielded 28 accepted aliases / 20 distinct choices; this count depends on installed mods. Exactly 240 kg is accepted; overweight, negative and nonfinite data are rejected. Stale mount.mass is ignored in favor of emptyMass plus massPerRound. Saved overweight rails clear; exact-boundary rails remain; late mass edits are rechecked. Inner payload lists, registered weapon IDs and B's heavy inner payload remain unchanged. The private fixture was restored.
- MFD: 519 assertions in Unity using reconstructed native screen, font, sliced-border and material settings; aircraft/weapon/Harmony data types are stand-ins. A/C/T/NA row ordering, state colors, bright selected-group markers, dim attached unselected markers and hidden detached markers were checked. Runtime references reuse installed game assets; no extracted sprite/font or replacement raster is shipped. This evidence does not establish a fresh complete running-game cockpit test of the final build.

The 240 kg figure is an approved station specification, not a measured structural failure threshold. Mod compatibility does not transfer weapon mods between peers or certify every provider's geometry, firing or network registration. No new full real-flight FPS benchmark or two-PC Steam acceptance was performed for this release.

## Retained historical coverage

The public 0.7.2 baseline retains its FCS, six-underwing-station C/T and A/C outlet cleanup. Its historical evidence includes 2,811 reference checks, 24 native free-flight cases and six NA roll reversals; ten native C/T fire/release/ownership cases; 206 C and 774 T movable-surface poses; and five A/C/B/T/NA native cleanup spawns. These are not new 0.7.3 reruns or arbitrary-mod-missile clearance results. Earlier 0.7.1 paint/network evidence remains historical (3,556 rendered checks, 183 contracts and 31 one-PC native network checks).

All 13 external paint bundles remain byte-identical. Multiplayer remains experimental and requires matching installations. No new FCS tuning, vapor redesign, FQ-20A or FS-20A VS integration is claimed.
