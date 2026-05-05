using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OddWire.GameContent;

namespace OddWire.Patches;

/// <summary>
/// Patches BlockCrop methods that hard-reference BlockEntityFarmland or BlockFarmland.
/// Each guard: (is not ICropland || is BlockEntityFarmland) lets vanilla handle its own path.
/// BlockEntitySoilNutrition cast used where ICropland is not needed (OnBlockBroken, OnBlockInteractStart).
/// </summary>

// ── Suppress wild random ticks on plowland ────────────────────────────────────

[HarmonyPatch(typeof(BlockCrop), "IsNotOnFarmland")]
public static class BlockCrop_IsNotOnFarmland_Patch
{
    public static void Postfix(IWorldAccessor world, BlockPos pos, ref bool __result)
    {
        if (!__result) return; // already on vanilla farmland — nothing to do

        // Treat any ICropland as farmland — suppress wild tick growth
        if (world.BlockAccessor.GetBlockEntity(pos.DownCopy()) is ICropland)
            __result = false;
    }
}

// ── Fertiliser via right-clicking crop ───────────────────────────────────────

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.OnBlockInteractStart))]
public static class BlockCrop_OnBlockInteractStart_Patch
{
    public static bool Prefix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref bool __result)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) is not BlockEntitySoilNutrition be
        ||  be is BlockEntityFarmland // vanilla handles its own
           )
            return true;

        if (!be.OnBlockInteract(byPlayer))
            return true;

        __result = true;
        return false;
    }
}

// ── OnCropBlockBroken on harvest ─────────────────────────────────────────────

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.OnBlockBroken))]
public static class BlockCrop_OnBlockBroken_Patch
{
    public static void Postfix(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        // crop is already removed at pos — plowland below is still there
        if (world.BlockAccessor.GetBlockEntity(pos.DownCopy()) is not BlockEntitySoilNutrition be
        ||  be is BlockEntityFarmland // vanilla already called OnCropBlockBroken
           )
            return;

        be.OnCropBlockBroken();
    }
}

// ── Drop handling ─────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.GetDrops))]
public static class BlockCrop_GetDrops_Patch
{
    private static readonly MethodInfo BaseGetDrops =
        AccessTools.Method(typeof(Block), nameof(Block.GetDrops));

    public static bool Prefix
        (BlockCrop       __instance
        ,IWorldAccessor  world
        ,BlockPos        pos
        ,IPlayer         byPlayer
        ,float           dropQuantityMultiplier
        ,ref ItemStack[] __result
        )
    {
        if (world.BlockAccessor.GetBlockEntity(pos.DownCopy()) is not ICropland target
        ||  target is BlockEntityFarmland
           )
            return true;

        __instance.SplitDropStacks = false;

        ItemStack[] baseDrops = (ItemStack[])BaseGetDrops
            .Invoke(__instance, new object[] { world, pos, byPlayer, dropQuantityMultiplier });

        __result = target.GetDrops(baseDrops);
        return false;
    }
}

// ── Block info when hovering a crop ──────────────────────────────────────────

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.GetPlacedBlockInfo))]
public static class BlockCrop_GetPlacedBlockInfo_Patch
{
    public static bool Prefix(IWorldAccessor world, BlockPos pos, IPlayer forPlayer, ref string __result)
    {
        Block block = world.BlockAccessor.GetBlock(pos.DownCopy());
        if (block is not BlockPlowland)
            return true;

        __result = block.GetPlacedBlockInfo(world, pos.DownCopy(), forPlayer);
        return false;
    }
}

// ── Crop vertical offset on plowland (cosmetic) ───────────────────────────────

[HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.AdjustYPosition))]
public static class BlockCrop_AdjustYPosition_Patch
{
    private static readonly FieldInfo OffsetField =
        AccessTools.Field(typeof(BlockCrop), "onFarmlandVerticalOffset");

    public static void Postfix(BlockCrop __instance, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d, ref float __result)
    {
        if (__result != 0f) return; // vanilla already applied an offset

        Block below = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
        if (below is BlockPlowland)
            __result = (float)OffsetField.GetValue(__instance);
    }
}
