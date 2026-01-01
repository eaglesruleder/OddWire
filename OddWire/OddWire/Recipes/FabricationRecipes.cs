using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace OddWire.GameContent
{
    public class FabricationRecipeManager
    {
        readonly ICoreAPI api;
        readonly List<FabricationRecipe> recipes = new List<FabricationRecipe>();

        public FabricationRecipeManager(ICoreAPI api)
        {
            this.api = api;
            LoadRecipes();
        }

        public FabricationResolvedRecipe ResolveFor(Block block)
        {
            foreach (FabricationRecipe recipe in recipes)
            {
                FabricationResolvedRecipe resolved = recipe.ResolveFor(block);
                if (resolved != null) return resolved;
            }

            return null;
        }

        public FabricationResolvedRecipe ResolveFor(Block block, string pattern)
        {
            if (pattern == null) return null;

            foreach (FabricationRecipe recipe in recipes)
            {
                if (!string.Equals(recipe.Pattern, pattern, StringComparison.OrdinalIgnoreCase)) continue;
                FabricationResolvedRecipe resolved = recipe.ResolveFor(block);
                if (resolved != null) return resolved;
            }

            return null;
        }

        void LoadRecipes()
        {
            foreach (IAsset asset in api.Assets.GetMany(new AssetLocation("recipes/fabrication")))
            {
                FabricationRecipeFile recipeFile = asset.ToObject<FabricationRecipeFile>();
                if (recipeFile?.IngredientsByType == null) continue;

                foreach (KeyValuePair<string, FabricationRecipeDefinition> entry in recipeFile.IngredientsByType)
                {
                    if (entry.Value == null || entry.Value.Steps == null || entry.Value.Steps.Length == 0) continue;
                    recipes.Add(new FabricationRecipe(entry.Key, entry.Value));
                }
            }
        }
    }

    public class FabricationRecipeFile
    {
        [JsonProperty("ingredientsByType")]
        public Dictionary<string, FabricationRecipeDefinition> IngredientsByType { get; set; }
    }

    public class FabricationRecipeDefinition
    {
        [JsonProperty("allowedvariants")]
        public string[] AllowedVariants { get; set; } = Array.Empty<string>();

        [JsonProperty("steps")]
        public FabricationStepDefinition[] Steps { get; set; } = Array.Empty<FabricationStepDefinition>();

        [JsonProperty("output")]
        public string Output { get; set; }
    }

    public class FabricationStepDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("temp")]
        public float? Temp { get; set; }

        [JsonProperty("hammerHits")]
        public int? HammerHits { get; set; }
    }

    public class FabricationRecipe
    {
        public string Pattern { get; }
        public FabricationRecipeDefinition Definition { get; }

        public FabricationRecipe(string pattern, FabricationRecipeDefinition definition)
        {
            Pattern = pattern;
            Definition = definition;
        }

        public FabricationResolvedRecipe ResolveFor(Block block)
        {
            if (block == null) return null;

            string metal = block.Variant?["metal"];
            string resolvedPattern = ResolvePattern(Pattern, metal);
            if (!WildcardUtil.Match(resolvedPattern, block.Code.Path)) return null;

            if (Definition.AllowedVariants?.Length > 0 && !Definition.AllowedVariants.Contains(metal))
            {
                return null;
            }

            string output = ResolvePattern(Definition.Output ?? Pattern, metal);
            return new FabricationResolvedRecipe(Pattern, output, Definition.Steps, metal);
        }

        static string ResolvePattern(string pattern, string metal)
        {
            return pattern?.Replace("{metal}", metal ?? string.Empty);
        }
    }

    public class FabricationResolvedRecipe
    {
        public string Pattern { get; }
        public string OutputCode { get; }
        public FabricationStepDefinition[] Steps { get; }
        public string Metal { get; }

        public FabricationResolvedRecipe(string pattern, string outputCode, FabricationStepDefinition[] steps, string metal)
        {
            Pattern = pattern;
            OutputCode = outputCode;
            Steps = steps;
            Metal = metal;
        }

        public bool MatchesStep(ItemStack stack, int stepIndex, ICoreAPI api)
        {
            if (stack == null || stepIndex < 0 || stepIndex >= Steps.Length) return false;
            GroundCraftingStepDefinition step = Steps[stepIndex];
            string stepCode = ResolveCode(step.Name);
            CollectibleObject collectible = api.World.GetItem(new AssetLocation(stepCode))
                ?? (CollectibleObject)api.World.GetBlock(new AssetLocation(stepCode));

            return collectible != null && collectible.Code.Equals(stack.Collectible.Code);
        }

        public ItemStack CreateOutputStack(IWorldAccessor world)
        {
            if (string.IsNullOrWhiteSpace(OutputCode)) return null;
            AssetLocation code = new AssetLocation(OutputCode);
            CollectibleObject collectible = world.GetItem(code) ?? (CollectibleObject)world.GetBlock(code);
            return collectible == null ? null : new ItemStack(collectible);
        }

        public ItemStack[] CreateStepStacks(IWorldAccessor world, int count)
        {
            List<ItemStack> stacks = new List<ItemStack>();
            for (int i = 0; i < count && i < Steps.Length; i++)
            {
                string stepCode = ResolveCode(Steps[i].Name);
                CollectibleObject collectible = world.GetItem(new AssetLocation(stepCode))
                    ?? (CollectibleObject)world.GetBlock(new AssetLocation(stepCode));
                if (collectible == null) continue;
                stacks.Add(new ItemStack(collectible));
            }

            return stacks.ToArray();
        }

        public float GetRequiredTemperature(int stepIndex)
        {
            return stepIndex >= 0 && stepIndex < Steps.Length ? Steps[stepIndex].Temp ?? 0 : 0;
        }

        public int GetRequiredHammerHits(int stepIndex)
        {
            return stepIndex >= 0 && stepIndex < Steps.Length ? Steps[stepIndex].HammerHits ?? 0 : 0;
        }

        string ResolveCode(string code)
        {
            return code?.Replace("{metal}", Metal ?? string.Empty);
        }
    }
}
