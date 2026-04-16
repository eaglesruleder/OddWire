using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;
public class BlockCompostpile : Block
{
    #region HeldAndBlockInput
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        #region Require compostpile target
        if (blockSel is null
        ||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
           )
            return false;
        #endregion

        #region Require held stack
        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot?.Empty != false)
            return false;
        #endregion
        
        #region Apply accepted held input
        if(!be.TryAdd(slot, out int accepted)
        ||  accepted < 1
           )
            return false;

        if (world.Side == EnumAppSide.Server)
        {
            slot.TakeOut(accepted);
            slot.MarkDirty();
        }

        return true;
        #endregion
    }
    
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        #region Require non-shift compostpile target
        if (blockSel is null
        ||  byEntity.Controls.ShiftKey
        ||  byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
            )
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        #endregion

        #region Consume held input before default block placement
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
        #endregion

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    #endregion

    #region NeighbourChangeHandling
    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);

        if (world.Side == EnumAppSide.Server
        &&  world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCompostpile be
            )
            be.NeighboursDirty = true;
    }

    #endregion

    #region BreakAndHarvest
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        #region Resolve compostpile block entity
        if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCompostpile be)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }
        #endregion

        #region Require server-side break authority
        if (world.Side != EnumAppSide.Server)
            return; // Let the server decide post-harvest removal. Client state here is still pre-harvest.
        #endregion

        #region Harvest before removing block
        be.Harvest(dropQuantityMultiplier);
        if (be.CanHarvest())
            return;
        #endregion

        #region Remove emptied compostpile block
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        #endregion
    }
    #endregion
}
