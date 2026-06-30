using System.Collections.Generic;
using Vintagestory.API.Common;

namespace OddWire.VintageStory.API.Common
{
    public static class ItemStackExtensions
    {
        public static bool CanBurn(this ItemStack stack, bool reqStackSize = true) =>
            stack?.Collectible?.CombustibleProps?.BurnTemperature > 0
        && (!reqStackSize || stack?.StackSize > 0);

        public static ItemStack[] ResolveStacks(this ICoreAPI api, string[] codes)
        {
            List<ItemStack> stacks = new();
            foreach (string code in codes)
            {
                ItemStack? stack = ResolveStack(api, code);
                if (stack is not null)
                    stacks.Add(stack);
            }

            return stacks.ToArray();
        }

        public static ItemStack? ResolveStack(this ICoreAPI api, string code)
        {
            AssetLocation loc = new(code);
            Item? item = api.World.GetItem(loc);
            if (item is not null)
                return new ItemStack(item);

            Block? block = api.World.GetBlock(loc);
            if (block is not null)
                return new ItemStack(block);

            return null;
        }
    }
}
