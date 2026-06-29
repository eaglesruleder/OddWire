# OddWire

Vintage Story mod — farming and survival quality-of-life systems.

---

## What this is

OddWire adds hands-on organic systems to Vintage Story (game 1.21.0). The
centrepiece is the **CompostPile** — a tick-driven composting block: feed it dry
grass and food scraps with a starter culture, keep it moist and aerated, and it
works the inputs into compost over time across a two-stage decomposition pipeline.
Watering runs through the **vanilla watering can**, patched to feed any
moisture-aware block, and the **Survival** side adds a quality-of-life bag
right-click pickup. More features (Brazier, Plow) are in progress.

---

## Domains & docs

The in-code wiki — each link renders on GitHub; follow them down to the
implementation. Every domain and feature carries a gameplay **Brief** (`.git.md`)
and an implementation **Doc** (`.gpt.md`).

- **[Farming](OddWire/OddWire/Farming/.git.md)** — [Brief](OddWire/OddWire/Farming/.git.md) · [Doc](OddWire/OddWire/Farming/.gpt.md)
  - **CompostPile** — [Brief](OddWire/OddWire/Farming/CompostPile/.git.md) · [Doc](OddWire/OddWire/Farming/CompostPile/.gpt.md) — Active, ~1.9k LOC<br>Dry grass + nutrition ⇒ compost engine
  - **WateringCan patch** — [Doc](OddWire/OddWire/Farming/_Patches/WateringCan/.gpt.md) — Active, ~60 LOC<br>Calls `IWaterable.Water(dt)`
- **Survival** — _(todo)_
  - **BBRClickPickup patch** — [Doc](OddWire/OddWire/Survival/_Patches/BBRClickPickup/.gpt.md) — Support, ~0.3k LOC<br>Bag right-click pickup

Shared base: `_Common` / `_Extensions` — block-tint renderer + Vintage Story API
helpers (~0.5k LOC, Support).

Brief conventions (how these docs are written) live in [`.gpt/`](.gpt): the
[Summary](.gpt/gpt_brief.summary.md), [Feature Brief](.gpt/gpt_brief.feature.md),
[In-Repo Doc](.gpt/gpt_brief.repo.md), and [Code Brief](.gpt/gpt_brief.code.md) specs.

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
