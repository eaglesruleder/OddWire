using HarmonyLib;
using Vintagestory.API.Common;

namespace YourMod
{
    [HarmonyPatch(typeof(ItemSlotSurvival), nameof(ItemSlotSurvival.CanHold))]
    static class ItemSlotSurvival_CanHold_AllowSmallBag_Patch
    {
        static bool Prefix(ItemSlot sourceSlot, ref bool __result, ItemSlotSurvival __instance)
        {
            var bag = sourceSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldBag>();
            if (bag?.IsEmpty(sourceSlot.Itemstack) == true)
                return true;

            var quantitySlots = bag?.GetQuantitySlots(sourceSlot.Itemstack);
            if (quantitySlots >= 4)
                return true;
            
            __result = __instance.Inventory.CanContain(__instance, sourceSlot);
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemSlotSurvival), nameof(ItemSlotSurvival.CanTakeFrom))]
    static class ItemSlotSurvival_CanTakeFrom_AllowSmallBag_Patch
    {
        static bool Prefix(ItemSlot sourceSlot, ref bool __result)
        {
            var bag = sourceSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldBag>();
            if (bag?.IsEmpty(sourceSlot.Itemstack) == true)
                return true;

            var quantitySlots = bag?.GetQuantitySlots(sourceSlot.Itemstack);
            if (quantitySlots >= 4)
                return true;
            
            __result = true;
            return false;
        }
    }
}
