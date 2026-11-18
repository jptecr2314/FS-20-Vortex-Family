# FS-20 Vortex Family 0.7.1

Optional A/T/NA paints load asynchronously from `BuiltinPaints/` beside the DLL. Install the complete BepInEx folder; replacing only the DLL is insufficient. Three default prefab coats and their neutral basis stay resident. Optional coats and distant vertex-color meshes are shared and retained while native handles or renderers use them, then evicted without global garbage collection. Custom `.fs20paint` packs keep their existing format and location.

Five FS-20 variants in one family, one selector and one plugin: A, the native B,

C, integrated-delta VTOL T, and FS-20NA Night Attack. Open the FS-20B tile to

choose a variant. Requires Nuclear Option 0.34.6, BepInEx 5 and Blueprinter 2.1.1,

installed separately. This release is an experimental multiplayer prerelease.

## Aircraft and inventory

| Variant | Configuration | Base price (game currency) |

|---|---|---:|

| A | Conventional aircraft, original wing outline and extra internal fuel | $75m |

| B | Original native VTOL aircraft | $90m |

| C | Conventional aircraft with wider wings and an arresting hook | $105m |

| T | Integrated-delta VTOL with articulated slats and separate wing damage sections | $125m |

| NA | Night Attack variant with four built-in liveries and dedicated sensors | $150m |

Faction supply sharing is optional: F1 > FS-20 Vortex Family > Inventory >

Shared airframe stock, with BepInEx Configuration Manager installed. Purchased

and reserved aircraft keep their exact variant for ownership, flight, sale and

return in both modes. Buying A does not grant ownership or resale value of NA.

Reserved airframes cannot be sold. Existing inventories are not rewritten.

The A/C/NA takeoff advisor uses current fuel, payload, CG/inertia and headwind

relative to the runway or deck. It provides a planning estimate; it does not

command rotation or guarantee a launch. Optional authored C/NA Penumbra catapult

rules are under packaging/fs20-penumbra, with their own instructions; the

third-party catapult loader is not included.

## Installation and version-number correction

Close the game, then update the existing FS-20 Vortex Family entry in NOMM or

extract the package's BepInEx folder into the game. Keep exactly one

VortexConventional.dll and keep backups outside BepInEx/plugins. Preserve

configuration files. The canonical plugin/catalog ID remains fs20vortexfamily.

The old standalone Shared Airframes plugin is superseded by this family.

The historical 6.0.7 number was a numbering mistake. The supported release line

resumes at 0.6.9, using tags fs20-v0.6.9, fs20-v0.6.10 and later fs20-v1.0.0.

The historical 6.0.7 release and its download links remain available. If you

already installed 6.0.7 or a 6.x development build, explicitly select/reinstall

0.6.9 once; a numeric update check will not offer this downgrade. Later releases

advance normally within the corrected line. Catalog PR approval is separate

from this GitHub release being available.

The optional Install.ps1 performs a checked installation with backups; it does

not run automatically. The separate [NO Manual Tailhook](https://github.com/jptecr2314/NO-Manual-Tailhook)

plugin is optional and is not bundled.

## Experimental matching-install multiplayer

Use the identical package and game/mod versions on the host and every client.

Matching modded sessions are enabled by default, with a one-time migration

from the old disabled default. Later explicit opt-outs are respected. Unmodded servers and mixed family builds are

unsupported. Steam/password/game-build authentication remains native; the host

controls shared supply and C flap policy. Simulation-owner guards cover flight

assistance, the NA yaw allocator and arresting forces. Bounded remote visual

updates cover slats, hooks and measured C/T vapor loads. Native replication

still owns aircraft position, controls, weapons and damage.

Native Steam authentication, handshake, remote A/C/T/NA spawns, exact remote paint references and T detachment were tested with a hidden anonymous server and legitimate client in separate processes on one PC. This is not two-PC Internet/latency or human combat acceptance. Late joins, every weapon/mission combination and mixed mod environments remain outside that result. See QA.md.

## Source and build

```powershell

./build.ps1 -GamePath 'PATH_TO_NUCLEAR_OPTION'

```

The ordered build-spec.json lists every compiled source and embedded resource.

The compiler restorer verifies the pinned compiler package hash. Game/Unity

libraries and compiler binaries are not distributed. Tests under tests/ retain

their documented fixture scope; historical lab drivers may require adaptation

to local paths and are not part of the build command.

Author: Jpgamer4045d. Code license: MIT. Substantial AI assistance is declared

in AI_ASSISTANCE.md. Game-derived resources retain their original ownership;

see THIRD_PARTY_NOTICES.md. Not affiliated with Shockfront Studios.

## Compact own-livery set

A/T/NA each provide two BDF and two PALA paints at 2K output resolution.

The 0.7.1 set uses about 112 MiB compressed mip payload, excluding driver overhead

and custom packs. This is dimension arithmetic, not a measured VRAM peak.



## Paint creator kit

Download `FS-20-Paint-Creator-Kit-0.7.1.zip` from the same release for editable A/T/NA templates, new-wing UV references and the licensed Unity 2022.3.62f2 exporter. The kit is optional and does not enlarge the player package. Custom packs may retain 4K authoring/output where needed; set averageColor in the manifest for the native distant vertex-color finish.
