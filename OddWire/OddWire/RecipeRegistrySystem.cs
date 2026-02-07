using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace OddWire.GameContent
{
    public class RecipeRegistrySystem : ModSystem
    {
        public List<BarrelFailureRecipe> BarrelFailRecipes { get; private set; } = new();

        public override void Start(ICoreAPI api)
        {
            BarrelFailRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<BarrelFailureRecipe>>("barrelfailrecipes").Recipes;

            base.Start(api);
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            foreach (var recipe in BarrelFailRecipes)
            {
                recipe.Resolve(api.World, "barrelfailrecipe");
            }
        }

        public BarrelFailureRecipe FindMatchingBarrelFailRecipe(ItemSlot[] slots, out int outsize)
        {
            foreach (var recipe in BarrelFailRecipes)
            {
                if (!recipe.Enabled)
                {
                    continue;
                }

                if (recipe.Matches(slots, out outsize))
                {
                    return recipe;
                }
            }

            outsize = 0;
            return null;
        }
    }

    public static class RecipeRegistrySystemExtensions
    {
        public static List<BarrelFailureRecipe> GetBarrelFailRecipes(this ICoreAPI api)
        {
            return api.ModLoader.GetModSystem<RecipeRegistrySystem>().BarrelFailRecipes;
        }
    }
}
