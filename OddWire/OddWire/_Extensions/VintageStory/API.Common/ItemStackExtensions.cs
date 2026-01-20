using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace OddWire.VintageStory.API.Common
{
    public static class ItemStackExtensions
    {
        public static bool CanBurn(this ItemStack stack) => stack?.Collectible?.CombustibleProps?.BurnTemperature > 0;
    }
}
