# Epic Snapshot — Tectonic World Generation Mode

## Core Objective
- Improve terrain, stone composition, and ore distribution by using lightweight tectonic-style worldgen maps.
- Aim for worldgen that feels more geologically structured than vanilla terrain without simulating full real tectonics.
- This is meant to be a map generation mode, not a full persistent tectonic simulation.

## Design Constraints
- Must be computable directly from `XY` position.
- May use additional scalar maps such as noise, height, or depth.
- Must **not** require:
  - plate identification
  - second-pass province solving
  - heavy worldgen procedures that depend on reconstructing plate relationships
- Consistency shortcomings are acceptable if the result still produces good large-scale terrain features.
- Main purpose is believable terrain / rock / ore patterning, not scientific tectonic correctness.

## Core Maps

### Requirement — Tectonic map `T(x, y)`
- Scale: roughly `10e3` to `10e4` blocks.
- Skew values toward `0` to represent plate interiors.
- This creates whitespace trails between plates, on the order of roughly `100` to `10e3` blocks.
- Those trails define collision / boundary provinces.

### Requirement — Buoyancy map `B(x, y)`
- Scale: roughly `10e4` to `10e5`.
- Creates very large surface-level clumps that behave like continents.
- Surround those with very large ocean regions.
- Also helps define how collision provinces should feel.

### Requirement — Vector-style map `V(x, y)`
- Scale: heavily smoothed over roughly `10e3` to `10e4` blocks.
- This is not meant to represent true vector motion.
- It instead represents `convergent` vs `divergent` tendency.
- Values near `-1` / `+1` indicate highly active or strongly pronounced province features.

## Derived Province Signals

### Boundary province type
- `BoundaryMap * V(x, y)` gives the boundary province type.
- This is the main signal for convergent / divergent boundary behaviour.

### Plate interior type
- `(1 - BoundaryMap) * V(x, y)` gives different plate interior tendencies.

## Known Simplifications / Acceptable Dumbness
- The model may create logical fallacies, such as divergent provinces on opposing sides of one plate.
- However, because `V(x, y)` is smoothed over very large distances, convergent and divergent extremes should not sit immediately adjacent.
- Neutral transition areas should naturally exist between them.
- Micro-plates or other unrealistic structures may still appear.
- These shortcomings are acceptable if the output still creates useful large-scale terrain patterns.

## Intended Worldgen Payoff
- Stable plate interiors can create large plains.
- Convergent boundary regions can create long mountain chains.
- The tectonic signal can influence:
  - terrain features
  - mountain / valley placement
  - stone-type distribution such as andesite vs slate
  - ore distribution by tectonic landmark type
  - biome flavouring where useful

## Terrain / Feature Use Cases
- Use the derived maps to define biomes and feature code programmatically.
- Real tectonic correctness is less important than strong landmark identity.

### Example — Collision provinces
- Where `T(x, y)` indicates a collision province and `B(x, y)` is low:
  - create a gorge along the highest `T()` values
- Over a wider surrounding range of `T()` values:
  - use `T() * Perlin` to distribute volcanoes in the surrounding region
- This avoids needing to know which side is actually subducting.

## What This System Is Actually Missing
These are not objections, just missing variables worth acknowledging when interpreting province types:

### Missing variable — Compression / shear distinction
- Current design distinguishes mostly convergent vs divergent tendency.
- It does not explicitly model transform / shear boundaries.
- That means some mountain / valley / strike-slip behaviours may overlap unnaturally.

### Missing variable — Boundary asymmetry
- The system does not know which side is subducting.
- That limits one-sided features such as trench + volcanic arc asymmetry.
- Current workaround is to make symmetric landmark provinces rather than directional ones.

### Missing variable — Interior age / crust maturity
- Plate interiors are not differentiated by age.
- That means stable cratons, younger oceanic crust, and recycled crust are not cleanly separated.
- This may matter later if rock and ore generation need stronger geological identity.

### Missing variable — Stress accumulation vs activity type
- `V(x, y)` captures tendency but not separate notions of activity strength, compression history, or uplift potential.
- A later scalar map could help distinguish:
  - quiet convergent regions
  - violent convergent regions
  - failed rifts
  - exhausted provinces

## Recommended Clean Interpretation
Treat the system less as “tectonic simulation” and more as:
- `province classification noise`
- with geology-inspired semantics
- used to drive terrain, rock, and ore placement consistently at very large scales

That framing fits the constraints better and avoids overpromising realism.

## Suggested Future Extension Points
These remain compatible with the `XY-only, no plate-ID` constraint:
- add an `activity magnitude` map separate from signed `V`
- add an `interior age / maturity` map
- add a `fracture / roughness` map for mountain-chain breakup and ore corridor density
- add a `depth / crust-thickness proxy` map where useful

## Structural Review Notes
- This file is already closer to an `Epic snapshot` than a todo list.
- The core idea is coherent.
- The main thing it needed was explicit separation between:
  - objective
  - constraints
  - core maps
  - derived signals
  - intended outputs
  - known simplifications
- The strongest hidden assumption is that good province classification is more important than tectonic directional correctness. That assumption is reasonable for your stated goal.
