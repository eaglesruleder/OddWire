This gpt_brief..md file defines the structure and content expectations for a Feature Brief — a durable, conceptual summary of a feature, subsystem, or domain that lives alongside the code it describes.

# Feature Brief

## Purpose
A Feature Brief is a stable reference artifact, not a task document.

It captures **what a feature is and why it exists** — the conceptual intent, the system shape, the core mechanics, and the known rules — without duplicating implementation detail or bug history.

It sits between a user story and a QA summary:
- broader than a coding task
- more concrete than a design sketch
- focused on intent and structure, not bugs or change history

It is produced in planning chat and lives in the solution repo.
It is consumed by Code as standing context, by QA as the intent reference, and by Plan as the canonical summary of what has been designed and built.

A Feature Brief does not change per coding session. It evolves when the feature itself changes in scope or behaviour.

---

## Scope Field

Every Feature Brief declares one of three scopes:

| Scope | Meaning | Example |
|---|---|---|
| `Domain` | A broad gameplay or system domain spanning multiple features | Farming, Agriculture, Storage |
| `Feature` | One self-contained player-facing or system-facing feature | CompostPile, Brazier, Plowland |
| `Subsystem` | A supporting technical subsystem owned by a feature or domain | CompostPile Inventory, Plowland Ticking |

A Domain brief summarises the shape and intent of the domain and how its features relate.
A Feature brief summarises one feature's mechanics, systems, and rules.
A Subsystem brief documents one technical layer when it is complex enough to need its own reference.

Use the smallest scope that is honest. Do not scope to Domain when the work is really one Feature.

---

## Structure

```md
# Brief — <Name>

**Scope:** Domain | Feature | Subsystem  
**Domain:** <mod domain, e.g. wildfarm>  
**Status:** Draft | Active | Stable | Superseded  
**Related briefs:** <links or filenames if applicable>

---

## Purpose
What this feature or domain exists to do in gameplay or software terms.
One short paragraph or tight bullet list.
Avoid implementation language here — describe player or system intent.

---

## Systems
What subsystems or components make up this feature and how they relate.
Name the key classes, assets, or layers without deep implementation detail.

- `BlockEntityCompostpile` — stores pile state, drives tick progression
- `CompostpileInventory` — manages input slots and acceptance rules
- `BlockCompostpile` — handles player interaction and block placement

For Domain scope, list the features instead of the subsystems.

---

## Core Mechanics
The main gameplay or runtime loops this feature owns.

For each mechanic:
- what drives it
- what it consumes or transforms
- what it produces or changes
- what can stall or fail it

Keep language close to behaviour-step style:
require → resolve → apply → produce

---

## Data and State
What is stored, what is derived, what is persisted.

- Stored: nutrition, moisture, aeration, tick accumulator
- Derived: conversionRate, outputRoom, progressFraction
- Persisted: everything in TreeAttribute on save
- Transient: cached derived values rebuilt on load

---

## Known Rules and Constraints
Concrete rules that define correct behaviour.
These are the things QA checks against and Code must not accidentally break.

- output room is always checked before mutation
- moisture must be within band for progression to run
- aeration resets to zero on harvest
- nutrition is clamped between 0 and maxNutrition

---

## Open Questions and Future Directions
Unresolved design questions or known planned expansion.
Not a todo list — record only things that affect how the feature should be understood now.

---
```

---

## Production

Feature Briefs are produced in **planning chat** using `gpt_task.plan.md` Mode B.

When to produce one:
- when a feature or domain has enough design throughput to be worth a stable reference
- when an Epic in the plan record has been developed enough to need a separate summary
- before handing off to a first implementation session in Claude Code
- when QA finds repeated gaps in intent clarity that should be anchored somewhere

A Feature Brief is not required for every feature. Small or simple features may be fully covered by the plan record and a Code Brief alone.

---

## Consumption

**By Code** (`gpt_task.code.md`):
- read as standing context at the start of a Claude Code session
- tells Code what the feature is for before it reads the Code Brief
- does not change per session

**By QA** (`gpt_task.qa.md`):
- used as the intent reference when reviewing whether behaviour matches design
- QA validates against Known Rules and Constraints, and Core Mechanics
- bugs are not recorded in the Feature Brief — they live in task or issue tracking

**By Plan** (`gpt_task.plan.md`):
- acts as the canonical summary of what has been designed and agreed
- keeps the plan record from re-litigating settled design
- updated when scope or mechanic intent changes meaningfully

---

## What Good Looks Like

- Someone unfamiliar with the feature can read it and understand what it does and why
- Core mechanics are described in behaviour-step language close enough to be useful for Code and QA
- Known rules are concrete enough that QA can check a change against them
- Scope is honest — a Feature Brief does not quietly become a Domain brief
- It stays useful as a reference across multiple coding sessions without needing updates

## What Bad Looks Like

- Contains implementation detail that belongs in a Code Brief or in comments
- Contains bug history or change log entries
- Scope is too broad to be useful as a coding reference
- Written once then silently drifts from what was actually built
- So abstract it tells the reader nothing about actual behaviour
