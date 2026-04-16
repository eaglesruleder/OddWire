using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireFarming : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass($"{Mod.Info.ModID}farming.compostpile.block", typeof(BlockCompostpile));
            api.RegisterBlockEntityClass($"{Mod.Info.ModID}farming.compostpile.blockentity", typeof(BlockEntityCompostpile));
            
            base.Start(api);
        }
    }
}
