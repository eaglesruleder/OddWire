using Vintagestory.API.Common;
using HarmonyLib;

namespace OddWire.Patches;

[HarmonyPatch(typeof(ItemSlotSurvival), nameof(ItemSlotSurvival.CanHold))]
static class ItemSlotSurvival_CanHold_AllowSmallBag_Patch
{
    static bool Prefix(ItemSlot sourceSlot, ref bool __result, ItemSlotSurvival __instance)
    {
        if (__instance is ItemSlotBagContent)
            return true;
        
        var bag = sourceSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldBag>();
        if (bag?.IsEmpty(sourceSlot.Itemstack) != false)
            return true;
        
        var quantitySlots = bag.GetQuantitySlots(sourceSlot.Itemstack);
        if (quantitySlots >= 4)
            return true;
        
        __result = __instance.Inventory.CanContain(__instance, sourceSlot);
        return false;
    }
}

[HarmonyPatch(typeof(ItemSlotSurvival), nameof(ItemSlotSurvival.CanTakeFrom))]
static class ItemSlotSurvival_CanTakeFrom_AllowSmallBag_Patch
{
    static bool Prefix(ItemSlot sourceSlot, ref bool __result, ItemSlotSurvival __instance)
    {
        if (__instance is ItemSlotBagContent)
            return false;
        
        var bag = sourceSlot.Itemstack?.Collectible.GetCollectibleInterface<IHeldBag>();
        if (bag?.IsEmpty(sourceSlot.Itemstack) != false)
            return true;

        var quantitySlots = bag.GetQuantitySlots(sourceSlot.Itemstack);
        if (quantitySlots >= 4)
            return true;
        
        __result = true;
        return false;
    }
}
