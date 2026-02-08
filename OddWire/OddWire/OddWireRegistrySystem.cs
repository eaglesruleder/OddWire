using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace OddWire.GameContent;
public partial class OddWireRegistrySystem : ModSystem
{
    public static bool canRegister = true;
    
    public override double ExecuteOrder() => 0.6;

    public override void StartPre(ICoreAPI api)
    {
        canRegister = true;
    }
    
    public override void Start(ICoreAPI api)
    {
        Start_Farming(api);
        base.Start(api);
    }
    
    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        AssetsLoaded_Farming(api);
    }
}

public class OddWireRecipeLoader : ModSystem
{
    public override double ExecuteOrder() => 1;
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (!(api is ICoreServerAPI sapi))
            return;

        sapi.LoadFarmingRecipes();
    }
}

public class DisableOddWireRegistrySystem : ModSystem
{
    public override double ExecuteOrder() => 99999;
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;
    public override void AssetsFinalize(ICoreAPI api)
    {
        OddWireRegistrySystem.canRegister = false;
    }
}