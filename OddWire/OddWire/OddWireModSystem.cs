using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass($"{Mod.Info.ModID}.BlockBrazier", typeof(BlockBrazier));
            api.RegisterBlockEntityClass($"{Mod.Info.ModID}.Brazier", typeof(BlockEntityBrazier));
            
            base.Start(api);
        }
    }
}
