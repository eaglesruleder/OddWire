using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;
public class BlockCompostPile : Block
{
    // ToDo: Runs client and server side. Fix?
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

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCompostPile be
        &&  be.CanHarvest(out int compostPileQty, out int compostQty)
            )
        {
            if (world.Side == EnumAppSide.Server)
            {
                if(compostPileQty > 0)
                    be.HarvestCompostPile(world.Rand.Next(compostPileQty)+1, dropQuantityMultiplier);
                
                if(compostQty > 0)
                    be.HarvestCompost(world.Rand.Next(compostQty)+1, dropQuantityMultiplier);
                
                be.UpdateShapeStackSize();
            }
            return;
        }
        
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
