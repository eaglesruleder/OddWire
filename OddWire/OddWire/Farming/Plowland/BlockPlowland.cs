using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public class BlockPlowland : Block
{
    public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type) =>
        facing == BlockFacing.UP ? 0 : 3;

    public override bool SideIsSolid(BlockPos pos, int faceIndex) =>
        faceIndex == BlockFacing.indexDOWN;
    
    public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
    {
        if (blockFace.IsHorizontal)
            return false;
        
        if (blockFace == BlockFacing.UP
        && (block is BlockCrop
        ||  block is BlockDeadCrop
           ))
            return true;

        return base.CanAttachBlockAt(world, block, pos, blockFace, attachmentArea);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        #region Forward held fertilizer into BE
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityPlowland be)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (be.TryFertilize(slot, out int consumed)
            &&  consumed > 0
                )
            {
                slot.TakeOut(consumed);
                slot.MarkDirty();
                return true;
            }
        }
        #endregion

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
