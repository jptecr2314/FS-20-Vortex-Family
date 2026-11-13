# FS-20 Vortex Family 6.0.7

- Include the FS-20T as a standard member of the A/B/C/T family in one DLL.
- Retain the four-variant selector and configurable shared or separate stock.
- Retire the old T experimental-enable gate without changing aircraft keys.
- Include the latest C/T volumetric vapor, rear-panel/elevon coverage and
  corrected density-advection direction from the 0.8.11 development build.
- Replace the T's repeated pitch cut/rebuild near the flight envelope with a
  continuous native-FBW rate reference. Retain immediate stick release and
  hard protection for actual G/AoA exceedance; VTOL and manual input bypass it.
- Retain the reviewed geometry, other flight paths, damage states, detachment
  behavior and single-player boundary.
- Use version 6.0.7 consistently in plugin and package metadata.
- Preserve the canonical `fs20vortexfamily` ID and legacy configuration migration.

This is a complete family package. Do not install separate A/C/T aircraft
DLLs alongside it. Publication and catalog acceptance are separate from
local package preparation; see the release receipt for current delivery status.
