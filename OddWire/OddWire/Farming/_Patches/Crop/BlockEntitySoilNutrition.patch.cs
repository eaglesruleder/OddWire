using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using HarmonyLib;
using OddWire.GameContent;

namespace OddWire.Patches;

[HarmonyPatch(typeof(BlockEntitySoilNutrition), "WaterFarmland")]
public static class BlockEntitySoilNutrition_WaterFarmland_Patch
{
    public static void Postfix(BlockEntitySoilNutrition __instance, float dt, bool waterNeightbours)
    {
        if (!waterNeightbours)
            return;

        foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
        {
            BlockPos npos = __instance.Pos.AddCopy(facing);
            __instance.Api.World.BlockAccessor
                .GetBlock(npos)
               ?.GetInterface<IWaterable>(__instance.Api.World, npos)
               ?.Water(dt / 3f);
        }
    }
}
