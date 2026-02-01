using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireSmithing : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass($"{Mod.Info.ModID}.holysymbol", typeof(BlockBrazier));
            api.RegisterBlockEntityClass($"{Mod.Info.ModID}smithing.brazier.blockentity", typeof(BlockEntityBrazier));
            
            base.Start(api);
        }
    }
}
