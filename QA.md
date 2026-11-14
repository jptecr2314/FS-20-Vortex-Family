# 0.6.9 validation scope

The delivery receipt identifies the actual final DLL and tests run against it.
Historical checks are retained under their original hashes and are not relabeled
as execution on this build.

Local checks cover the production dynamic takeoff calculator, native in-memory
Mirage authentication/compatibility exchange, simulation-owner guards, remote
mechanism/damage presentation, definition/part layout and compiled integration
scope. NA source/geometry provenance is compared with the corrected 6.3.2 work.

The dynamic model assumes ready rated thrust, a level runway, intact symmetric
aircraft, closed bays, fuel 20–100%, mass up to 16,000 kg, density 0.95–1.25 kg/m³
and headwind 0–20 m/s. It uses 20% roll reserve plus 5 m. It does not reproduce
the native tire/strut solver, ground effect, engine spool/lapse, ship heave,
deck-edge settling, water contact or complete native FBW/takeoff state.

Still pending: a real private Steam session across two matching installations,
complete spawn/stock/weapons/damage/detachment/late-join/latency trials, native
mission invocation of the takeoff snapshot reader, and live visual verification
of NA materials/cannon-door firing. Local results do not certify these flows.

The tests below were run during local preparation against the same DLL now
packaged for publication. Publishing does not install or launch the game.
GitHub availability and NOMNOM catalog approval are separate states.


## Per-variant economy revision

Base airframes: A $75m, B $90m, C $105m, T $125m, NA $150m.
Faction supply sharing remains optional. Purchased and reserved aircraft retain
their exact variant for ownership, flight, sale and return. A purchase of A
does not grant ownership of NA. Native server commands use the definition price;
reserved aircraft remain ineligible for sale. No save inventory is rewritten.


## Final binary and reproducibility

The final DLL SHA-256 is 8ecf56ef0c2a968fe814b3471664cb9e2aec2d14e591fef268fb5efa9a42488a.
Local preparation passed 596 native server economy checks, including 50
purchase/sale pairs in both stock modes and 40 rejected cross-variant pairs;
615 pure ownership-ledger checks and 116 native in-memory multiplayer checks.
The prior ownership implementation reproduced a 75-million profit when the new
prices were injected. The corrected native command fixture rejects that path.
Fourteen embedded resources and unrelated flight/damage paths were preserved.

The source export must rebuild to this exact DLL before release publication.
Historical NA-factory and takeoff-calculator evidence belongs to its original
hashes; those complete suites were not rerun on the pricing revision. The native
fixtures use controlled local objects and in-memory identities/wires, not a
remote Steam client or actual network damage/weapon RPC acceptance.

Machine-readable final-scope evidence is in docs/qa/0.6.9/validation.json;
historical raw preparation fields are not claims of current publication state.

The previous 6.0.7 release's detailed historical results remain available in
[its tagged QA document](https://github.com/jptecr2314/FS-20-Vortex-Family/blob/v6.0.7/QA.md).
