using Vintagestory.API.Common;

namespace OddWire.Patches;

public static class IHeldBag_Extensions
{
    public static bool IsHandheld(this IHeldBag bag, ItemStack? stack) =>
        // Intent: Target is Basket, this was simplest
        bag.GetQuantitySlots(stack) < 4;
}