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
            api.RegisterBlockEntityClass(Mod.Info.ModID+".", typeof(BlockEntityBrazier));
            FabricationRecipes = new FabricationRecipeManager(api);
            base.Start(api);
        }
    }
}
