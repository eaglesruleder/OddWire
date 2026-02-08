using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace OddWire.GameContent
{
    public class BarrelFailureRecipe : BarrelRecipe, IRecipeBase<BarrelFailureRecipe>
    {
        [DocumentAsJson]
        public BarrelFailureSpoilProperties? Spoil { get; set; }

        public new BarrelFailureRecipe Clone()
        {
            BarrelRecipeIngredient[] ingredients = new BarrelRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
                ingredients[i] = Ingredients[i].Clone();

            return new BarrelFailureRecipe()
                {SealHours = SealHours
                ,Output = Output.Clone()
                ,Code = Code
                ,Enabled = Enabled
                ,Name = Name
                ,RecipeId = RecipeId
                ,Ingredients = ingredients
                ,Spoil = Spoil?.Clone()
                };
        }

        IRecipeIngredient[] IRecipeBase<BarrelFailureRecipe>.Ingredients => Ingredients;
        IRecipeOutput IRecipeBase<BarrelFailureRecipe>.Output => Output;
    }

    public class BarrelFailureSpoilProperties
    {
        public float? MinEnvTemp { get; set; }
        public float? MaxEnvTemp { get; set; }
        public float? TempSpoilChance { get; set; }
        
        public bool WaterVulnerable { get; set; }
        public float? WetSpoilChance { get; set; }

        public BarrelFailureSpoilProperties Clone() => new()
            {MinEnvTemp =  MinEnvTemp
            ,MaxEnvTemp =  MaxEnvTemp
            ,TempSpoilChance =  TempSpoilChance
            ,WaterVulnerable =  WaterVulnerable
            ,WetSpoilChance =  WetSpoilChance
            };
    }
}
