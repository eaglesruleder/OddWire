using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace OddWire.GameContent
{
    public class BarrelFailureRecipe : BarrelRecipe
    {
        [DocumentAsJson]
        public BarrelFailureSpoilProperties Spoil { get; set; }
    }

    public class BarrelFailureSpoilProperties
    {
        public float? MinEnvTemp { get; set; }
        public float? MaxEnvTemp { get; set; }
        public bool WaterVulnerable { get; set; }
    }
}
