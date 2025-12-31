using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace OddWire.GameContent
{
    public class GroundCraftingRecipeManager
    {
        readonly ICoreAPI api;
        readonly Dictionary<string, GroundCraftingRecipe> recipesByPattern = new Dictionary<string, GroundCraftingRecipe>();

        public GroundCraftingRecipeManager(ICoreAPI api)
        {
            this.api = api;
            LoadRecipes();
        }

        public GroundCraftingResolvedRecipe ResolveFor(Block block)
        {
            foreach (GroundCraftingRecipe recipe in recipesByPattern.Values)
            {
                GroundCraftingResolvedRecipe resolved = recipe.ResolveFor(block);
                if (resolved != null) return resolved;
            }

            return null;
        }

        public GroundCraftingResolvedRecipe ResolveFor(Block block, string pattern)
        {
            if (pattern == null) return null;

            return recipesByPattern.TryGetValue(pattern, out GroundCraftingRecipe recipe)
                ? recipe.ResolveFor(block)
                : null;
        }

        void LoadRecipes()
        {
            foreach (IAsset asset in api.Assets.GetMany(new AssetLocation("recipes/groundcrafting")))
            {
                GroundCraftingRecipeFile recipeFile = asset.ToObject<GroundCraftingRecipeFile>();
                if (recipeFile?.IngredientsByType == null) continue;

                foreach (KeyValuePair<string, GroundCraftingRecipeDefinition> entry in recipeFile.IngredientsByType)
                {
                    if (entry.Value == null || entry.Value.Steps == null || entry.Value.Steps.Length == 0) continue;
                    recipesByPattern[entry.Key] = new GroundCraftingRecipe(entry.Key, entry.Value);
                }
            }
        }
    }

    public class GroundCraftingRecipeFile
    {
        [JsonProperty("ingredientsByType")]
        public Dictionary<string, GroundCraftingRecipeDefinition> IngredientsByType { get; set; }
    }

    public class GroundCraftingRecipeDefinition
    {
        [JsonProperty("allowedvariants")]
        public string[] AllowedVariants { get; set; } = Array.Empty<string>();

        [JsonProperty("steps")]
        public GroundCraftingStepDefinition[] Steps { get; set; } = Array.Empty<GroundCraftingStepDefinition>();

        [JsonProperty("output")]
        public string Output { get; set; }
    }

    public class GroundCraftingStepDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("temp")]
        public float? Temp { get; set; }

        [JsonProperty("hammerHits")]
        public int? HammerHits { get; set; }
    }

    public class GroundCraftingRecipe
    {
        public string Pattern { get; }
        public GroundCraftingRecipeDefinition Definition { get; }

        public GroundCraftingRecipe(string pattern, GroundCraftingRecipeDefinition definition)
        {
            Pattern = pattern;
            Definition = definition;
        }

        public GroundCraftingResolvedRecipe ResolveFor(Block block)
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
            return new GroundCraftingResolvedRecipe(Pattern, output, Definition.Steps, metal);
        }

        static string ResolvePattern(string pattern, string metal)
        {
            return pattern?.Replace("{metal}", metal ?? string.Empty);
        }
    }

    public class GroundCraftingResolvedRecipe
    {
        public string Pattern { get; }
        public string OutputCode { get; }
        public GroundCraftingStepDefinition[] Steps { get; }
        public string Metal { get; }

        public GroundCraftingResolvedRecipe(string pattern, string outputCode, GroundCraftingStepDefinition[] steps, string metal)
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
