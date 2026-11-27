# Multiplayer regression tests

`TestAssemblyFingerprint.cs` compiles alongside the production
`src/Multiplayer/Fs20AssemblyFingerprint.cs`. Supply two different managed DLLs
with the same assembly identity, a DLL with a different identity and a non-DLL
text file as its four arguments. It reproduces `OpenRead(Assembly.Location)`
failing after an actual `Assembly.Load(byte[])`, verifies the loader-path
fallback, full-file hashing, path independence and actionable rejections.

`TestCompatibilityPolicy.cs` compiles alongside `Fs20NetworkPolicy.cs` and
executes matching/mismatched TCP loopback exchanges, malformed packets,
native-authentication gating, acknowledgement/nonces and host-policy checks.
The native authentication result is a fixture input, not a Steam login.

`TestSurfacePolicy.cs` compiles alongside `Fs20SurfacePolicy.cs` and checks
bounded visual-packet serialization, invalid values and sequence ordering.

`NativeBench.cs` is the existing Unity editor/native Mirage fixture. Its
in-memory wire and explicit native-authentication fixture values are not a
two-installation Steam flight. Reusing that harness requires installing the
locally owned game references and the complete matching paint payload.

All these tests are local developer checks; they neither install the plugin
nor launch the user's game.

`NativeSteamBench.cs` and `MemoryFixturePlugin.cs` are separate private diagnostic plugins, excluded from build-spec.json. The native harness requires an isolated game copy, hidden server/client roles, output directory and memory-fixture path supplied through FS20_NET_* environment variables. It uses real Steam authentication and native weapon/damage APIs. Never include these diagnostic DLLs in a player package.
