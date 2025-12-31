using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireModSystem : ModSystem
    {
        public GroundCraftingRecipeManager GroundCraftingRecipes { get; private set; }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass(Mod.Info.ModID+".", typeof(BlockBrazier));
            api.RegisterBlockEntityClass(Mod.Info.ModID+".", typeof(BlockEntityBrazier));
            GroundCraftingRecipes = new GroundCraftingRecipeManager(api);
            base.Start(api);
        }
    }
}
