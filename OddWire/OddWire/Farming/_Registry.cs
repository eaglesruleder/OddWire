using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace OddWire.GameContent;
public partial class OddWireRegistrySystem
{
    public void Start_Farming(ICoreAPI api)
    {
        Start_BarrelFailRecipes(api);
    }
    
    public void AssetsLoaded_Farming(ICoreAPI api)
    {
        AssetsLoaded_BarrelFailRecipes(api);
    }
}

public static class FarmingExtensions
{
    public static void LoadFarmingRecipes(this ICoreServerAPI api)
    {
        api.LoadBarrelFailRecipes();
    }
}