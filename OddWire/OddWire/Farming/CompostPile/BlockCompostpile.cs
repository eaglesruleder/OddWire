using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;
public class BlockCompostpile : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server
        ||  blockSel is null
        ||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
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
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCompostpile be
        &&  be.CanHarvest(out int CompostpileQty, out int sourCompostQty, out int compostQty)
            )
        {
            if (world.Side == EnumAppSide.Server)
            {
                if(CompostpileQty > 0)
                    be.HarvestCompostpile(world.Rand.Next(CompostpileQty)+1, dropQuantityMultiplier);
                
                if(sourCompostQty > 0)
                    be.HarvestSourCompost(world.Rand.Next(sourCompostQty)+1, dropQuantityMultiplier);
                
                if(compostQty > 0)
                    be.HarvestCompost(world.Rand.Next(compostQty)+1, dropQuantityMultiplier);
                
                be.UpdateShapeStackSize();
            }
            return;
        }
        
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
