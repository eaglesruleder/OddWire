# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OddWire is a Vintage Story mod (mod ID: `oddwire`) targeting game version 1.21.0+. It is built on .NET 8.0 and uses the Cake build system for packaging. The mod adds farming enhancements (compost piles, plowland, watering cans, barrel recipes) and smithing improvements (brazier).

The environment variable `VINTAGE_STORY` must point to the game installation directory for the build and debug targets to resolve game assemblies.

## Build & Package

**Package a release zip** (runs ValidateJson → Build → Package, outputs to `Releases/`):
```
# VS Code: Terminal > Run Task > "package"
dotnet run --project CakeBuild/CakeBuild.csproj
# or
./build.ps1   # Windows
./build.sh    # Linux/Mac
```

**Debug build only** (no packaging):
```
dotnet build -c Debug OddWire/OddWire-Timberworks/OddWire-Timberworks.csproj
```

**Skip JSON asset validation** (faster iteration):
```
dotnet run --project CakeBuild/CakeBuild.csproj -- --skipJsonValidation=true
```

There are no automated tests; correctness is verified by running the mod in-game.

## Architecture

### ModSystem Lifecycle

Vintage Story loads `ModSystem` subclasses in `ExecuteOrder()` sequence. OddWire uses three systems:

| Class | Order | Role |
|---|---|---|
| `OddWireHarmony` | (default) | Applies all `[HarmonyPatch]` classes via `harmony.PatchAll()` |
| `OddWireRegistrySystem` | 0.61 | Registers block/item/behaviour classes; partial class split per subsystem |
| `OddWireRecipeLoader` | 1.0 (server only) | Loads custom recipes after assets are available |
| `DisableOddWireRegistrySystem` | 99999 | Sets `canRegister = false` to prevent late registrations |

`OddWireRegistrySystem` is a partial class. Each subsystem adds its own `_Registry.cs` file that contributes `Start_X(api)` and `AssetsLoaded_X(api)` partial methods.

### Subsystem Layout

```
OddWire/OddWire/
├── OddWireRegistrySystem.cs   # Main entry point (partial)
├── OddWireHarmony.cs          # Harmony patch bootstrapper
├── Farming/
│   ├── _Registry.cs           # Partial: Start_Farming, AssetsLoaded_Farming
│   ├── OddWireFarming.cs      # Farming constants / shared state
│   ├── Barrel/                # BarrelFailureRecipe + Harmony patch on BEBarrel
│   ├── CompostPile/           # BlockEntity with inventory, heat, tint rendering
│   ├── Plowland/              # PlowlandEngine (moisture/nutrient simulation)
│   └── WateringCan/           # Harmony patch on BlockWateringCan
├── Smithing/
│   └── Brazier/               # In-progress fuel/heat system
├── _Common/                   # Shared renderers
└── _Extensions/               # Extension methods wrapping VS APIs
    ├── System/
    └── VintageStory/          # Mirrors VS namespace tree
```

### Key Patterns

**Harmony patches** live alongside the feature they patch (e.g., `BEBarrel.harmonypatch.cs`, `BlockWateringCan.harmonypatch.cs`). `OddWireHarmony.PatchAll()` picks them all up automatically — no manual registration needed.

**Extension methods** in `_Extensions/` wrap VintageStory APIs. The directory structure mirrors the VS namespace (e.g., `_Extensions/VintageStory/API.Common/ItemStackExtensions.cs`). Prefer adding new wrappers here rather than calling raw API inside feature code.

**Asset pipeline**: JSON assets live in `OddWire/OddWire/assets/`. The Cake build validates every `.json` file with `Newtonsoft.Json` before building. Custom recipe types (barrel-fail) are loaded server-side in `OddWireRecipeLoader`.

**PlowlandEngine** (`Farming/Plowland/PlowlandEngine.cs`) is the most complex system: it manages per-block moisture and nutrient values updated on game ticks, with soil fertility tiers defined in `FertilitySet.cs`.

### Adding a New Subsystem

1. Create a folder under `Farming/` or `Smithing/` (or a new top-level category).
2. Add a `_Registry.cs` with a `partial class OddWireRegistrySystem` contribution.
3. Call `Start_X(api)` from `OddWireRegistrySystem.Start` and `AssetsLoaded_X(api)` from `AssetsLoaded`.
4. Place any Harmony patches as `*.harmonypatch.cs` files inside the feature folder.
5. Add JSON assets under `assets/` following the existing folder conventions.
