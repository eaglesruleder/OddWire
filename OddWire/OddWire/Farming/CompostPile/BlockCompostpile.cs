using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;
public class BlockCompostpile : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (blockSel is null
        ||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
           )
            return false;

        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot?.Empty != false)
            return false;
        
        if(!be.TryAdd(slot, out int accepted)
        ||  accepted < 1
           )
            return false;

        slot.TakeOut(accepted);
        slot.MarkDirty();
        return true;
    }
    
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (blockSel is null
        ||  byEntity.Controls.ShiftKey
        ||  byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
            )
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        if (be.TryAdd(slot, out int accepted)
        &&  accepted > 0
           )
        {
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                slot.TakeOut(accepted);
                slot.MarkDirty();
            }

            handling = EnumHandHandling.PreventDefault;
            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCompostpile be)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        if (world.Side != EnumAppSide.Server)
            return; // Let the server decide post-harvest removal. Client state here is still pre-harvest.

        be.Harvest(dropQuantityMultiplier);
        if (be.CanHarvest())
            return;

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
