using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace YourMod
{
    [HarmonyPatch(typeof(BlockBehaviorRightClickPickup), nameof(BlockBehaviorRightClickPickup.OnBlockInteractStart))]
    static class BBRightClickPickup_BasketIntercept_Patch
    {
        static bool Prefix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (world.Side != EnumAppSide.Server)
                return true;

            #region if(bag && bag.QtySlots >= 4) return true;
            var activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot?.Itemstack is null)
                return true;

            var bag = activeSlot.Itemstack.Collectible.GetCollectibleInterface<IHeldBag>();
            if (bag == null
            ||  bag.GetQuantitySlots(activeSlot.Itemstack) >= 4
                )
                return true;
            #endregion
            
            var block = world.BlockAccessor.GetBlock(blockSel.Position);
            var dropStack = block.OnPickBlock(world, blockSel.Position);
            if (dropStack == null)
                return true;
            
            var bagstack = activeSlot.Itemstack;
            var slots = bag.GetOrCreateSlots(bagstack, activeSlot.Inventory, 0, world);

            bool accepted = false;
            foreach (var bagSlot in slots)
            {
                #region if (bagSlot.Empty || bagSlot.item.Equals(dropStack)) { bag.Store(bagstack, bagSlot); accepted = true; break; }
                if (bagSlot.Empty)
                {
                    bagSlot.Itemstack = dropStack.Clone();
                    bag.Store(bagstack, bagSlot);
                    accepted = true;
                    break;
                }
                
                if (bagSlot.Itemstack.Equals(world, dropStack, Vintagestory.API.Config.GlobalConstants.IgnoredStackAttributes)
                &&  bagSlot.Itemstack.StackSize < bagSlot.Itemstack.Collectible.MaxStackSize
                   )
                {
                    bagSlot.Itemstack.StackSize++;
                    bag.Store(bagstack, bagSlot);
                    accepted = true;
                    break;
                }
                #endregion
            }

            if (!accepted)
                return true;
            
            world.BlockAccessor.SetBlock(0, blockSel.Position);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);

            handling = EnumHandling.PreventDefault;
            return false;
        }
    }
}