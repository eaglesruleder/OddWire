using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using OddWire.GameContent;

namespace OddWire.Patches;

// ── Seed planting on plowland ─────────────────────────────────────────────────

[HarmonyPatch(typeof(ItemPlantableSeed), "OnHeldInteractStart")]
public static class ItemPlantableSeed_OnHeldInteractStart_Patch
{
    public static bool Prefix
        (ItemPlantableSeed __instance
        ,ItemSlot          itemslot
        ,EntityAgent       byEntity
        ,BlockSelection    blockSel
        ,EntitySelection   entitySel
        ,bool              firstEvent
        ,ref EnumHandHandling handHandling
        )
    {
        #region Require ICropland non-vanilla target
        if (blockSel is null)
            return true;

        if (byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not ICropland target
        ||  target is BlockEntityFarmland
           )
            return true;
        #endregion

        #region Require valid crop
        string? croptype = itemslot.Itemstack?.Collectible?.Variant?["type"];
        if (croptype is null)
            return false;

        Block cropBlock = byEntity.World.GetBlock(__instance.CodeWithPath($"crop-{croptype}-1"));
        if (cropBlock is null)
            return false;
        #endregion

        #region Plant and consume
        IPlayer? byPlayer = (byEntity as EntityPlayer)?.Player;

        if (!target.TryPlant(cropBlock, itemslot, byEntity, blockSel))
            return false;

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

// ── Seed interaction hint includes plowland blocks ────────────────────────────

[HarmonyPatch(typeof(ItemPlantableSeed), nameof(ItemPlantableSeed.OnLoaded))]
public static class ItemPlantableSeed_OnLoaded_Patch
{
    public static void Postfix(ItemPlantableSeed __instance, ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Client) return;

        if (AccessTools.Field(typeof(ItemPlantableSeed), "interactions")
               .GetValue(__instance) is not WorldInteraction[] interactions
        ||  interactions.Length == 0
           )
            return;

        // Guard: shared cache — only add plowland stacks once
        if (interactions[0].Itemstacks != null)
        {
            foreach (ItemStack s in interactions[0].Itemstacks)
                if (s.Block is BlockPlowland) return;
        }

        List<ItemStack> extra = new();
        foreach (Block block in api.World.Blocks)
        {
            if (block?.Code is null || block.EntityClass is null) continue;
            if (api.World.ClassRegistry.GetBlockEntity(block.EntityClass) == typeof(BlockEntityPlowland))
                extra.Add(new ItemStack(block));
        }

        if (extra.Count == 0) return;
        
        List<ItemStack> merged = new(interactions[0].Itemstacks ?? []);
        merged.AddRange(extra);
        interactions[0].Itemstacks = merged.ToArray();
    }
}
