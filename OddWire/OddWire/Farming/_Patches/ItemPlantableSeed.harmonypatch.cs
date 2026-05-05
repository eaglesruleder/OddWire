using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using HarmonyLib;
using OddWire.GameContent;
using Vintagestory.API.Client;

namespace OddWire.Patches;
[HarmonyPatch(typeof(ItemPlantableSeed), "OnHeldInteractStart")]
public static class ItemPlantableSeed_OnHeldInteractStart_Patch
{
    public static bool Prefix
        (ItemSlot        itemslot
        ,EntityAgent     byEntity
        ,BlockSelection  blockSel
        ,EntitySelection entitySel
        ,bool            firstEvent
        ,ref EnumHandHandling handHandling
        )
    {
        #region Require plantable non-vanilla target
        if (blockSel is null)
            return true;

        if (byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not ICropland target
        ||  target is BlockEntityFarmland
           )
            return true;
        #endregion

        #region Require valid crop
        string croptype = itemslot.Itemstack?.Collectible?.Variant?["type"];
        if (croptype is null)
            return false;

        Block cropBlock = byEntity.World.GetBlock(new AssetLocation($"game:crop-{croptype}-1"));
        if (cropBlock is null)
            return false;
        #endregion

        #region Plant and consume
        if (!target.TryPlant(cropBlock, itemslot, byEntity, blockSel))
            return false;

        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
        byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), blockSel.Position, 0.4375, byPlayer);
        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

        if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
        {
            itemslot.TakeOut(1);
            itemslot.MarkDirty();
        }

        handHandling = EnumHandHandling.PreventDefault;
        #endregion

        return false;
    }
}