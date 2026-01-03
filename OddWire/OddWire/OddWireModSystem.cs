using OddWire.GameContent;
using Vintagestory.API.Common;

namespace OddWire
{
    public class OddWireModSystem : ModSystem
    {
        public FabricationRecipeManager FabricationRecipes { get; private set; }

        public override void Start(ICoreAPI api)
        {
            FabricationRecipes = new FabricationRecipeManager(api);
            
            api.RegisterBlockClass($"{Mod.Info.ModID}.BlockBrazier", typeof(BlockBrazier));
            api.RegisterBlockClass($"{Mod.Info.ModID}.BlockFabricate", typeof(BlockFabricate));
            api.RegisterBlockEntityClass($"{Mod.Info.ModID}.BlockEntityBrazier", typeof(BlockEntityBrazier));
            api.RegisterBlockEntityClass($"{Mod.Info.ModID}.BlockEntityFabricate", typeof(BlockEntityFabricate));
            api.RegisterBlockEntityBehaviorClass($"{Mod.Info.ModID}.BEBehaviorBrazierAmbient", typeof(BEBehaviorBrazierAmbient));
            api.RegisterBlockEntityBehaviorClass($"{Mod.Info.ModID}.BEBehaviorBrazierMusic", typeof(BEBehaviorBrazierMusic));
            
            base.Start(api);
        }
    }
}
