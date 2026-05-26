using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using HarmonyLib;

namespace OddWire.Patches;

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetMergableQuantity))]
public static class CollectibleObject_GetMergableQuantity_HeldBagAcceptsItems_Patch
{
    static bool Prefix(
        ItemStack sinkStack,
        ItemStack sourceStack,
        EnumMergePriority priority,
        ref int __result,
        CollectibleObject __instance)
    {
        if (priority != EnumMergePriority.DirectMerge
        ||  sourceStack is null
            )
             return true;
        
        var bag = __instance.GetCollectibleInterface<IHeldBag>();
        if (bag == null
        ||  bag.GetQuantitySlots(sinkStack) >= 4
            ) return true;
        
        __result = sourceStack.StackSize;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.TryMergeStacks))]
public static class CollectibleObject_TryMergeStacks_HeldBagAcceptsItems_Patch
{
    static bool Prefix(ItemStackMergeOperation op, CollectibleObject __instance)
    {
        var sourceStack = op.SourceSlot.Itemstack;
        if (op.CurrentPriority != EnumMergePriority.DirectMerge
        ||  sourceStack == null
        ||  op.SinkSlot is ItemSlotBagContent
            )
            return true;

        var bag = __instance.GetCollectibleInterface<IHeldBag>();
        if (bag == null
        ||  bag.GetQuantitySlots(op.SinkSlot.Itemstack) >= 4
            )
            return true;
        
        var bagstack = op.SinkSlot.Itemstack;
        var slots = bag.GetOrCreateSlots(bagstack, op.SinkSlot.Inventory, 0, op.World);

        int remaining = sourceStack.StackSize;
        foreach (var slot in slots)
        {
            if (remaining <= 0
            ||  slot?.CanHold(op.SourceSlot) != true
                )
                continue;

            if (slot.Empty)
            {
                if (op.World.Side == EnumAppSide.Server)
                {
                    slot.Itemstack = sourceStack.Clone();
                    slot.Itemstack.StackSize = remaining;
                    bag.Store(bagstack, slot);
                }
                remaining = 0;
            }
            else if (slot.Itemstack.Equals(op.World, sourceStack, GlobalConstants.IgnoredStackAttributes))
            {
                int room = slot.Itemstack.Collectible.MaxStackSize - slot.Itemstack.StackSize;
                int moveQty = Math.Min(room, remaining);
                if (moveQty <= 0)
                    continue;

                if (op.World.Side == EnumAppSide.Server)
                {
                    slot.Itemstack.StackSize += moveQty;
                    bag.Store(bagstack, slot);
                }
                remaining -= moveQty;
            }
        }

        int moved = sourceStack.StackSize - remaining;
        if (moved > 0)
        {
            op.MovedQuantity = moved;
            if (op.World.Side == EnumAppSide.Server)
            {
                sourceStack.StackSize -= moved;
                if (sourceStack.StackSize <= 0)
                    op.SourceSlot.Itemstack = null;

                op.SourceSlot.MarkDirty();
                op.SinkSlot.MarkDirty();
            }
        }

        return false;
    }
}