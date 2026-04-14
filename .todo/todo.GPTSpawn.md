# Epic Snapshot — Latent AI Spawn Field

## Core Objective
- Create a lightweight, continuous AI simulation where latent enemies drift through a 3D field toward the surface over time.
- Only materialize real entities near the player.
- Preserve the feeling that the world contains hidden life without paying the cost of fully simulating large numbers of active mobs.

## High-Level Model
- World regions are represented as streamed 3D fields.
- Latent enemies exist primarily as queued “red dots” moving through those fields.
- A real spawn is only produced when local conditions near a player permit collapse from latent state to active entity.
- Far or obsolete active entities can be reabsorbed back into the latent system.

## 1. Data Representation

### Requirement — Regional tensor
- Represent each world region, e.g. `64^3`, with an RGB tensor:
  - `R`: enemy density / latent activity
  - `G`: terrain density + movement cost
  - `B`: skylight exposure / surface distance

### Requirements
- Maintain and stream these maps per chunk or chunk-ring around players.
- Update `G` / `B` lazily when terrain or light changes.
- Store `R` as a dynamic buffer updated each tick by queue processing.

## 2. Latent Agent Engine

### Requirement — Red-dot queue
- Maintain a queue of latent enemy positions.

### Tick behaviour
- Each tick, process a fixed number `N` of dots.
- For each processed dot:
  - sample local `G` / `B`
  - determine preferred movement, e.g. down gradient toward the surface
  - move one step
  - re-enqueue with decay or replacement

### Collapse / recycle rules
- If a dot is near a player and hidden from view, collapse it into a real enemy spawn.
- If it is too far away or obsolete, recycle it to seed new latent dots underground.
- Include hysteresis:
  - once collapsed, an entity should persist for a while before despawning

## 3. Field Generation

### Requirement — Periodic global desirability update
- Every `1–2 s`, run a 3D transformer or UNet to update global spawn desirability.
- Input: current RGB map.
- Output: next `R` field representing spawn pressure.

### Intended effect
- The model encodes long-range coherence and global flow.
- Example: cave systems can feed surface hot spots.
- Between model passes, the queue handles local micro-movement using the current field.

## 4. Performance Control

### Requirements
- Throttle updates, e.g. `100–1000` queue ops per tick.
- Keep field data in `FP16` or `8-bit` to reduce memory, roughly `1–3 MB` per chunk.
- Run inference in a background thread or GPU stream.
- Stream only nearby chunk tensors.
- Evict distant ones.

## 5. Spawn Mechanics

### Spawn conditions
- Spawn only when:
  - player is within a defined radius
  - line-of-sight and light thresholds are satisfied
  - local spawn budget is not exceeded

### Spawn outputs
- Randomize enemy type by difficulty derived from `R` intensity.

### Despawn / reabsorption
- Far or idle entities should despawn by being reabsorbed into the latent field, i.e. back into `R`.

## 6. Integration

### Requirements
- Cache and reuse RGB tensors per chunk.
- Synchronize with:
  - lighting
  - pathfinding
  - player movement systems
- Save / load latent `R` field and queue seeds to preserve world continuity.

## 7. Optional Enhancements
- Add memory layers such as:
  - noise
  - fear
  - pheromones
- Use Dijkstra or flow maps for:
  - path prediction
  - migration events
- Allow environmental factors such as storms, seasons, and biomes to influence `R` updates.
- Add simple visualization such as a latent-activity heatmap overlay for debugging.

## Structural Review Notes
- This is already much closer to a programmer-facing subsystem brief than a todo file.
- The core loop is clear:
  - field exists
  - queue moves latent agents
  - local conditions collapse some into real entities
  - distant entities are reabsorbed
- The biggest hidden risk is not the queue system; it is the `3D transformer or UNet` requirement.
- That model pass is probably the first thing to challenge if you ever want a minimal first implementation.

## Recommended Minimal First Version
If this later gets split into phases, the sensible v1 is:
1. static `G` / `B` fields
2. queue-driven `R` updates without ML inference
3. collapse near player under LOS / light / budget rules
4. reabsorb on despawn
5. debugging overlay

Then add learned / global-coherence updates later if the simpler version proves the fantasy.

That does not remove any existing idea here; it just identifies the likely first executable slice.
