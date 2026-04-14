using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using HarmonyLib;
using OddWire.GameContent;

namespace OddWire.Patches;

[HarmonyPatch(typeof(BlockWateringCan), "OnHeldInteractStep")]
public static class BlockWateringCan_OnHeldInteractStep_Patch
{
    public static void Prefix(ItemSlot slot, ref float __state) =>
        __state = slot?.Itemstack?.TempAttributes?.GetFloat("secondsUsed") ?? 0f;

    public static void Postfix
        (bool __result
        ,float __state
        ,float secondsUsed
        ,ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        )
    {
        #region Validate
        if(!__result
        ||  byEntity?.World?.Side != EnumAppSide.Server
        ||  blockSel is null
        ||  slot?.Itemstack is null
        ||  slot.Itemstack.TempAttributes.GetInt("refilled") > 0
           )
            return;
        
        float dt = secondsUsed - __state;
        if (dt <= 0f)
            return;
        #endregion

        #region Resolve watered block position
        IWorldAccessor world = byEntity.World;
        BlockPos targetPos = blockSel.Position;

        Block block = world.BlockAccessor.GetBlock(blockSel.Position);
        if (block.CollisionBoxes is null
        ||  block.CollisionBoxes.Length == 0
            )
        {
            block = world.BlockAccessor.GetBlock(blockSel.Position, BlockLayersAccess.Fluid);
            if(!block.IsLiquid()
            && (block.CollisionBoxes is null
            ||  block.CollisionBoxes.Length == 0
               ))
                targetPos = targetPos.DownCopy();
        }
        #endregion

        #region IWaterable.Water()
        IWaterable? waterable = world.BlockAccessor
            .GetBlock(targetPos)
           ?.GetInterface<IWaterable>(world, targetPos);
        if (waterable is not null)
            waterable.Water(dt);
        
        #endregion
    }
}
