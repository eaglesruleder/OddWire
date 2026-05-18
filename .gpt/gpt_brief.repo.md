This gpt_brief..md file defines the structure and content expectations for an In-Repo Doc — a compact, durable `.gpt.md` file that lives inside a feature's source folder and serves as the primary reference for both human readers and AI sessions.

# In-Repo Doc

## Purpose
An In-Repo Doc is a folder artifact, not a task document or planning record.

It captures **what a feature is and how it is implemented** in the most compressed form that remains useful — a tight summary header followed by a pseudocode body that reflects the actual shape of the code.

It sits between a Feature Brief and the source code itself:
- more concrete than a Feature Brief
- far denser than prose documentation
- grounded in implementation, not just intent

It lives in the feature's source folder in the repo, named `<feature>.gpt.md`.
It is consumed by humans browsing the folder, by Code as cheap standing context, and by QA as the implementation shape reference.

Target compression is **80–90% pseudocode, 10–20% prose** — compact enough to be cheap AI context and fast human orientation, not a substitute for reading the code but a reliable map of it.

An In-Repo Doc evolves when the feature's mechanics or implementation shape change meaningfully. It is not updated per session — that is the Code Brief's job.

---

## Structure

```md
# Doc — <Feature Name>

**Scope:** Feature | Subsystem
**Domain:** <mod domain, e.g. wildfarm>
**Status:** Draft | Active | Stable | Superseded

---

## Summary

**Goal:** One sentence. What this feature does in functional or mechanical system terms.

**Lifecycle:**
1. <player or system action>
2. <next step>
3. ...

**Rules:**
- <hard constraint — things Code must not accidentally break>
- <hard constraint>
- ...

**State:** `Stored: x, y, z — Derived: a, b — Persisted: on save`

---

## Implementation

### ClassName
One line — what this class owns or is responsible for.

```
MethodName() — one-line purpose if not obvious from name
  #region step description
  #region step description
  #region step description

MethodName()
  #region step description
  #region step description
```

### ClassName
...

---
```

---

## Summary Section Rules

### Goal
One sentence in system terms. Not player experience language, not design rationale.

Good:
> Converts organic inputs into compost over time, driven by nutrition, moisture, and aeration values.

Bad:
> Lets players make compost to improve soil and feel like a real farmer.

### Lifecycle
The happy-path interaction sequence. 4–7 steps maximum.
Write in terse imperative form — player or system action per line.
Do not describe internal mechanics here — that belongs in the Implementation body.

```
1. Place block
2. Fill inputs — browns, inoculum, nutrition source
3. Maintain moisture
4. Wait — tick progression runs while moisture in band
5. Turn — dig with shovel to restore aeration
6. Harvest — shovel when ready, resets pile state
```

Bad: prose paragraph describing the same sequence. Terse steps only.

### Rules
3–6 bullets. Constraint language only.
These are the things that define correct behaviour — what QA checks and Code must not break.
One rule per bullet. No elaboration.

### State
One line. Three categories at most: Stored, Derived, Persisted.
Omit Persisted if everything stored is always persisted.

---

## Implementation Section Rules

### Class entries
One class per heading. One-line purpose statement if the class name does not already make it obvious.

### Method entries
Write each method as a pseudocode region skeleton.
Follow `gpt_style.pseudocode.md` region-step conventions:
- region labels describe behaviour steps, not code fragments
- prefer: require → resolve → apply → return
- one region per meaningful step

Add a one-line purpose note after the method name only when the name alone is ambiguous.

Use `// Intent:` annotations alongside a skeleton or region for non-obvious rules, invariants, or operator choices that fall outside region labels but would be easy to accidentally break.

```
Update()
  // Intent: | not || — both UpdateState and ProcessCompost must always run
  #region require fully loaded chunk
  #region run UpdateState and ProcessCompost
```

Omit trivial getters, setters, and pass-through methods unless they carry a non-obvious rule.

Group methods by their natural region grouping if the class uses `#region` sections:
```
### BECompostpile

// #region Tick
OnGameTick()
  #region require server side
  #region accumulate delta
  #region require threshold reached
  #region run progression step

// #region Input
TryAddNutrition()
  #region require nutrition props
  #region cap by nutrition room
  #region resolve nutrition per input
  #region apply nutrition gain
  #region return consumed qty
```

Use `// #region` comment markers to show file-level region grouping without breaking the doc's own markdown.

---

## Production

In-Repo Docs are produced in **planning chat** using `gpt_task.plan.md` or during a **QA pass** after a feature reaches a stable shape.

**Filename convention:** `<feature>.gpt.md`, placed in the feature's source folder.
Use the feature name, not the class name — `compostpile.gpt.md`, not `BECompostpile.gpt.md`.
If a subsystem warrants its own doc, use `<feature>.<subsystem>.gpt.md`.

When to produce one:
- when a feature has completed its first working implementation
- when a feature is complex enough that future sessions would benefit from a map of the code
- when QA or Code sessions repeatedly need to re-orient to the same implementation

When to update one:
- when a mechanic loop changes meaningfully
- when a class is added, split, or removed
- when a hard rule is changed by design decision

When not to update one:
- after a refactor that preserves all behaviour — update only if the pseudocode shape changed
- to record bugs or change history — those belong in issue tracking

---

## Consumption

**By Code** (`gpt_task.code.md`):
- read at session start alongside the Code Brief as cheap orientation context
- gives Code the implementation map before it opens any files
- reduces the need to scan unfamiliar code before making changes

**By QA** (`gpt_task.qa.md`):
- used as the implementation shape reference when reviewing changes
- QA checks that changed methods still match the step structure and that rules still hold
- also used to identify when the doc itself has drifted and needs updating

**By Plan** (`gpt_task.plan.md`):
- read when planning changes to an existing feature to understand current implementation scope
- helps Plan avoid re-litigating what is already built

**By humans browsing the repo:**
- primary orientation artifact for anyone unfamiliar with the feature
- should be readable without opening any source files

---

## Example — compostpile.gpt.md (partial)

```md
# Doc — CompostPile

**Scope:** Feature
**Domain:** wildfarm
**Status:** Active

---

## Summary

**Goal:** Converts organic inputs into compost over time, driven by nutrition, moisture, and aeration values.

**Lifecycle:**
1. Place block
2. Fill inputs — browns, inoculum, nutrition source
3. Maintain moisture
4. Wait — tick progression runs while moisture in band
5. Turn — dig with shovel to restore aeration
6. Harvest — shovel when ready, resets pile state

**Rules:**
- Output room checked before any mutation
- Progression stalls outside moisture band
- Aeration resets to zero on harvest
- Nutrition clamped between 0 and maxNutrition

**State:** `Stored: nutrition, moisture, aeration, tickAccumulator — Derived: conversionRate, outputRoom, progressFraction`

---

## Implementation

### BECompostpile
Owns pile state and drives tick progression.

// #region Tick
OnGameTick()
  #region require server side
  #region accumulate delta
  #region require threshold reached
  #region run progression step

// #region Input
TryAddNutrition()
  #region require nutrition props
  #region cap by nutrition room
  #region resolve nutrition per input
  #region apply nutrition gain
  #region return consumed qty

// #region Harvest
TryHarvest()
  #region require ready state
  #region drop output stack
  #region reset pile state
  #region mark dirty

### CompostpileInventory
Manages input slots and acceptance rules.

TryAdd()
  #region require valid item type
  #region require slot room
  #region apply to slot
  #region mark dirty
```

---



- Summary section readable in under 15 seconds
- Lifecycle makes the feature immediately graspable before reading any code
- Rules are concrete enough for QA to check a change against
- Pseudocode skeletons reflect the actual method step structure in the codebase
- A new session can orient from this doc without reading full source files
- Total doc fits in low token count — stays cheap as context

## What Bad Looks Like

- Summary creeps into design rationale or player experience language
- Lifecycle becomes a prose paragraph instead of terse steps
- Pseudocode skeletons are too vague to distinguish what each method actually does
- Implementation section lists every method including trivial ones — becomes noise
- Doc is written once and drifts silently from the real codebase shape
- Prose explanations replace region skeletons where pseudocode would be denser and clearer
