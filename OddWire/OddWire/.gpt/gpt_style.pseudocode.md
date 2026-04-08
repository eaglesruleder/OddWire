This gpt_style..md file describes the expected response style, shaping how the assistant should write, structure, and present its output without changing the task itself.

# Applied Pseudocode

## Purpose
Write code in a style that reads like practical pseudocode implemented directly in C#.

The goal is not abstract “clean code”.
The goal is code that is fast to read, easy to reason about, and shaped like intent.

This style is written for **RAD development**.
That means:
- direct implementation is often better than early abstraction
- one file doing a lot of directly related work is acceptable
- helper extraction is selective, not automatic
- descriptive `#region`s are a valid readability tool in larger mechanic files

---

## Core Style Rule
The code should read like:

**check -> decide -> do -> return**

Not like:

**clever expression -> hidden behaviour -> comment explaining it afterwards**

---

## What this means in practice

### 1. Top-level methods should read like process steps
A reader should be able to skim a method and understand the whole story without diving into every line.

Prefer:

```csharp
public bool TryAddFromHeldSlot(ItemSlot slot, out int accepted)
{
    accepted = 0;

    if (!CanAcceptFrom(slot))
        return false;

    CollectAcceptedQuantity(slot, out accepted);
    ApplyAcceptedQuantity(slot, accepted);
    RefreshPileState();

    return accepted > 0;
}
```

But do not extract helpers just for style.
A short inline method is also good when it already reads clearly.

### 2. Prefer readable inline logic when it is already clear
Good applied pseudocode is often a compact method with a few guards and one action.

Prefer:

```csharp
public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
{
    if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be)
        return false;

    var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
    if (slot?.Itemstack == null)
        return false;

    if (!be.TryAdd(slot, out int accepted)
    ||  accepted < 1
        )
        return false;

    slot.TakeOut(accepted);
    slot.MarkDirty();
    return true;
}
```

This is good when the flow is still easy to scan.

### 3. Use helpers when the name adds real value
Extract a helper when:
- the block has strong domain meaning
- the same logic is reused
- a comment would otherwise be needed
- the parent method becomes hard to scan
- the extracted name improves readability more than the jump hurts it

Do not extract a helper when:
- the inline code is already short and obvious
- the helper would only wrap one or two trivial lines
- the result would feel fragmented or over-abstracted

### 4. Prefer good variable names over explanatory prose
Prefer:
- `acceptedQuantity`
- `remainingNutrition`
- `harvestedCompostpile`
- `targetMoisture`
- `isClientSide`
- `canRestoreAeration`

Avoid:
- `x`
- `val`
- `obj`
- `tmp`
- `result2`
- `flag` when a real meaning exists

Short local names are acceptable when they have common, meaningful usage in the environment:
- `beFoo` over `blockEntityFoo`
- `pos` over `position`

### 5. Use methods as sentence fragments
Method names should sound like actions or decisions:
- `CanAcceptFrom`
- `TryRestoreAeration`
- `GetCheapestNutritionCategory`
- `ConsumeAvailableFuel`
- `ShouldDiscardLeftovers`
- `DropRecoveredCompost`

That lets the caller read like rough English.

### 6. Prefer explicit control flow
Use readable `if`, `foreach`, and early returns.
Do not compress meaningful logic into dense expressions just because it is shorter.

### 7. Prefer vertical scanning
Where it fits the codebase, line up compound conditions so they scan cleanly:

```csharp
if (world.Side != EnumAppSide.Server
||  blockSel is null
||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
   )
    return false;
```

This style is good when it makes the logic feel like stacked reasons, not a dense sentence.

### 8. Use comments sparingly
Do not write comments that just restate obvious code.

Good comment use:
- non-obvious domain rules
- invariants
- engine quirks
- intentionally weird behaviour that might be “fixed” by mistake
- communication notes requested by the user

### 9. Use descriptive `#region`s in larger RAD files
When one file owns several directly related concerns, keep it navigable.

Preferred remedy order:
1. improve names
2. add or improve descriptive `#region`s
3. improve local method flow
4. split files later if needed

A large file should not fail just for being large.
It should fail when the reader cannot quickly find the concern they need.

---

## Preferred Thinking Pattern

When solving a task, write the code in this order:

### 1. Name the behaviour
What is actually happening?
- reject invalid placement
- restore aeration from harvest
- consume cheapest nutrition first
- sync tint from pile contents

### 2. Write the caller as steps
Write the top-level method so it reads like the intended process.

### 3. Keep detail local unless extraction helps
Push complexity down only when the helper name makes the code easier to read.

### 4. Comment only where naming cannot carry intent
Use comments for:
- domain rules
- invariants
- engine quirks
- intentionally weird behaviour

---

## Preferred Engineering Constraints

When writing code in this language:
- preserve surrounding style and formatting
- do not refactor unrelated code
- prefer narrow changes
- avoid generic “utility” extraction unless it removes real duplication
- avoid LINQ where possible
- keep client/server concerns separated
- do not invent abstractions unless the code actually needs them
- allow one file to hold multiple layers of directly related gameplay logic during active iteration

---

## Question Behaviour

Asking clarifying questions is good engineering behaviour when missing detail would materially change the answer.

Rules:
- ask early when the missing detail would materially change the patch, review, design advice, or recommended architecture
- keep the question brief and specific
- end response there if the ambiguity is truly blocking
- do not continue into a long assumption-heavy answer first
- otherwise, give the narrowest useful best-effort response and state assumptions plainly

---

## Useful Prompt to Reuse

Write code in an **applied pseudocode** style.

I want the code to read like rough English or broken-English pseudocode, while still being real implementation code.

Rules:
- make top-level methods read like process steps
- prefer readable inline logic when it is already clear
- use helper methods only when the name adds real value
- use variable names that carry meaning
- keep one clear level of intent per method where practical
- prefer explicit flow over clever compressed expressions
- allow large files when the work is directly related
- use descriptive `#region`s before recommending file splits
- ask early when missing details would materially change the answer
- if a best-effort answer is still useful, give the narrowest useful answer and state assumptions plainly
