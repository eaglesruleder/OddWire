using System.Reflection;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using HarmonyLib;
using OddWire.GameContent;

namespace OddWire.Patches;

[HarmonyPatch(typeof(BlockCrop), "IsNotOnFarmland")]
public static class BlockCrop_IsNotOnFarmland_OrIsICropland_Patch
{
    public static void Postfix(IWorldAccessor world, BlockPos pos, ref bool __result)
    {
        if (__result
        &&  world.BlockAccessor.GetBlockEntity(pos.DownCopy()) is ICropland
            )
            __result = false;
    }
}

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.OnBlockInteractStart))]
public static class BlockCrop_OnBlockInteractStart_CallsBESN_Patch
{
    public static bool Prefix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref bool __result)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) is BlockEntitySoilNutrition besn
        &&  besn is not BlockEntityFarmland // vanilla handled
        &&  besn.OnBlockInteract(byPlayer)
           )
        {
            __result = true;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.OnBlockBroken))]
public static class BlockCrop_OnBlockBroken_CallsBESN_Patch
{
    public static void Postfix(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        if (world.BlockAccessor.GetBlockEntity(pos.DownCopy()) is BlockEntitySoilNutrition besn
        &&  besn is not BlockEntityFarmland // vanilla handled
           )
            besn.OnCropBlockBroken();
    }
}

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.GetDrops))]
public static class BlockCrop_GetDrops_CallsICropland_Patch
{
    private static readonly MethodInfo BaseGetDrops =
        AccessTools.Method(typeof(Block), nameof(Block.GetDrops));

    public static bool Prefix
        (BlockCrop __instance
        ,IWorldAccessor world
        ,BlockPos pos
        ,IPlayer byPlayer
        ,float dropQuantityMultiplier
        ,ref ItemStack[] __result
        )
    {
        if (world.BlockAccessor.GetBlockEntity(pos.DownCopy()) is not ICropland cropland
        ||  cropland is BlockEntityFarmland // vanilla handled
           )
            return true;

        __instance.SplitDropStacks = false;

        ItemStack[] baseDrops = (ItemStack[])BaseGetDrops
            .Invoke(__instance, new object[] { world, pos, byPlayer, dropQuantityMultiplier });

        __result = cropland.GetDrops(baseDrops);
        return false;
    }
}

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.GetPlacedBlockInfo))]
public static class BlockCrop_GetPlacedBlockInfo_CallsBlockPlowland_Patch
{
    public static bool Prefix(IWorldAccessor world, BlockPos pos, IPlayer forPlayer, ref string __result)
    {
        Block downBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
        if (downBlock is BlockPlowland)
        {
            __result = downBlock.GetPlacedBlockInfo(world, pos.DownCopy(), forPlayer);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.AdjustYPosition))]
public static class BlockCrop_AdjustYPosition_Patch
{
    private static readonly FieldInfo OffsetField =
        AccessTools.Field(typeof(BlockCrop), "onFarmlandVerticalOffset");

    public static void Postfix(BlockCrop __instance, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d, ref float __result)
    {
        if (__result != 0f)
            return; // vanilla handled

        Block below = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
        if (below is BlockPlowland)
            __result = (float)OffsetField.GetValue(__instance);
    }
}
