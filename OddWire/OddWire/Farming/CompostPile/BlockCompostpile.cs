using Vintagestory.API.Common;

namespace OddWire.GameContent
{
    public class BlockCompostPile : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel is null
            ||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostPile be
                )
                return false;

            var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot?.Empty != false)
                return false;
            
            if (!be.TryAdd(slot, out int accepted)
            ||  accepted < 1
                )
                return false;

            slot.TakeOut(accepted);
            slot.MarkDirty();
            return true;
        }
    }
}
