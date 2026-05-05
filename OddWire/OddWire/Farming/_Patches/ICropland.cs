using Vintagestory.API.Common;

namespace OddWire.GameContent;

public interface ICropland
{
    bool TryPlant(Block cropBlock, ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel);
    ItemStack[] GetDrops(ItemStack[] drops);
}
