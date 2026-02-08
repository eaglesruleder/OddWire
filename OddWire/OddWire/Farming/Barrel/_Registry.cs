using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using OddWire.VintageStory.API.Server;

namespace OddWire.GameContent;
public partial class OddWireRegistrySystem
{
    public List<BarrelFailureRecipe> BarrelFailRecipes { get; private set; } = new();
    
    private void Start_BarrelFailRecipes(ICoreAPI api)
    {
        BarrelFailRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<BarrelFailureRecipe>>("barrelfailrecipes").Recipes;
    }

    private void AssetsLoaded_BarrelFailRecipes(ICoreAPI api)
    {
        foreach (var recipe in BarrelFailRecipes)
            recipe.Resolve(api.World, "barrelfailrecipe");
    }
    
    public void RegisterBarrelRecipe(BarrelFailureRecipe recipe)
    {
        if (!canRegister)
            throw new InvalidOperationException("Coding error: Can no long register cooking recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
        
        if (recipe.Code == null)
            throw new ArgumentException("Barrel recipes must have a non-null code! (choose freely)");

        foreach (var ingredient in recipe.Ingredients)
            if (ingredient.ConsumeQuantity != null 
            &&  ingredient.ConsumeQuantity > ingredient.Quantity
                )
                throw new ArgumentException("Barrel recipe with code {0} has an ingredient with ConsumeQuantity > Quantity. Not a valid recipe!");

        BarrelFailRecipes.Add(recipe);
    }

    public BarrelFailureRecipe? FindMatchingBarrelFailRecipe(ItemSlot[] slots, out int outsize)
    {
        for (int i = 0; i < BarrelFailRecipes.Count; i++)
        {
            var recipe = BarrelFailRecipes[i];
            if (recipe.Enabled
            &&  recipe.Matches(slots, out outsize)
                )
                return recipe;
        }

        outsize = 0;
        return null;
    }
}

public static class BarrelFailureRecipeExtensions
{
    public static void LoadBarrelFailRecipes(this ICoreServerAPI api) => 
        api.LoadRecipes<BarrelFailureRecipe>("barrel fail recipe", "recipes/barrelfail", r => api.RegisterBarrelFailRecipe(r));
    
    public static void RegisterBarrelFailRecipe(this ICoreServerAPI api, BarrelFailureRecipe r) =>
        api.ModLoader.GetModSystem<OddWireRegistrySystem>().RegisterBarrelRecipe(r);
    
    
    public static List<BarrelFailureRecipe> GetBarrelFailRecipes(this ICoreAPI api) =>
        api.ModLoader.GetModSystem<OddWireRegistrySystem>().BarrelFailRecipes;
}