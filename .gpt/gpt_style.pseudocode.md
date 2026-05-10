This gpt_style..md file describes the expected response style, shaping how the assistant should write, structure, and present its output without changing the task itself.

# Applied Pseudocode

## Purpose
Write code in a style that reads like practical pseudocode implemented directly in C#.

The goal is not abstract "clean code".
The goal is code that is fast to read, easy to reason about, and shaped like intent.

This style is written for **RAD development**.
That means:
- direct implementation is often better than early abstraction
- one file doing a lot of directly related work is acceptable
- helper extraction is selective, not automatic
- `#region`s are a translation tool — use them only when the code needs translating
- method-local `#region`s may be used as behaviour-step regions when a method has several meaningful steps that the code itself does not already name

---

## Core Style Rule
The code should read like:

**check -> decide -> do -> return**

Not like:

**clever expression -> hidden behaviour -> comment explaining it afterwards**

A reader should be able to skim:
- file regions for major concern grouping
- method names for behaviour grouping
- method-local regions for step grouping, only where the code itself does not already tell the story
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

When method names already tell the story, the method body IS the pseudocode. Do not wrap self-describing calls in regions just to create folds.

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
- `x`, `val`, `obj`, `tmp`, `result2`
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

### 6. Prefer explicit control flow — vertical chains are valid pseudocode
Use readable `if`, `foreach`, and early returns.
Do not compress meaningful logic into dense expressions just because it is shorter.

**Vertical `||` / `&&` chains with one condition per line are explicit, not compressed.**
Each line is one story beat. The chain as a whole states one outcome.

```csharp
if (currentTotalHours < TotalHoursForNextStage
||  moisture01 < 0.1f
|| !TryGrowCrop(currentTotalHours, world, host, growthRate, out consumedNutrient, out consumedAmount)
    )
    return false;
```

This reads as: not ready yet / too dry / grow failed → return false.
That is a story, not a compression. Do not break it into separate guards unless the steps have meaningfully different outcomes or side effects.

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
- invariants — especially firing-order dependencies
- engine quirks
- intentionally weird behaviour that might be "fixed" by mistake
- communication notes requested by the user

When a comment is needed for an invariant, prefer it inline on the relevant line over a region wrapper:

```csharp
UpdateSupport(); // must precede base interval — sets totalHoursWaterRetention
baseInterval?.Invoke(hourInterval, conds, lightGrowthSpeedFactor, growthPaused);
```

### 9. Use descriptive `#region`s at two levels
When one file owns several directly related concerns, keep it navigable.

Use file-level `#region`s for major concern grouping, for example:
- `StoredState`
- `RateHelpers`
- `Harvest`
- `Input`
- `StateUpdates`
- `Persistence`

Use method-level `#region`s only when the method body needs a label the code itself does not already provide.

**Regions are a translator. When the code does not need translating, skip the region.**

Preferred remedy order:
1. improve names
2. add or improve descriptive `#region`s
3. improve local method flow
4. split files later if needed

A large file should not fail just for being large.
It should fail when the reader cannot quickly find the concern they need.

### 10. Method-local regions — use only when the code needs a translator

When a method's calls are already self-describing, the method body IS the pseudocode. Adding regions produces labels that say the same thing as the lines they wrap — noise with a token cost.

```csharp
// Self-describing — no regions needed
UpdateSupport(); // must precede base interval — sets totalHoursWaterRetention
baseInterval?.Invoke(hourInterval, conds, lightGrowthSpeedFactor, growthPaused);
_crop.CheckDamage(conds, Api.World);
TickCrop(hourInterval, lightGrowthSpeedFactor, growthPaused);
```

Use a method-local region when:
- the body contains setup or logic that does not name itself
- the region label would say something the lines cannot
- the method is long enough that skim-reading needs anchors

Do not use a method-local region when:
- the body is one or two self-describing calls
- the label would be a restatement of the code
- adding it increases region count without adding story

**Prefer fewer, meaningful regions over many fine-grained ones.**
Region count is a cost — each one adds noise to the fold map and tokens to context. Earn each region.

### 11. Region labels should read like collapsed code
Region labels are the pseudocode layer. They should read like the one-liner you would write if the body did not exist.

Prefer code-shaped labels:
- `#region if (!CanPlow(targetBlock)) return`
- `#region supportBlock = world.GetBlock(supportPos)`
- `#region ResolveCurrentNutrients(targetBlock / supportBlock)`
- `#region plowlandBlock = GetBlock($"plowland-{moistKey}-{fertilityCode}")`
- `#region SetBlock(plowlandBlock, targetPos).Initialise()`
- `#region if(byPlayer is EntityPlayer) slot.DamageItem()`

Prose labels are acceptable for decision steps where no single line captures the outcome:
- `#region Lose fertility when underfed`
- `#region Gain fertility when overfed`

Avoid labels that describe fragments or syntax without capturing the outcome:
- `#region Room,`
- `#region A cost per input,`
- `#region And return true out acceptedstackConsumedQty`

The test: if you fold the region shut, does the label alone tell you what pseudocode line sits there?

### 12. Embedded getting-there logic in guard regions is acceptable
When setup code exists only to produce a value that determines whether to proceed, embedding it in the guard region is acceptable. The label captures the outcome, not every step to get there. This keeps region count low without losing the story.

```csharp
#region if(!GetCrop || currentStage >= GrowthStages) return false
Block block = GetCrop(world);
if (block is null)
    return false;

int currentStage = GetCropStage(block);
if (currentStage >= block.CropProps.GrowthStages)
    return false;

int nextStage = currentStage + 1;
Block nextBlock = world.GetBlock(block.CodeWithParts("" + nextStage));
if (nextBlock is null)
    return false;
#endregion
```

`nextStage` and `nextBlock` are getting-there. Their only job is to fail or feed the next region.
A separate region for them adds a label that says less than the lines already do.

### 13. Use regions to make IDE folding become a design outline
In the editor, file regions and method-local regions should let the user skim the code as if it were a design note.

The fold labels should tell a truthful story of the code.
If the fold labels are weak, the pseudocode layer is weak even if the implementation is correct.

The ideal is:
- fully expanded: exact code
- partially folded: exact algorithm outline in collapsed-code shorthand
- mostly folded: concern map of the class

### 14. Region-backed pseudocode is a valid collaboration format
A user may provide a skeleton like:

```csharp
TryAddNewResource()
{
    #region if(!HasRoom) return false
    #endregion

    #region if(!ValidInput) return false
    #endregion

    #region resultNutrients = ResolveConversion(input)
    #endregion

    #region ApplyMutation(resultNutrients)
    #endregion

    #region return acceptedQty
    #endregion
}
```

That is a valid implementation handoff shape.

When working from this kind of skeleton:
- preserve the region order unless there is a real correctness issue
- fill each region with the narrowest logic that matches the heading
- tighten region names to collapsed-code style if needed, but keep the original intent
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

### 4. Ask: does the method need translating?
If method names already tell the story, leave it flat.
If the body has unlabelled setup or logic that does not name itself, add region labels in collapsed-code style.
Prefer fewer regions. Earn each one.

### 5. Comment only where naming cannot carry intent
Use comments for:
- domain rules
- invariants and firing-order dependencies
- engine quirks
- intentionally weird behaviour

---

## Preferred Engineering Constraints

When writing code in this language:
- preserve surrounding style and formatting
- do not refactor unrelated code
- prefer narrow changes
- avoid generic "utility" extraction unless it removes real duplication
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

I want the code to read like collapsed pseudocode while still being real implementation code.

Rules:
- make top-level methods read like process steps
- prefer readable inline logic when it is already clear
- use helper methods only when the name adds real value
- use variable names that carry meaning
- keep one clear level of intent per method where practical
- vertical `||` / `&&` chains with one condition per line are explicit pseudocode — do not break them into separate guards unless outcomes differ
- regions are a translator — skip them when the code already names itself
- use collapsed-code region labels: `#region if(!CanPlow) return` not `#region Validate plow target`
- embed getting-there logic in the guard region that depends on it — keep region count low
- prefer fewer, meaningful regions over many fine-grained ones
- when given a region skeleton, preserve it and implement to that structure where practical
- invariants and firing-order dependencies go in inline comments, not region wrappers
- ask early when missing details would materially change the answer
- if a best-effort answer is still useful, give the narrowest useful answer and state assumptions plainly