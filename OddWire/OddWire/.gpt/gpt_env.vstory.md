This gpt_env..md file describes a working environment including its platforms, structures, defaults, and common gotchas so other gpt..md prompts can reason inside the right local context.

# Vintage Story Modding Environment

## Purpose
Define the local operating environment for Vintage Story modding work.

Focus:
- mod root structure
- asset/domain structure
- class/code structure
- asset-to-code linkage
- client/server/runtime ownership
- the common failure points directly tied to that structure

---

## Core Environment Model

Vintage Story modding is usually **asset-first, code-extended**.

Default assumptions:
- JSON assets define implemented content
- Code customizes behaviour applied to one or more assets
- a mod is commonly built from:
  - root metadata
  - `assets/<domain>/...`
  - optional C# code
- base-game assets are an immutable reference source for development
   - Harmony code patches allow modification of source as approperiate 
- compile success is not enough; in-game load and linkage validation matter

Think in this order:
1. mod root
2. asset domain
3. asset category/path
4. registered code class
5. asset-to-code linkage
6. client/server ownership
7. in-game behavior

---

## Project Structure

### 1. Mod root
Common root-level structure:
- `modinfo.json`
- optional `modicon.png`
- `assets/`
- for code mods: `.csproj` and C# source files

Assume:
- `modinfo.json` is required
- `modid` is a stability anchor
- changing `modid` often implies changing the asset domain and related references too

### 2. Assets
The main content structure is usually:
- `assets/<domain>/...`

That domain folder is the core namespace for the mod's content.

Common asset categories:
- `blocktypes`
- `itemtypes`
- `entities`
- `lang`
- `patches`
- `recipes`
- `worldproperties`
- `worldgen`
- `shapes`
- `textures`
- `sounds`

Assume:
- each direct child under `assets/` is a domain
- same-domain references may omit the domain prefix
- cross-domain references should usually be explicit
- broken references are often path, category, or domain problems before they are code problems

### 3. Code
For code mods, assume:
- at least one `ModSystem`
- C# classes for blocks, items, block entities, behaviors, systems, commands, or utilities
- code folders are project convention, not engine-mandated structure

Common code grouping may include:
- `Blocks`
- `Items`
- `BlockEntities`
- `Systems`
- `Commands`
- `Config`

Do not over-assume a pure C# app architecture.
The real structure is content + registration + runtime behavior.

---

## Asset to Code Linkage

The central structural rule is:

**Code classes do not stand alone. They usually exist because an asset points at them, or because a mod system registers them for asset use.**

Typical flow:
1. create or edit the asset JSON
2. create the C# class
3. register the class in `ModSystem`
4. link the asset to the class
5. test in game

When custom behavior is attached to a block/item/entity, common expectations are:
- the class is registered in code
- the asset JSON uses the expected class/id string
- names match exactly
- the asset is loading from the path/domain you think it is

When debugging, inspect both sides:
- asset definition, path, category, domain, and reference strings
- code registration, side execution, and actual runtime type

---

## Runtime Model

Treat client/server separation as a default structural concern.

Assume:
- singleplayer still includes client and server logic
- some hooks and logs may appear once per side
- rendering and presentation are usually client-side
- gameplay authority and shared state are usually server-side
- clients communicate through the server, not directly to each other

When something looks wrong, check:
- is this running on the correct side
- is this supposed to be shared logic or side-specific logic
- is this a real duplicate call, or one call per side
- is the bug logic-side, sync-side, or presentation-side

For code mods, assume `ModSystem` side split matters:
- `Start()` for shared registration/setup
- `StartClientSide()` for client-only work
- `StartServerSide()` for server-only work

---


## Key Base Classes and Interfaces

These are the main structural types to assume first when reading or planning a Vintage Story mod:

- `ModSystem` — main code-mod entry point for registration, startup, and side-specific setup
- `ICoreAPI` — shared API handle for assets, logging, registries, and access to side-specific APIs
- `Block` — base class for a placeable block
- `BlockEntity` — base class for per-placed-block stored state and runtime logic
- `BlockBehavior` — reusable composable behavior attached to blocks
- `BlockEntityBehavior` — reusable composable behavior attached to block entities
- `Item` — base class for an item
- `CollectibleObject` — shared base class for both blocks and items
- `CollectibleBehavior` — reusable composable behavior for collectibles at the shared item/block layer
- `Entity` — base class for in-world entities
- `EntityBehavior` — reusable composable behavior attached to entities
- `IClassRegistryAPI` — registry/factory API used to register and resolve custom classes
- `IWorldAccessor` — main world access interface for blocks, items, entities, and world state
- `ItemStack` — runtime stack of an item or block plus quantity and attributes
- `InventoryBase` — base class for custom inventory/container implementations
- `BlockPos` — standard block position type used throughout world/block APIs

Structural takeaway:
- inheritance matters, but Vintage Story also leans heavily on composition through behavior classes
- `Block` and `Item` both sit under `CollectibleObject`, so some logic belongs at the shared collectible layer rather than being duplicated

## Planning and Review Bias

When planning or reviewing Vintage Story work, separate these concerns instead of flattening them into one “feature”:

- mod root / metadata
- asset creation or patching
- asset path/category/domain correctness
- code class creation
- code registration
- asset-to-code linkage
- runtime side ownership
- in-game validation
- packaging/release structure

A feature often contains multiple distinct tasks:
- define assets
- define textures/shapes/lang
- register code
- link asset to class
- implement runtime behavior
- validate on correct side
- test in game

Review and planning should make those layers visible.

---

## Common Structural Risk Checks

Check these early:

- wrong `modinfo.json` or unstable `modid`
- wrong asset domain
- wrong asset category/path
- broken or implicit cross-domain reference
- missing or incorrect class registration
- incorrect class string / linkage in JSON
- modid/domain rename done only partially
- logic running on the wrong side
- assuming compile success proves the feature works
- packaging the mod with the wrong folder depth

Prefer these structural distinctions:
- new asset vs patch to existing asset
- asset issue vs code issue
- registration issue vs linkage issue
- client issue vs server issue vs shared issue
- compile success vs in-game success

---

## Working Summary

When in doubt, assume Vintage Story mods are structured like this:

- root `modinfo.json`
- optional root `modicon.png`
- `assets/<domain>/...`
- JSON-defined content and references
- one or more `ModSystem` entry points
- registered C# classes for custom behavior
- asset-to-code linkage must be explicit and correct
- client/server ownership matters constantly
- in-game validation is mandatory
- base-game assets are the main reference library
