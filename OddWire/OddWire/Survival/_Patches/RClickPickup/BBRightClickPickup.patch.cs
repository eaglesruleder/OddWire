using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using HarmonyLib;

namespace OddWire.Patches;

[HarmonyPatch(typeof(BlockBehaviorRightClickPickup), nameof(BlockBehaviorRightClickPickup.OnBlockInteractStart))]
public static class BBRightClickPickup_BasketIntercept_Patch
{
    static bool Prefix(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        ref EnumHandling handling,
        ref bool __result,
        BlockBehaviorRightClickPickup __instance)
    {
        #region if(!bag.IsHandheld) return true
        var activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot?.Itemstack is null)
            return true;

        var bag = activeSlot.Itemstack.Collectible.GetCollectibleInterface<IHeldBag>();
        if (bag?.IsHandheld(activeSlot.Itemstack) != true)
            return true;
        #endregion
        
        if (world.Side != EnumAppSide.Server)
        {
            __result = true;
            handling = EnumHandling.PreventDefault;
            return false;
        }

        #region dropStacks = dropsPickupMode ? block.GetDrops() : block.OnPickBlock();
        var block = world.BlockAccessor.GetBlock(blockSel.Position);
        var dropsPickupMode = Traverse.Create(__instance).Field<bool>("dropsPickupMode").Value;

        ItemStack[] dropStacks;
        if (dropsPickupMode)
        {
            dropStacks = block.GetDrops(world, blockSel.Position, byPlayer, 1f);
            if (dropStacks == null || dropStacks.Length == 0)
                return true;
        }
        else
        {
            var single = block.OnPickBlock(world, blockSel.Position);
            if (single == null)
                return true;
            dropStacks = new[] { single };
        }
        #endregion
        
        var bagstack = activeSlot.Itemstack;
        var slots = bag.GetOrCreateSlots(bagstack, activeSlot.Inventory, 0, world);
        
        foreach (var dropStack in dropStacks)
        {
            foreach (var bagSlot in slots)
            {
                if (dropStack.StackSize <= 0)
                    break;

                if (bagSlot.Empty)
                {
                    bagSlot.Itemstack = dropStack.Clone();
                    bagSlot.Itemstack.StackSize = Math.Min(dropStack.StackSize, bagSlot.Itemstack.Collectible.MaxStackSize);
                    dropStack.StackSize -= bagSlot.Itemstack.StackSize;
                    bag.Store(bagstack, bagSlot);
                    activeSlot.MarkDirty();
                }
                else if (bagSlot.Itemstack.Equals(world, dropStack, GlobalConstants.IgnoredStackAttributes))
                {
                    int room = bagSlot.Itemstack.Collectible.MaxStackSize - bagSlot.Itemstack.StackSize;
                    int moveQty = Math.Min(room, dropStack.StackSize);
                    if (moveQty <= 0)
                        continue;

                    bagSlot.Itemstack.StackSize += moveQty;
                    dropStack.StackSize -= moveQty;
                    bag.Store(bagstack, bagSlot);
                    activeSlot.MarkDirty();
                }
            }

            if (dropStack.StackSize > 0
            && !byPlayer.InventoryManager.TryGiveItemstack(dropStack, true)
                )
                world.SpawnItemEntity(dropStack, blockSel.Position.ToVec3d().AddCopy(0.5, 0.1, 0.5));
        }
        
        world.BlockAccessor.SetBlock(0, blockSel.Position);
        world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);
        
        __result = true;
        handling = EnumHandling.PreventDefault;
        return false;
    }
}