using Vintagestory.API.Common;

namespace OddWire.GameContent;

/// <summary>
/// End-to-end crop surface contract for farmland variants.
/// OnCropBlockBroken and OnBlockInteract are satisfied automatically
/// by BlockEntitySoilNutrition via inheritance — only TryPlant and
/// GetDrops require explicit implementation.
/// </summary>
public interface ICropland
{
    bool         TryPlant     (Block cropBlock, ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel);
    ItemStack[]  GetDrops     (ItemStack[] drops);
    void         OnCropBlockBroken ();              // satisfied free via BlockEntitySoilNutrition
    bool         OnBlockInteract   (IPlayer byPlayer); // satisfied free via BlockEntitySoilNutrition
}
