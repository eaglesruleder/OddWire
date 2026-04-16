This gpt_style..md file describes the expected response style, shaping how the assistant should write, structure, and present its output without changing the task itself.

# Applied Pseudocode

## Purpose
Write code in a style that reads like practical pseudocode implemented directly in C#.

The goal is not abstract “clean code”.
The goal is code that is fast to read, easy to reason about, and shaped like intent.

This style standard is mainly about readability, cleanliness, editor navigation, and implemented-pseudocode fit.
It should influence ratings like `Code quality`, `Self-documenting`, and `Pseudocode clarity`.
It should not be treated as the main source for runtime safety or stability scoring.

This style is written for **RAD development**.
That means:
- direct implementation is often better than early abstraction
- one file doing a lot of directly related work is acceptable
- helper extraction is selective, not automatic
- descriptive `#region`s are a valid readability tool in larger mechanic files
- method-local `#region`s may also be used as **behaviour-step regions** when a method has several meaningful steps
- the fold structure should optimise **editor navigation and skim-reading first**, not formal prose purity

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

## Design Bias

### 1. Treat code as something read in the IDE, not just in review
The code should still make sense when partially folded.

Preferred skim states:
- **fully expanded**: exact implementation
- **partially folded**: rough method story
- **mostly folded**: class concern map

This means region labels do not need to be perfect English.
They do need to make the fold view useful.

### 2. Optimise for local mechanic language
Prefer wording that fits the actual mechanic and the surrounding codebase.

Good labels may use:
- local domain nouns
- concrete state names
- concrete method or field names
- rough English when it still scans clearly

Examples:
- `Default moisture`
- `Client _tintRenderer`
- `Server OnEvery12Seconds`
- `Update neighbours`
- `Update _inventory`
- `Get Environment`
- `Make roomLabel`
- `Write CompostingStatus`
- `Materials`

Do not force every region title into formal command language when a shorter project-shaped label is easier to skim.

### 3. Prefer truthful labels over polished labels
A label should tell the reader what block they are about to unfold.
That matters more than whether the wording is elegant.

Good:
- `Get MinMax`
- `Browns`
- `Nutrition`
- `Inoculum`
- `Debug health`
- `Debug`

Bad:
- labels that sound polished but hide what the code really does
- labels so generic that they stop helping navigation
- labels that are longer than the block they describe

### 4. Regions are allowed to be a little rough when they still carry the story
This style does **not** require every region to read like final documentation prose.

It is acceptable for a region name to be:
- short
- slightly rough
- mixed with project-specific symbols
- closer to prompt language than to textbook naming

That is valid when it makes the file easier to work in.

---

## What this means in practice

### 5. Top-level methods should read like process steps
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

### 6. Prefer readable inline logic when it is already clear
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

### 7. Use helpers when the name adds real value
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

### 8. Prefer good variable names over explanatory prose
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

### 9. Use methods as sentence fragments
Method names should sound like actions or decisions:
- `CanAcceptFrom`
- `TryRestoreAeration`
- `GetCheapestNutritionCategory`
- `ConsumeAvailableFuel`
- `ShouldDiscardLeftovers`
- `DropRecoveredCompost`

That lets the caller read like rough English.

### 10. Prefer explicit control flow
Use readable `if`, `foreach`, and early returns.
Do not compress meaningful logic into dense expressions just because it is shorter.

### 11. Prefer vertical scanning
Where it fits the codebase, line up compound conditions so they scan cleanly:

```csharp
if (world.Side != EnumAppSide.Server
||  blockSel is null
||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
   )
    return false;
```

This style is good when it makes the logic feel like stacked reasons, not a dense sentence.

### 12. Use comments sparingly
Do not write comments that just restate obvious code.

Good comment use:
- non-obvious domain rules
- invariants
- engine quirks
- intentionally weird behaviour that might be “fixed” by mistake
- communication notes requested by the user

---

## Region Use

### 13. Use descriptive `#region`s at two levels
When one file owns several directly related concerns, keep it navigable.

Use file-level `#region`s for major concern grouping, for example:
- `StoredState`
- `HeatSource`
- `TintRendering`
- `Inventory`
- `Lifecycle`
- `Environment`
- `BlockInfo`
- `Persistence`

Use method-level `#region`s when they improve skim-reading of a medium or large method.
Do not avoid them just because the method is not huge.

Preferred remedy order:
1. improve names
2. add or improve descriptive `#region`s
3. improve local method flow
4. split files later if needed

A large file should not fail just for being large.
It should fail when the reader cannot quickly find the concern they need.

### 14. Treat method-local `#region`s as fold-backed pseudocode
Inside a longer method, a `#region` should mark one useful step in the method story.

Good region use:
- one guard block
- one setup block
- one derived-value block
- one mutation block
- one write/output block
- one finish block

Also valid:
- a short loop-body step when it materially helps skim-reading
- a short sub-block inside a long method when it makes the fold view more truthful

Bad region use:
- wrapping arbitrary lines that do not form a real step
- creating folds so tiny they clutter more than they help
- forcing formal structure where the method is already clearer without it

A method should be skimmable like:
- `Default moisture`
- `Client _tintRenderer`
- `Server OnEvery12Seconds`
- `Update neighbours`
- `Update _inventory`
- `Get Environment`
- `Make roomLabel`
- `Materials`

### 15. Region names should aim for scan utility, not documentation prose
Prefer region titles that make sense in the fold gutter.

Prefer:
- `Default moisture`
- `Client _tintRenderer`
- `Server OnEvery12Seconds`
- `Get Environment`
- `Make roomLabel`
- `Write CompostingStatus`
- `Debug health`
- `Rates`
- `Materials`
- `Get MinMax`

Still avoid:
- titles that only describe syntax
- titles that are misleading about what the block does
- extremely long natural-language descriptions that make folding noisy

This style no longer requires every region title to be formal command language like:
- `Require nutrition props`
- `Resolve room label`
- `Apply nutrition gain`

That form is still valid.
It is no longer the only preferred form.

### 16. A region may be local, concrete, and symbol-aware
It is acceptable for region names to reference:
- a field
- a method name
- a local output label
- a domain concept
- a concrete write target

Examples:
- `Client _tintRenderer`
- `Update _inventory`
- `result += neighbours.GetHeatStrength()`
- `Write CompostingStatus`

Use this when it improves local navigation.
Do not use it when it makes the label more confusing than the unfolded code.

### 17. Not every important line needs its own region
The aim is a readable fold outline, not maximum segmentation.

Add a method-local region when it improves one of these:
- navigation
- skim-reading
- future prompting
- implementation handoff
- collaboration against a rough skeleton

Do not add a region just because a line is important.

### 18. Use regions to make IDE folding become a design outline
In the editor, file regions and method-local regions should let the user skim the code as if it were a design note.

That means the fold labels should tell a truthful story of the code.
If the fold labels are useful in your IDE workflow, the pseudocode layer is doing its job.

The ideal is:
- fully expanded: exact code
- partially folded: rough implementation outline
- mostly folded: concern map of the class

### 19. Region-backed pseudocode is a valid collaboration format
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

A user may also provide a rougher shape like:

```csharp
Initialize()
{
    #region Default moisture
    #endregion

    #region Client _tintRenderer
    #endregion

    #region Server OnEvery12Seconds
    #endregion
}
```

That is also a valid implementation handoff shape.

When working from this kind of skeleton:
- preserve the region order unless there is a real correctness issue
- fill each region with the narrowest logic that matches the heading
- tighten obviously broken wording only when it improves scan value
- do not silently replace the structure with a totally different abstraction unless clearly beneficial
- mention when a region is missing a required step or mixes multiple steps

### 20. Prefer one clear level of intent per method
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

### 5. Tune the labels for the user’s fold workflow
Prefer the shortest truthful labels that make the fold view useful.
Do not over-polish them into abstract prose unless that clearly helps.

### 6. Comment only where naming cannot carry intent
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
- prefer fold usefulness over region-name elegance

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
- region names should optimise fold readability, not polished prose
- short rough labels are valid when they are truthful and useful
- labels may use local mechanic language, symbols, and concrete write targets
- when given a region skeleton, preserve it and implement to that structure where practical
- ask early when missing details would materially change the answer
- if a best-effort answer is still useful, give the narrowest useful answer and state assumptions plainly
