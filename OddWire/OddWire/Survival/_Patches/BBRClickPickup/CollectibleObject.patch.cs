using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using HarmonyLib;

namespace OddWire.Patches;

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetMergableQuantity))]
public static class CollectibleObject_GetMergableQuantity_HeldBagAcceptsItems_Patch
{
    static bool Prefix
        (ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority
        ,ref int __result, CollectibleObject __instance
        )
    {
        if (priority != EnumMergePriority.DirectMerge
        ||  sourceStack is null
            )
            return true;
        
        var bag = __instance.GetCollectibleInterface<IHeldBag>();
        if (bag?.IsHandheld(sinkStack) != true)
            return true;
        
        __result = sourceStack.StackSize;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.TryMergeStacks))]
public static class CollectibleObject_TryMergeStacks_HeldBagAcceptsItems_Patch
{
    static bool Prefix
        (ItemStackMergeOperation op
        ,CollectibleObject __instance
        )
    {
        #region if(SinkSlot is ItemSlotBagContent || !bag.IsHandheld) return true;
        var sourceStack = op.SourceSlot.Itemstack;
        if (op.CurrentPriority != EnumMergePriority.DirectMerge
        ||  sourceStack == null
        ||  op.SinkSlot is ItemSlotBagContent
            )
            return true;

        var bag = __instance.GetCollectibleInterface<IHeldBag>();
        if (bag?.IsHandheld(op.SinkSlot.Itemstack) != true)
            return true;
        #endregion
        
        var bagstack = op.SinkSlot.Itemstack;
        var slots = bag.GetOrCreateSlots(bagstack, op.SinkSlot.Inventory, 0, op.World);

        int remaining = sourceStack.StackSize;
        foreach (var slot in slots)
        {
            if (remaining < 1
            ||  slot?.CanHold(op.SourceSlot) != true
                )
                continue;

            if (slot.Empty)
            #region bag.Store(sourceStack.Clone());
            {
                slot.Itemstack = sourceStack.Clone();
                slot.Itemstack.StackSize = remaining;
                bag.Store(bagstack, slot);
                remaining = 0;
            }
            #endregion
            else if (slot.Itemstack.Equals(op.World, sourceStack, GlobalConstants.IgnoredStackAttributes))
            #region slot.StackSize += Min(remaining, room)
            {
                int room = slot.Itemstack.Collectible.MaxStackSize - slot.Itemstack.StackSize;
                if (room <= 0)
                    continue;

                int moveQty = Math.Min(room, remaining);
                slot.Itemstack.StackSize += moveQty;
                bag.Store(bagstack, slot);
                remaining -= moveQty;
            }
            #endregion
        }

        int moved = sourceStack.StackSize - remaining;
        if (moved > 0)
        #region sourceStack.StackSize -= moved; SourceSlot/SinkSlot.MarkDirty()
        {
            op.MovedQuantity = moved;
            sourceStack.StackSize -= moved;
            if (sourceStack.StackSize <= 0)
                op.SourceSlot.Itemstack = null;

            op.SourceSlot.MarkDirty();
            op.SinkSlot.MarkDirty();
        }
        #endregion

        return false;
    }
}
