# OddWire

Vintage Story mod — farming and survival quality-of-life systems.

---

## What this is

OddWire is a code mod for Vintage Story (game 1.21.0) organised into self-contained
domains. Each domain holds one or more features — custom blocks and block entities,
plus targeted Harmony patches over vanilla behaviour — on a shared base of API
extensions and renderers. Documentation lives next to the code as an in-repo wiki:
every domain and feature carries a gameplay **Brief** (`.git.md`) and an
implementation **Doc** (`.gpt.md`), cross-linked so you can read down from this page
to a single method without leaving GitHub.

---

## Domains

| Domain | Adds | Size | Status |
|---|---|---|---|
| [Farming](OddWire/OddWire/Farming/.git.md) | CompostPile composting block + watering-can `IWaterable` patch | ~2.0k LOC | Active |
| Survival | Bag right-click pickup patch (`BBRClickPickup`) | ~0.3k LOC | Active |
| `_Common` / `_Extensions` | Shared block-tint renderer + Vintage Story API extension helpers | ~0.5k LOC | Support |

---

## Priorities

- **Active:** CompostPile — currently in playtest tuning (branch `Compostpile-Playtest`).
- **Next:** Brazier and Plow features (branches exist, not yet documented).
- **Parked:** Survival `BBRClickPickup` patch is in place and stable; no brief yet.

---

## Docs map

The in-code wiki. Each link renders on GitHub; follow them down to the implementation.

- **[Farming](OddWire/OddWire/Farming/.git.md)** — [Brief](OddWire/OddWire/Farming/.git.md) · [Doc](OddWire/OddWire/Farming/.gpt.md)
  - **CompostPile** — [Brief](OddWire/OddWire/Farming/CompostPile/.git.md) · [Doc](OddWire/OddWire/Farming/CompostPile/.gpt.md)
  - **WateringCan patch** — [Doc](OddWire/OddWire/Farming/_Patches/WateringCan/.gpt.md)
- **Survival** — _(todo)_
  - **BBRClickPickup patch** — _(todo)_

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
