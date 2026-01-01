using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace OddWire.GameContent
{
    public class BlockFabricate : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            BlockEntityFabricate beFabricate = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFabricate;
            if (beFabricate != null && byPlayer?.InventoryManager?.ActiveHotbarSlot != null)
            {
                if (beFabricate.TryHandleFabricationInteraction(byPlayer, byPlayer.InventoryManager.ActiveHotbarSlot))
                {
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
