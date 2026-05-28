using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using HarmonyLib;
using OddWire.GameContent;
using Vintagestory.API.Util;

namespace OddWire.Patches;

// Intent: This patch file effectively rewrites ItemPlantableSeed
// Outcome: Functionally similar, delegating to Farmland ?? ICropland

[HarmonyPatch(typeof(ItemPlantableSeed), nameof(ItemPlantableSeed.OnLoaded))]
public static class ItemPlantableSeed_OnLoaded_Overwrite_Patch
{
    private static readonly FieldInfo InteractionsField =
        AccessTools.Field(typeof(ItemPlantableSeed), "interactions");

    public static bool Prefix(ICoreAPI api, ItemPlantableSeed __instance)
    {
        if (api.Side != EnumAppSide.Client)
            return false;

        InteractionsField.SetValue(__instance, ObjectCacheUtil.GetOrCreate(api, "seedInteractions", () =>
        {
            List<ItemStack> stacks = new();
            foreach (Block block in api.World.Blocks)
            {
                if (block.Code is null
                ||  block.EntityClass is null
                    )
                    continue;

                Type? beType = api.World.ClassRegistry.GetBlockEntity(block.EntityClass);
                if (beType == typeof(BlockEntityFarmland)
                ||  beType?.IsAssignableTo(typeof(ICropland)) == true
                   )
                    stacks.Add(new ItemStack(block));
            }

            return new WorldInteraction[]
                {new()
                    {ActionLangCode = "heldhelp-plant"
                    ,MouseButton    = EnumMouseButton.Right
                    ,Itemstacks     = stacks.ToArray()
                    }
                };
        }));

        return false;
    }
}

[HarmonyPatch(typeof(ItemPlantableSeed), nameof(ItemPlantableSeed.OnHeldInteractStart))]
public static class ItemPlantableSeed_OnHeldInteractStart_Overwrite_Patch
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldInteractStart))]
    public static void base_OnHeldInteractStart
        (ItemPlantableSeed instance
        ,ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel ,EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling
        ) => throw new NotImplementedException("Harmony reverse patch stub");
    
    public static bool Prefix
        (ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling
        ,ItemPlantableSeed __instance
        )
    {
        #region if(!blockSel || !cropType || BE is not BEFarmland or ICropland) return false
        string? cropType = itemslot.Itemstack?.Collectible?.Variant?["type"];
        if (blockSel is null
        ||  cropType is null
           )
        {
            base_OnHeldInteractStart(__instance, itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            return false;
        }
        
        Block b = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
        BlockEntity? be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
        if (be is not BlockEntityFarmland
        &&  be is not ICropland
           )
        {
            base_OnHeldInteractStart(__instance, itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            return false;
        }
        #endregion

        #region if(!cropBlock || !be.TryPlant()) return false;
        string plotType = be.Block.Code.FirstCodePart();
        Block? cropBlock = byEntity.World.GetBlock(__instance.CodeWithPath($"crop-{cropType}-{plotType}-1"));
        if (cropBlock is null)
            return false;
        
        bool planted =
            (be as BlockEntityFarmland)?.TryPlant(cropBlock, itemslot, byEntity, blockSel)
        ??  (be as ICropland)!.TryPlant(cropBlock, itemslot, byEntity, blockSel);
        if (!planted)
            return false;
        #endregion

        IPlayer? byPlayer = (byEntity as EntityPlayer)?.Player;
        byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), blockSel.Position, 0.4375, byPlayer);
        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

        if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
        {
            itemslot.TakeOut(1);
            itemslot.MarkDirty();
        }

        handHandling = EnumHandHandling.PreventDefault;
        return false;
    }
}

[HarmonyPatch(typeof(ItemPlantableSeed), nameof(ItemPlantableSeed.GetHeldItemInfo))]
public static class ItemPlantableSeed_GetHeldItemInfo_Overwrite_Patch
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(Item), nameof(Item.GetHeldItemInfo))]
    public static void base_GetHeldItemInfo
        (ItemPlantableSeed instance
        ,ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo
        ) => throw new NotImplementedException("Harmony reverse patch stub");

    public static bool Prefix
        (ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo
        ,ItemPlantableSeed __instance
        )
    {
        base_GetHeldItemInfo(__instance, inSlot, dsc, world, withDebugInfo);
        
        #region cropProps = GetBlock("crop-{cropType}-farmland-1")?.CropProps ?? return false;
        string? cropType = inSlot.Itemstack?.Collectible?.Variant?["type"];
        if (cropType is null)
            return false;

        var cropProps = world.GetBlock(__instance.CodeWithPath($"crop-{cropType}-farmland-1"))?.CropProps;
        if (cropProps is null)
            return false;
        #endregion
        
        dsc.AppendLine(Lang.Get("soil-nutrition-requirement") + cropProps.RequiredNutrient);
        dsc.AppendLine(Lang.Get("soil-nutrition-consumption") + cropProps.NutrientConsumption);

        double totalDays = cropProps.TotalGrowthDays > 0
        ?   cropProps.TotalGrowthDays / 12
        :   cropProps.TotalGrowthMonths;
        totalDays *= world.Calendar.DaysPerMonth / world.Config.GetDecimal("cropGrowthRateMul", 1);
        dsc.AppendLine(Lang.Get("soil-growth-time") + " " + Lang.Get("count-days", Math.Round(totalDays, 1)));
        
        dsc.AppendLine(Lang.Get("crop-coldresistance", Math.Round(cropProps.ColdDamageBelow, 1)));
        dsc.AppendLine(Lang.Get("crop-heatresistance", Math.Round(cropProps.HeatDamageAbove, 1)));

        return false;
    }
}
