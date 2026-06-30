using Vintagestory.API.Common;
using HarmonyLib;

namespace OddWire.Patches;

[HarmonyPatch(typeof(ItemSlot), "ActivateSlotLeftClick")]
public static class ItemSlot_ActivateSlotLeftClick_SwapBagInSlotBag_Patch
{
    static bool Prefix(ItemSlot sourceSlot, ref ItemStackMoveOperation op, ItemSlot __instance)
    {
        if (__instance.Empty
        ||  sourceSlot.Empty
        ||  __instance is not ItemSlotBagContent
            )
            return true;

        var bag = __instance.Itemstack?.Collectible.GetCollectibleInterface<IHeldBag>();
        if (bag == null)
            return true;
        
        __instance.TryFlipWith(sourceSlot);
        return false;
    }
}