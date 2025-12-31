using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireModSystem : ModSystem
    {
        public SmithingRecipeManager SmithingRecipes { get; private set; }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass(Mod.Info.ModID+".", typeof(BlockBrazier));
            api.RegisterBlockEntityClass(Mod.Info.ModID+".", typeof(BlockEntityBrazier));
            SmithingRecipes = new SmithingRecipeManager(api);
            base.Start(api);
        }
    }
}
