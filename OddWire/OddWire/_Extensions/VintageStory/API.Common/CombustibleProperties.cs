using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace OddWire.VintageStory.API.Common
{
    public static class CombustiblePropertiesExtensions
    {
        public static bool CanBurn(this CombustibleProperties props) => (props?.BurnTemperature ?? -1) > 0;
    }
}
