using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass($"BlockBrazier", typeof(BlockBrazier));
            api.RegisterBlockEntityClass($"Brazier", typeof(BlockEntityBrazier));
            base.Start(api);
        }
    }
}
