using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;
public class BlockCompostpile : Block
{
    #region Interaction
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        #region Require target compostpile
        if (blockSel is null
        ||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
           )
            return false;
        #endregion

        #region Require held item
        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot?.Empty != false)
            return false;
        #endregion

        #region Try add held stack
        if(!be.TryAdd(slot, out int accepted)
        ||  accepted < 1
           )
            return false;
        #endregion

        #region Consume accepted input
        slot.TakeOut(accepted);
        slot.MarkDirty();
        return true;
        #endregion
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        #region Fall back to normal held-block handling
        if (blockSel is null
        ||  byEntity.Controls.ShiftKey
        ||  byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
            )
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        #endregion

        #region Try add held block before placement
        if (be.TryAdd(slot, out int accepted)
        &&  accepted > 0
           )
        {
            #region Consume accepted input on server
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                slot.TakeOut(accepted);
                slot.MarkDirty();
            }
            #endregion

            #region Prevent default block placement
            handling = EnumHandHandling.PreventDefault;
            return;
            #endregion
        }
        #endregion

        #region Fall back when input was rejected
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        #endregion
    }
    #endregion

    #region NeighbourUpdates
    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);

        #region Mark compostpile neighbour scan dirty
        if (world.Side == EnumAppSide.Server
        &&  world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCompostpile be
            )
            be.NeighboursDirty = true;
        #endregion
    }
    #endregion

    #region BreakHandling
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        #region Fall back for non-compostpile blocks
        if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCompostpile be)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }
        #endregion

        #region Let server own harvest resolution
        if (world.Side != EnumAppSide.Server)
            return; // Let the server decide post-harvest removal. Client state here is still pre-harvest.
        #endregion

        #region Harvest before breaking block
        be.Harvest(dropQuantityMultiplier);
        if (be.CanHarvest())
            return;
        #endregion

        #region Break empty pile block
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        #endregion
    }
    #endregion
}
