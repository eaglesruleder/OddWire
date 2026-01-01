using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireModSystem : ModSystem
    {
        public FabricationRecipeManager FabricationRecipes { get; private set; }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass(Mod.Info.ModID+".", typeof(BlockBrazier));
            api.RegisterBlockClass(Mod.Info.ModID+".", typeof(BlockFabricate));
            api.RegisterBlockEntityClass(Mod.Info.ModID+".", typeof(BlockEntityBrazier));
            api.RegisterBlockEntityClass(Mod.Info.ModID+".", typeof(BlockEntityFabricate));
            FabricationRecipes = new FabricationRecipeManager(api);
            base.Start(api);
        }
    }
}
