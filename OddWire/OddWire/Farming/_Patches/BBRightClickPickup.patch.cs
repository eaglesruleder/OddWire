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
        #region if(!bag || bag.QtySlots >= 4) return true
        var activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot?.Itemstack is null)
            return true;

        var bag = activeSlot.Itemstack.Collectible.GetCollectibleInterface<IHeldBag>();
        if (bag == null
        ||  bag.GetQuantitySlots(activeSlot.Itemstack) >= 4
            )
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
            #region if(bagSlots.Any(Accept(dropStock))) bagSlot.Add(dropStack);
            bool accepted = false;
            foreach (var bagSlot in slots)
                if (bagSlot.Empty
                || (bagSlot.Itemstack.Equals(world, dropStack, GlobalConstants.IgnoredStackAttributes)
                &&  bagSlot.Itemstack.StackSize < bagSlot.Itemstack.Collectible.MaxStackSize
                   ))
                {
                    if(bagSlot.Empty)
                        bagSlot.Itemstack = dropStack.Clone();
                    else
                    {
                        bagSlot.Itemstack.StackSize++;
                    }
                    
                    bag.Store(bagstack, bagSlot);
                    activeSlot.MarkDirty();
                    accepted = true;
                    break;
                }
            #endregion
            
            if (!accepted
            &&  !byPlayer.InventoryManager.TryGiveItemstack(dropStack, true)
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