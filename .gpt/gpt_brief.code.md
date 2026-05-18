This gpt_brief..md file defines the structure and content expectations for a Code Brief — an ephemeral, per-session coding task document generated from planning chat and consumed by Claude Code as the specific objective for one implementation session.

# Code Brief

## Purpose
A Code Brief is a session artifact, not a reference document.

It captures **what needs to change in this coding session and why** — the objective, the affected systems, and enough structural intent that Claude Code can begin without re-reading the full conversation history.

It sits between a user story and actual code:
- more concrete than a plan bullet
- less detailed than finished implementation
- structured enough for Claude Code to follow without conversational context

It is produced in planning chat, handed to Claude Code per prompt, and discarded or archived after the session.
It is consumed once. It does not live in the repo.

A Code Brief changes every session. It is not a stable reference — that is the Feature Brief's job.

---

## Relationship to Feature Brief

A Code Brief is always scoped to one objective inside a feature.
It assumes the reader already has the Feature Brief as standing context.

- Feature Brief — what the feature is and how it works
- Code Brief — what changes in this session and how the change should be structured

Do not repeat feature intent in the Code Brief.
Do not put per-session change requirements in the Feature Brief.

---

## Pseudocode as the Change Language

Code Briefs use behaviour-step language and rough pseudocode region skeletons to express change requirements.

This is the natural bridge between planning language and implementation language.
It follows `gpt_style.pseudocode.md` conventions:

- method names read like actions or decisions
- region labels describe behaviour steps, not fragments
- structure reads: require → resolve → apply → return

A skeleton in a Code Brief is not finished code. It is the intended shape.
Claude Code fills in the implementation inside that shape, following style conventions.

Example change requirement:

```
BECompostpile.TryApplyFertiliser(float boost)
  #region require nutrition has room
  #region resolve clamped boost amount
  #region apply boost to currentNutrition
  #region mark dirty and return accepted
```

This gives Claude Code the step structure without over-specifying the implementation.
The level of detail should match the complexity of the change — simple changes may need no skeleton at all.

---

## Structure

```md
# Code Brief — <Short Objective Name>

**Feature brief:** <filename or None>  
**Session date:** <date>  
**Scope:** <one-line scope statement, e.g. "Add fertiliser tool interaction to BECompostpile">

---

## Objective
What this session should achieve in plain terms.
One short paragraph. Focus on the player-facing or system-facing outcome, not the implementation path.

---

## Context
What is the current state of the relevant code.
What already exists that this change builds on or modifies.
What Claude Code should read first before touching anything.

- `BECompostpile.cs` — owns pile state and tick progression
- `CompostpileInventory.cs` — handles input slot rules
- No fertiliser tool exists yet

Keep this short. If the Feature Brief already covers it, do not repeat it here.

---

## Change Requirements
What needs to be created, modified, or removed.

Group by file or class. Use pseudocode region skeletons where the structure is meaningful.
Use plain language where the change is simple enough not to need a skeleton.

### New — ItemFertiliser.cs
```
OnHeldInteractStart()
  #region require target is BECompostpile
  #region call be.TryApplyFertiliser(ResolveBoostAmount())

ResolveBoostAmount()
  #region read boost from tool attributes
  #region clamp to valid range
  #region return boost value
```

### Modify — BECompostpile.cs
- Add `TryApplyFertiliser(float boost)` in the #Input region
```
TryApplyFertiliser(float boost)
  #region require nutrition has room
  #region resolve clamped boost
  #region apply and mark dirty
  #region return accepted bool
```

### New — itemtypes/fertiliser.json
- Standard item asset, no custom behaviour beyond class linkage

---

## Files and Systems Touched
Explicit list of what Claude Code should expect to open or create.

- `Items/ItemFertiliser.cs` — new
- `BlockEntities/BECompostpile.cs` — modified, #Input region
- `assets/wildfarm/itemtypes/fertiliser.json` — new

---

## Acceptance Criteria
What done looks like. Concrete enough to check without knowing the conversation history.

- Pile nutrition increases by expected boost amount on tool use
- Tool use on non-CompostPile target is silently rejected
- Tool use when nutrition is already at max is silently rejected
- No change to existing TryAddNutrition or tick behaviour

---

## Out of Scope
What this session should not touch, even if adjacent.

- Tool durability — separate brief
- Fertiliser crafting recipe — separate brief
- Moisture or aeration effects from fertiliser — not yet designed

---

## Open Questions
Anything unresolved that Claude Code should flag rather than guess.

- Should fertiliser apply instantly or queue into the next tick?

---
```

---

## Production

Code Briefs are produced in **planning chat** using `gpt_task.plan.md` Mode B.

When to produce one:
- when a Story or change requirement from the plan is ready to implement
- at the start of each Claude Code session, scoped to that session's objective
- when the user asks for an implementable handoff from the current conversation

Granularity is flexible. A Code Brief may cover:
- one new method
- one new class
- one Story from the plan
- a set of related small changes across a few files

The right size is whatever can be clearly expressed and validated in one session.
Do not combine unrelated objectives into one brief to save effort — they will create entangled output.

---

## Consumption

**By Code** (`gpt_task.code.md`):
- read at the start of the session alongside the Feature Brief
- Code navigates to the stated files, reads them, then implements to the stated change requirements
- Claude Code treats open questions in the brief as blockers to resolve by reading the code, not by guessing
- pseudocode skeletons are the intended shape; Claude Code fills in implementation per `gpt_style.pseudocode.md`

**By QA** (`gpt_task.qa.md`):
- used alongside the Feature Brief to check whether the implementation matches both the session objective and the feature intent
- acceptance criteria are the primary QA checkpoints

---

## What Good Looks Like

- Claude Code can start the session from the brief alone without needing the chat history
- Change requirements use enough pseudocode structure that the intended design is clear
- Out of scope is explicit, so Claude Code does not drift into adjacent work
- Acceptance criteria are concrete enough to check by reading the changed code
- It is short enough to read in under two minutes

## What Bad Looks Like

- Contains full implementation — brief should express intent, not write the code
- Contains no structure — plain language alone makes it hard to scope the session
- Mixes unrelated objectives — creates entangled output that is hard to review
- Repeats Feature Brief content — wastes reading time and risks divergence
- Acceptance criteria are vague — "works correctly" is not checkable
