using Vintagestory.API.Common;

namespace OddWire.VintageStory.API.Common
{
    public static class ItemStackExtensions
    {
        public static bool CanBurn(this ItemStack stack, bool reqStackSize = true) =>
            stack?.Collectible?.CombustibleProps?.BurnTemperature > 0
        && (!reqStackSize || stack?.StackSize > 0);
    }
}
