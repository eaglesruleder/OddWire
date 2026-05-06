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
        &&  block is BlockCrop or BlockDeadCrop
           )
            return true;

        return base.CanAttachBlockAt(world, block, pos, blockFace, attachmentArea);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server)
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityPlowland be
        &&  be.OnBlockInteract(byPlayer)
           )
            return true;

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
