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
- method-local `#region`s may also be used as **behaviour-step regions** when a method has several meaningful steps

---

## Core Style Rule
The code should read like:

**check -> decide -> do -> return**

Not like:

**clever expression -> hidden behaviour -> comment explaining it afterwards**

A reader should be able to skim:
- file regions for major concern grouping
- method names for behaviour grouping
- method-local regions for step grouping
- code lines for exact implementation

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

### 9. Use descriptive `#region`s at two levels
When one file owns several directly related concerns, keep it navigable.

Use file-level `#region`s for major concern grouping, for example:
- `StoredState`
- `RateHelpers`
- `Harvest`
- `Input`
- `StateUpdates`
- `Persistence`

Use method-level `#region`s only when a method has several meaningful steps and those steps are worth skimming as a mini-outline.

Preferred remedy order:
1. improve names
2. add or improve descriptive `#region`s
3. improve local method flow
4. split files later if needed

A large file should not fail just for being large.
It should fail when the reader cannot quickly find the concern they need.

### 10. Treat method-local `#region`s as behaviour-step regions
Inside a longer method, a `#region` should mark one real step in the method story.

Good region use:
- one guard block
- one derived-value block
- one mutation block
- one return/finish block

Bad region use:
- wrapping arbitrary lines just to create folds
- titles that only describe syntax
- titles that are too vague to tell what the step actually proves or changes

A method should be skimmable like:
- require valid source
- require room
- resolve conversion rate
- resolve consumable quantity
- apply state mutation
- return accepted quantity

### 11. Region names should describe intent, not just fragments
Prefer region titles that tell the reader what the step means.

Prefer:
- `Require nutrition props`
- `Cap by nutrition room`
- `Resolve nutrition per input`
- `Resolve consumable input qty`
- `Resolve nutrition output qty`
- `Apply nutrition gain`
- `Return consumed qty`

Avoid:
- `If we have NutritionProps,`
- `Room,`
- `A cost per input,`
- `Atleast one input,`
- `And return true out acceptedstackConsumedQty`

The rough form is still useful during ideation, but final region names should read like stable behaviour-step language.

### 12. Each region should usually end with one of three outcomes
A region should usually:
- reject and return
- compute and store a value needed by later regions
- mutate state in one clear way

If a line introduces a new semantic step, it usually deserves either:
- its own region
- or a better helper name

Do not leave the most important step as unlabelled math in the middle if the surrounding regions are carrying the story.

### 13. Use regions to make IDE folding become a design outline
In the editor, file regions and method-local regions should let the user skim the code as if it were a design note.

That means the fold labels should tell a truthful story of the code.
If the fold labels are weak, the pseudocode layer is weak even if the implementation is correct.

The ideal is:
- fully expanded: exact code
- partially folded: exact algorithm outline
- mostly folded: concern map of the class

### 14. Region-backed pseudocode is a valid collaboration format
A user may provide a skeleton like:

```csharp
TryAddNewResource()
{
    #region Check we have room
    #endregion

    #region Check we have valid input
    #endregion

    #region Resolve conversion
    #endregion

    #region Apply mutation
    #endregion

    #region Return accepted qty
    #endregion
}
```

That is a valid implementation handoff shape.

When working from this kind of skeleton:
- preserve the region order unless there is a real correctness issue
- fill each region with the narrowest logic that matches the heading
- tighten region names if needed, but keep the original intent
- do not silently replace the structure with a totally different abstraction unless clearly beneficial
- mention when a region is missing a required step or mixes multiple steps

### 15. Prefer one clear level of intent per method
A method should usually read at one dominant level:
- top-level flow
- or local implementation detail

If a method is mixing five tiny math decisions, three side effects, and two domain exceptions, either:
- improve the region outline
- or extract a helper whose name explains the chunk better than the inline block does

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

### 4. If the method gets medium-sized, add a truthful region outline
Use method-local `#region`s when they improve skim-reading and future promptability.

### 5. Comment only where naming cannot carry intent
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
- prefer region-backed step outlines over premature file splitting when the class is still one coherent mechanic

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
- method-local `#region`s may be used as behaviour-step regions
- region names should describe behaviour, not just fragments
- when given a region skeleton, preserve it and implement to that structure where practical
- ask early when missing details would materially change the answer
- if a best-effort answer is still useful, give the narrowest useful answer and state assumptions plainly
