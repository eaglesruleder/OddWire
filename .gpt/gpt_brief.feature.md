This gpt_brief..md file defines the structure and content expectations for a Feature Brief — a concise, gameplay-facing summary of a feature that serves as the QA session opener and standing context for Code and Plan.

# Feature Brief

## Purpose
A Feature Brief is a stable reference artifact, not a task document.

It captures **what a feature is and how it moves** — the gameplay loop, the driving values, the system shape — in the most compressed form that remains immediately useful.

It is the opener for every QA session. A reader should be able to identify the feature, understand what drives it, and know what the player does with it before reading a single line of code or findings.

It is produced in planning chat and lives in the solution repo.
It is consumed by QA as the session opener, by Code as standing context, and by Plan as the canonical gameplay summary.

Known rules, constraints, and risks are QA outputs — derived fresh from code each session. They do not live here.

A Feature Brief does not change per session. It evolves when the feature's gameplay loop or system shape changes meaningfully.

---

## Scope Field

Every Feature Brief declares one of three scopes:

| Scope | Meaning | Example |
|---|---|---|
| `Domain` | A broad gameplay or system domain spanning multiple features | Farming, Agriculture, Storage |
| `Feature` | One self-contained player-facing or system-facing feature | CompostPile, Brazier, Plowland |
| `Subsystem` | A supporting technical subsystem owned by a feature or domain | CompostPile Inventory, Plowland Ticking |

Use the smallest scope that is honest. Do not scope to Domain when the work is really one Feature.

---

## Structure

```md
# Brief — <Feature Name>

**Scope:** Domain | Feature | Subsystem
**Domain:** <mod domain, e.g. wildfarm>
**Status:** Draft | Active | Stable | Superseded

---

## What it is
One line. System terms only. Not player experience language.

---

## Core Loop

```
Step → Step → Step → Step
```

Inputs: **x** (role), **y** (role), **z** (role)
Output: **result** — note what is lossy and what is recovered.

One sentence on tick cadence or timing if relevant.

---

## Driving Values

| Value | What drives it | What it does |
|---|---|---|
| ... | ... | ... |

---

## <Central Gate or Formula> — include only if the feature has one named mechanic worth surfacing
One line on what it is, then the formula or condition.

---

## <Output Resolution> — include only if processing has a meaningful split or cascade worth naming

---

## Gameplay Loop

| Input | Action | Reward | Risk |
|---|---|---|---|
| ... | ... | ... | ... |

---

## Class Responsibility

| Class | Owns |
|---|---|
| ... | ... |
```

---

## Section Rules

### What it is
One sentence in system terms. Not player experience language, not design rationale.

Good:
> A tick-driven organic simulation. Inputs decompose into compost over time. Four environmental axes interact to set the rate.

Bad:
> Lets players make compost to improve their soil.

### Core Loop
The happy-path flow as a single arrow chain. Then inputs and output on separate lines.
Note lossiness explicitly — what does not come back on harvest is a hard rule worth stating here.
One sentence on tick cadence if the feature is time-driven.

### Driving Values
One row per value axis. Three columns: the value name, what moves it, what it gates or affects.
Keep language close to behaviour — not design rationale.
Omit derived display values and trivial pass-throughs.

### Central Gate or Formula
Only include if the feature has one named mechanic that is the primary progression gate.
Name it explicitly. Give the formula or stall condition in one block.

### Output Resolution
Only include if the processing step has a meaningful split, cascade, or overflow rule.
One short paragraph or arrow chain. Not a full pseudocode skeleton — that lives in the In-Repo Doc.

### Gameplay Loop Table
One row unless interactions are genuinely distinct and mutually exclusive.
Four columns: Input, Action, Reward, Risk.
Cells may contain multiple values as line-separated entries.
This is the player-facing view. Keep it honest about what is always lost and what is conditional.

### Class Responsibility
One row per class. One-line ownership statement.
Not an implementation map — that lives in the In-Repo Doc.

---

## Production

Feature Briefs are produced in **planning chat** using `gpt_task.plan.md` Mode B, or extracted by **QA** cold from code at the start of a review session.

When to produce one:
- when a feature has enough gameplay throughput to be worth a stable reference
- before handing off to a first implementation session in Claude Code
- when QA cold-produces one and it is worth anchoring as a standing artifact

When to update one:
- when the gameplay loop changes meaningfully
- when the driving value set changes
- when the class responsibility split changes

When not to update one:
- after a refactor that preserves gameplay behaviour
- to record rules, risks, or findings — those belong in QA output

---

## Consumption

**By QA** (`gpt_task.qa.md`):
- produced cold from code at session start — this is the QA opener
- if a Feature Brief was already provided, QA compares its cold output against it
- divergence between the two is a finding

**By Code** (`gpt_task.code.md`):
- read as standing context before the Code Brief
- tells Code what the feature does before it reads what needs to change

**By Plan** (`gpt_task.plan.md`):
- acts as the canonical gameplay summary of what has been designed
- keeps the plan record from re-litigating settled design

---

## What Good Looks Like

- Readable in under 30 seconds
- Core Loop makes the feature immediately graspable
- Driving Values table tells you what levers the player and system are pulling
- Gameplay Loop table is honest about losses and risks
- Class Responsibility table gives Code the system map without implementation detail
- Known rules and constraints are absent — those are QA's job

## What Bad Looks Like

- Contains known rules or constraints — those drift and belong in QA output
- Contains implementation detail — that belongs in the In-Repo Doc
- "What it is" line uses player experience language instead of system terms
- Core Loop becomes a prose paragraph instead of a step chain
- Driving Values rows are vague or describe UI rather than mechanics
- Written once then silently drifts from what was actually built
