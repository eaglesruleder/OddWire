# Vintage Story Mod Planning Ledger

## Current Objective
- Use Vintage Story modding as the practical path toward building the kind of game you have always wanted to make.
- Lean into the game's voxel survival sandbox and progressively reshape it toward a more TRPG-like RPG structure.
- Start from modding and datapack-style changes where possible, rather than getting trapped rebuilding fundamentals from scratch.

## Notes
- `✓` indicates the item has already been completed.
- This document is a cleaned planning ledger, not a full programmer handoff.
- Some entries are broad enough to remain `Epic`s for later breakdown.

## Installed Mods / Existing Feature Baseline
- Shaders
- Darkvision
  - Adapt eyesight to light/dark vision per reality
- Cartography Mod
- Temporal Mod: Forecasting equipment
- Farming Mod: Soil depth rework
- Farming Mod: Bees and pollination
- Smithing Mod: Bloomery needs firebrick tiers

---

## Common Code & QoL Mod

### Story — Non-blocking flat items
- Allow certain flat items, such as pelts, to lie flat on the ground without losing normal surface interaction.
- Current issue: once placed flat, they can no longer be used as a surface for placing items such as bowls.
- Goal: certain “flat” items should still behave like a regular surface for interaction / placement.

### Story — Weather direction check
- The game already has wind.
- Investigate a basic check for whether a block is exposed in the wind direction.

### Epic — Room-specific buffs
- Investigate the existing room checks.
- Create a system that looks for room variants.
- Inspiration: Oxygen Not Included-style rooms with size and furniture requirements.

### Story — Handbook rework
- Rework the in-game handbook / wiki.
- Improve at least the Items tab.
- Add a `craftable` filter so world-only items can be filtered out.

### Story — Pinned notes
- Offhand nails + parchment on a wood block pins parchment to the block.
- With offhand pigment, draw the selected toolbar item onto the parchment.

### Story — Sleeping restores sanity
- Sanity is currently restored only in real time.
- Sleeping should also restore the sanity-equivalent amount that would have been gained during that elapsed duration.

### Story — Rope climb
- Rope can be placed on top of a block.
- It should hang off the side facing away from the player.
- Additional rope can be placed extending downward from that side.
- Rope segments should be climbable.

### Story — Refill stack on stack emptied
- Some items should correctly replace themselves when emptied, similar to how tools break into another state.
- Examples:
  - Pie slice portion replaces with stack of slices
  - Firewood

### Story — Backpack on ground acts as chest
- A dropped / placed backpack should function as a chest-like container.

### Story — Torch warmth rework
- Holding a torch should add roughly `+1 to +2 °C` of warmth.

### Story — Torches light torches
- A torch in a torch holder or in the offhand can ignite a torch in the main hand with `Shift + Right Click`.

### Epic — Block renderer mechanic
- Investigate a tile-block mechanic that renders a block shape using a tilemap-style border system.
- Example use case: braziers that can visually grow larger.

---

## Timberworks Mod

### Story — Layer of sticks stacking
- Layers of sticks can be stacked like other on-floor piles.
- A single layer of sticks should be as high as a path.
- A layer of sticks can be used as Pit Kiln material.
- ✓ A layer of sticks can also be crafted back into `9 sticks`.

### Epic — Lumberjack minigame
- Add a tree-cutting minigame.
- The axe uses a chisel-like mechanic to cut through the log.
- On hit:
  - find nearest voxels, with some randomness by Manhattan distance from the aimed pixel
  - destroy a number of pixels determined by the current block-break-time mechanic
- Desired effect:
  - creates a wedge
  - number of cuts remains proportional to tree difficulty
  - player skill can improve efficiency by choosing better cut positions

### Epic — Forestry growth stages
- Current issue: planted tree seeds sprout immediately into a full tree.
- Add an intermediate `young tree` stage before the final form.
- First stage:
  - thin tree, similar to fruit trees
  - drops firewood instead of logs when cut
- Consider a `Tree Trunk Root` object:
  - occasionally searches up the trunk to determine tree size
  - attempts growth when not yet at max size
- Growth should be influenced by dirt quality to some block depth.

### Story — Spigot item
- Add an item used to tap trees for resin and syrup.

### Story — Firepit from sticks
- Update Firepit so it can also be made from sticks.

### Story — Knife bark chiseling
- A knife can chisel the outermost layer of logs.

---

## Mining Mod

### Story — Cracked stone while mining
- Add a chance for stone to turn into cracked stone while mining.

### Story — Prospecting pick works on boulders
- Prospecting pick should work on boulders.

### Story — Boulders can be picked up
- Boulders can be picked up.
- Follow-up thought: boulders may craft into ashen blocks.

### Story — Per-stone-type paths
- Add `{Stone Type} Path` variants.
- Current issue: only one generic `Stone Path` exists with move-speed buffs.
- Desired outcome: one path type per stone type, e.g. granite / andesite.

---

## Masonry Mod

### Story — Ingot recovery recipe
- Recipe: `Anvil top/bottom + Chisel -> Ingots`.

### Story — Sculpt cloning
- Figure out a recipe that allows cloning a sculpted item.

### Story — Chisel SHIFT fill rework
- `Shift + Right Click` should fill the voxel being aimed at, rather than the adjacent voxel.

### Story — Chisel accepts new material by material type
- Chisel should accept a new chiseled material if it matches one material type.

### Story — Rooms and chiseled blocks rework
- Rework how rooms interact with chiseled blocks.

### Story — Full chiseled returns original block
- Rework so a fully chiseled block returns the original block appropriately.

---

## Smithing Mod

### Epic — Brazing and forge-welding
- `Forge-welding`
  - heat iron rods
  - add borax
  - hammer them together, similar to an anvil interaction
- `Brazing`
  - assemble cold using flux and metal bits at joints
  - heat over furnace so the filler melts into the cracks

### Epic — Brazier
- ✓ Copy Firepit code
- ✓ Separate firepit scripts and renderers
- ✓ Make fuel behave like a put-on-ground variant
- ✓ Stack positions set by `Brazier.json` transform object
- `FuelRenderer` needs to be a true renderer

#### Stories / requirements already identified
- Burning more fuel creates more light
- Burning fuel creates ash
- Brazier temperature / damage should scale from furnace temperature

#### Fuel attribute model
- `BurnTime => Sum(fuelStacks.burnDuration)`
- `BurnTemp => Max(fuelStacks.burnTemperature)`
- `BurnMass => brazier.thermalTier * (1 + (Sqrt(fuelStacks.qty) - 1) * c)`
- `HeatIn => brazier.thermalBonus * (1 + Avg(fuelStacks.burnIntensity))`
- `HeatLoad = BaseLoss + AmbientLoss + WindLoss + WetLoss`
- `HeatRate => (BurnTemp - Temp) * (HeatIn / BurnMass) - (HeatLoad / BurnMass)`
- `Temperature => clamp(Temp + HeatRate * (Time.time - timeLastTick), AmbientTemp, BurnTemp)`

### Bug — Crucible part and product smelting
- Combining metals, e.g. Copper + Tin, should allow Tin Bronze nuggets.
- You should be able to smelt chunks at increased melt duration for reduced reward.

---

## Farming Mod

### Story — Barrel recipes accept non-exact quantities
- Barrel recipes should not require exact quantities.
- Example:
  - `1 lime + 1 water = 1 limewater`
  - `2 lime + 1 water` should produce `1 lime + 1 limewater`

### Epic — Barrel compost rework
- ✓ Rot is a fluid placed in a barrel.
- The barrel item slot modifies behaviour based on added material:
  - ✓ `Dry Grass`: slower processing, largest yield
  - ✓ `Food`: fast processing, larger yield, susceptible to rain
  - `Manure`: fastest processing, larger yield, susceptible to high temperatures
  - ✓ `None`: slow processing, low yield

### Epic — Compost pile
- Creation:
  - place a dirt block with air above and on all sides
  - place rot or crop in a pile on top to create a compost pile
- Interaction:
  - using a shovel on the compost pile spreads compost item to side blocks
  - reduces compost pile status
- Core drivers:
  - `Nutrients`: rot low, crops medium, manure high
  - `Wetness`: rain and watering vs temperature evaporation
  - `Temperature`: outside temperature, nutrient level, greenhouse

### Story — Sour compost
- ✓ Sour Compost item exists
- ✓ Overheated or overwatered compost can produce Sour Compost
- ✓ Sour Compost can be recomposted into Compost
- ✓ Temporary use value: `60% NPK of Compost`
- If used on crops:
  - short-term effect: large `N` + some `PK`
  - long-term effect: deplete `NPK`, increase temperature

### Story — Barren soil
- Add `Barren Soil`
- Maximum fertility is `0% NPK`.

### Story — Hoe / tilled soil
- Required by greens and legumes.
- Uses current mechanic.
- Moisture max is determined by the fertility block below.

### Story — Plow / plowed soil
- Required by cereals and root crops.
- Plowing behaviour:
  - roughly `-1 fertility++` and `-2 fertility--`
  - if fertilised and below fertility-block threshold, roughly `-1`, and on some chance fertility increases

### Story — Crop fertility behaviour
- `Roots`: `% chance per day` increases fertility
- `Legumes`: NPKs except their own recover
- `Fallow`: all NPK recover quickly, boosted by rain

### Epic — Animal AI tools
- Use Elk mount logic and build AI tools from that basis.

### Story — Manure item
- Animals drop manure.

### Story — Digging farmland drops dirt
- Digging farmland should drop dirt.

### Investigate — Weather forecasting equipment
- Investigate farming-related weather forecasting equipment.

---

## Hunters Mod

### Epic — Early husbandry
- Current issue: husbandry is effectively locked until the copper age because planks are needed for a trough.
- Goal: create an earlier husbandry system.
- Design motivation: herding animals predates the copper age and current progression feels wrong.

### Story — Hunting tracks
- Rather than a direct blood trail, prefer a tracking system.
- Existing inspiration: cave-art style block replacement when using chalk on rock.
- Desired mechanic:
  - when a creature moving at velocity collides with certain block types, e.g. dirt or leaves
  - there is a chance to create a temporary `block with tracks (Creature {size})`
  - the track disappears after some time

### Story — Trodden grass and trail
- Players or mobs moving on grass can create `Trodden` grass.
- Chance increases with grass length.
- Trodden grass then has a chance to disappear and turn underlying dirt into `Trail` dirt.

### Epic — Animal spawns / ecology heatmaps
- Create heatmaps of berries and vegetables.
- These feed heatmaps for animals such as chickens.
- Those in turn feed heatmaps for predators such as foxes.
- Continue this up the ecological chain.
- Create migration paths for animals.
- Seasonal influence may affect migration paths.
- Migration / presence may present as tracks even when the animal is not currently present.
- Older track information may become class- or skill-dependent.

### Story — Pelt armour
- Leather is hard to attain early.
- Pelt currently only turns into wood lamellar armour.
- Add an alternative pelt armour path.

### Epic — Leather rework
- Current issue: leather is tech-locked behind copper because planks are required for barrels, and barrels are required for tannin.
- Investigate earlier alternatives.
- Notes / inspirations:
  - earth pits were used historically
  - pits could be lined with clay, ash, slabs, or hide
  - wood ash could substitute for limestone
  - clay can act as an effective water container

### Story — Eggs can be thrown
- Rework so eggs can be thrown.

### Story — Crock pots store stack food
- Crock pots can hold one stack of food like a storage container.

---

## Cooking Mod

### Epic — Nutrient system
- Food categories should grant more than one nutrient.
- Each nutrient also contributes a buff when full, plus health interactions.

#### Nutrients and buffs
- `Carbohydrates => Move speed`
- `Proteins => Action speed`
- `Fats => Hunger rate`
- `Water => Exp rate?`
- `Fibre => Increases other bonuses`
- `Vitamins => Immunity`
- `Minerals => Health`

#### Food category nutrient mixes
- `Fruit`
  - Move Speed ↑, Immunity ↑, Exp Rate ↑
  - Carbohydrates `50%`
  - Vitamins `25%`
  - Water `15%`
  - Fibre `10%`
- `Vegetables`
  - Immunity ↑, Health ↑, All Buffs Slight ↑
  - Vitamins `35%`
  - Minerals `25%`
  - Fibre `20%`
  - Water `15%`
  - Proteins `5%`
- `Protein`
  - Action Speed ↑, Health ↑, Hunger Rate ↓
  - Proteins `60%`
  - Fats `25%`
  - Minerals `10%`
  - Water `5%`
- `Grains`
  - Move Speed ↑, Buff Synergy ↑
  - Carbohydrates `55%`
  - Fibre `20%`
  - Proteins `15%`
  - Minerals `10%`
- `Dairy`
  - Hunger Rate ↓, Health ↑, Action Speed ↑
  - Fats `35%`
  - Proteins `30%`
  - Minerals `25%`
  - Water `10%`

### Story — Sandwiches
- Craft using knife, bread, and edible ingredients in the crafting grid.
- Pairs well with foraging in the wild.

### Story — Dough-making mechanic
- Current system is flour + water directly in crafting grid.
- Desired interaction:
  - pour flour on table
  - pour water
  - knead with empty hands

### Story — Flatbread item
- Made from flour and water.

### Story — Dough item
- Made from flour, water, and egg.

### Story — Flour and grain as liquid bulk goods
- Rework flour and grain so they behave like a liquid / loose bulk material moved by buckets, jugs, or barrels.

---

## Mechanical Power Mod

### Story — Medium gear
- Crafted from an angled gear and a wooden axle.
- Behaves like a large gear but only `1` square wide.
- Does not change torque:speed ratio.

### Story — Windmill check shape
- Rework windmills so they check a circle rather than a square.

### Story — Axle slab
- Add an `Axle Slab`.
- Placed like a slab.
- Counts as solid like a slab for room-size logic.
- Contains an axle through it.

---

## Combat Mod

### Story — Clothing slots appearance toggle
- Toggle clothing slots on / off for appearance.

### Story — Armour effects by body-part hit chance
- Scale armour effects to the percentage chance of the body part being hit.

---

## Temporal Mod

### Epic — Temporal storm carrier
- Make the giant giraffe-like storm entity an actual position in space.
- The storm spot always appears `512` away from the player.
- In world-space, it is actually moving past the player.
- That moving space-object is the storm carrier.

### Epic — AI spawn
- Use an `R/G/B` map from `AI / Terrain / Skylight`.
- Run a queue of red-dot transformers as a passive agent update system.
- Red dots collapse near the player into real entities.
- They later despawn back into probability agents.
- See dedicated AI Spawn document for the fuller concept.

---

## Inventory Mod

### Epic — Inventory space overhaul
- Current issue: multiple backpacks each contribute part of final slot capacity.
- Preferred model: fewer items in dedicated slots such as back / armour with more pronounced effects.
- Example:
  - backpacks may allow sacks to attach to them
  - but the player should not effectively carry four backpacks at once

### Story — Shulker-basket rework
- While in the toolbar, baskets act like an inventory that does not itself contribute inventory slots.
- Similar to a shulker box.
- Should also be usable to collect goods and deposit directly into itself.

### Story — Portable container access
- When a portable container is placed, `Shift` / `Ctrl + Right Click` opens it as a container.

### Story — Slot-first transfer
- Shift-clicking a stack should prioritise the highlighted toolbar slot.

### Story — Sheath item
- Adds an extra slot that allows a sword to be stowed.

### Epic — Toolbelt rework
- Tools in the toolbelt are always accessible for their purpose without manual selection.
- Example: while mining stone, the pickaxe can auto-select / auto-apply.
- Optional extension:
  - placing stones from the toolbelt can directly place cobblestone
  - pressing `F` may let the player choose the shape

### Story — Tool-in-hand recipe support
- If a recipe only needs tools and one material, e.g. hammer + axe to debark logs, it should also work when those tools are held rather than only when slotted in a crafting interface.

### Story — Quiver item
- Provides arrow / spear storage and increased draw speed.
- Also allows arrows to be placed on the ground.

### Story — Arrow stack placement tweak
- Need a way for arrows to be placed on the ground.
- Alternative thought: allow arrow storage in a barrel.

### Epic — Self-resource management overhaul
- Current Vintage Story stats: Health and Hunger.
- Desired redesign:
  - Sleep restores health, and a rest stat increases max stamina
  - Food restores stamina, and food-group stats increase max health
- Motivation: replace the current food-diversity-to-max-health system with a preferred structure.

---

## RPG Mod

### Epic — Class attribute overhaul
- Current classes come with predefined pros and cons.
- Desired direction: class-builder style selection from a set of pros and cons.

### Epic — World-building and later NPC / story systems
- Start with scene design.
- Look for existing world-editing / map-management mods rather than building those first.
- Search wiki / mod DB for:
  - WorldEdit-style tools
  - region / chunk management mods
  - deletion / copy / paste support
  - preferably cross-world support as well
- Later research:
  - what it takes to build NPCs
  - APIs for AI behaviour
  - likely use of mod libraries plus own code

---

## Original Prioritisation Intent Preserved
- Order objectives by the mod / category structure above.
- Summarise action items with `1–5 star` effort and reward values.
- For high effort:reward items, keep summaries concise but with enough room to preserve details.
- For low effort:reward items, keep summaries shorter but do not omit details.
- Float quick wins toward the top.

## Structural Review Notes
- The ledger contains a mix of `quick wins`, `broad epics`, `active systems`, and `already-completed groundwork`.
- `Brazier`, `Compost pile`, `Animal spawns`, `Inventory overhaul`, `Nutrient system`, and `Class overhaul` are honest Epics and should stay that way until selected for deeper breakdown.
- Some ideas overlap and likely want shared supporting systems later:
  - weather / exposure logic
  - surface / placement interaction rules
  - room detection / classification
  - AI / ecology heatmaps
  - inventory container behaviour
- A later pass should split this ledger into:
  1. quick implementation wins
  2. mature epics ready for story breakdown
  3. speculative / research-first concepts
