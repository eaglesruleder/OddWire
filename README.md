# OddWire

Vintage Story mod — Farming and Survival systems.

---

## What this is

OddWire adds hands-on organic systems to Vintage Story 1.21.0.

### Farming

Adds **CompostPile** — consumes perishables to create inoculum (rot), then consumes
inoculum and dry grass to create compost. Driven by moisture, temperature, and
aeration (topped up by adding to the pile).

Adds **Plowland** — a plow tool that turns soil into high-retention furrowed
farmland, sustaining moisture and NPK so crops cycle faster through a season.

Patches the **Watering Can** — now checks for an `IWaterable` interface to extend
functionality.

### Survival

Patches **BlockBehaviourRightClickPickup** — allows baskets (via `MaxSlots < 4`)
with inventory to be held in the toolbar, trading tool-holding slots for small
inventory boons.

---

## Domains & docs
Conventions: [gpt..md](https://github.com/eaglesruleder/gpt..md) repo

- **[Farming](OddWire/OddWire/Farming/.git.md)** — [Brief](OddWire/OddWire/Farming/.git.md) · [Doc](OddWire/OddWire/Farming/.gpt.md)
  - **CompostPile** — [Brief](OddWire/OddWire/Farming/CompostPile/.git.md) · [Doc](OddWire/OddWire/Farming/CompostPile/.gpt.md) — Active, ~1.9k LOC<br>Dry grass + nutrition ⇒ compost engine
  - **Plowland** — [Brief](OddWire/OddWire/Farming/Plowland/.git.md) · [Doc](OddWire/OddWire/Farming/Plowland/.gpt.md) — Active, ~0.6k LOC<br>Plow soil into high-retention farmland furrows
  - **WateringCan patch** — [Brief](OddWire/OddWire/Farming/_Patches/WateringCan/.git.md) · [Doc](OddWire/OddWire/Farming/_Patches/WateringCan/.gpt.md) — Active, ~60 LOC<br>Calls `IWaterable.Water(dt)`
- **[Survival](OddWire/OddWire/Survival/.git.md)** — [Brief](OddWire/OddWire/Survival/.git.md)
  - **BBRClickPickup patch** — [Brief](OddWire/OddWire/Survival/_Patches/BBRClickPickup/.git.md) · [Doc](OddWire/OddWire/Survival/_Patches/BBRClickPickup/.gpt.md) — Support, ~0.3k LOC<br>Bag right-click pickup
- **`_Common`** — Support, ~140 LOC<br>Block-tint renderer
- **[`_Extensions`](OddWire/OddWire/_Extensions/.git.md)** — [Brief](OddWire/OddWire/_Extensions/.git.md) · [Doc](OddWire/OddWire/_Extensions/VintageStory/.gpt.md) — Support, ~0.4k LOC<br>Vintage Story API + System helpers

---

## Build

The project uses the [Cake](https://cakebuild.net/) build system to produce a
release-ready mod zip. Build with any of:

- Run the **CakeBuild** project from Visual Studio or Rider.
- In VS Code: **Terminal → Run Task → package**.
- Run `build.ps1` (Windows) or `build.sh` (Linux / Mac).

The packaged `oddwire_<version>.zip` is written to the `Release` folder in the
project root.

> **Linux / Mac:** the launch config is preconfigured for Windows. Update the
> `executablePath` entries in `launchSettings.json` to the platform binaries
> (`$(VINTAGE_STORY)/Vintagestory`, `$(VINTAGE_STORY)/VintagestoryServer`) before
> running from an IDE.
