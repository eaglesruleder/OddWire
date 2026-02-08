using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OddWire.GameContent;

namespace OddWire.Patches
{
    [HarmonyPatch(typeof(BlockEntityBarrel), "OnEvery3Second")]
    public static class BEBarrel_OnEvery3Second_Patch
    {
        private const float SPOIL_DT_MODIFIER = 3600;
        
        [HarmonyPostfix]
        public static void Postfix(BlockEntityBarrel __instance, float dt)
        {
            if (__instance.Api?.Side != EnumAppSide.Server
            || !__instance.Sealed
            ||  __instance.CurrentRecipe == null
                )
                return;

            double sealedTime = __instance.Api.World.Calendar.TotalHours - __instance.SealedSinceTotalHours;
            var slots = new [] { __instance.Inventory[0], __instance.Inventory[1] };
            
            BarrelFailureRecipe currentFailRecipe = null;
            var barrelFailRecipes = __instance.Api.GetBarrelFailRecipes();
            for (int i = 0; i < barrelFailRecipes.Count; i++)
            {
                var recipe = barrelFailRecipes[i];
                if (recipe?.Spoil is null
                || !recipe.Enabled
                ||  recipe.SealHours > sealedTime
                || !recipe.Matches(slots, out _)
                   )
                    continue;

                if (recipe.SealHours > currentFailRecipe?.SealHours)
                    currentFailRecipe = recipe;
            }
            
            if (currentFailRecipe is null)
                return;

            float spoilRate = 0;
            
            float temp = __instance.Api.World.BlockAccessor.GetClimateAt(__instance.Pos)?.Temperature ?? 0f;
            if (currentFailRecipe.Spoil.MinEnvTemp > temp
            ||  currentFailRecipe.Spoil.MaxEnvTemp < temp
                )
                spoilRate += currentFailRecipe.Spoil.TempSpoilChance ?? 0;
                
            if (currentFailRecipe.Spoil.WaterVulnerable
            &&  IsWetRecently(__instance)
                )
                spoilRate += currentFailRecipe.Spoil.WetSpoilChance ?? 0;
            
            if (spoilRate > 0
            &&  __instance.Api.World.Rand.NextDouble() >= spoilRate * dt * SPOIL_DT_MODIFIER
            &&  currentFailRecipe.TryCraftNow(__instance.Api, sealedTime, slots)
               )
            {
                __instance.Sealed = false;
                __instance.MarkDirty(true);
                __instance.Api.World.BlockAccessor.MarkBlockEntityDirty(__instance.Pos);
            }
        }

        private static bool IsWetRecently(BlockEntityBarrel be)
        {
            var world = be.Api?.World;
            if (world is null)
                return false;

            var blockAccessor = world.BlockAccessor;
            var climate = blockAccessor.GetClimateAt(be.Pos);
            return
                climate is not null
            &&  TryGetPrecipitation(climate, out var precipitation)
            && !(precipitation <= 0f)
            &&  TryIsSkyExposed(blockAccessor, be.Pos);
        }

        private static MethodInfo? GetRainMapHeightAtMethod;
        private static bool TryIsSkyExposed(IBlockAccessor blockAccessor, BlockPos pos)
        {
            GetRainMapHeightAtMethod ??= blockAccessor.GetType().GetMethod("GetRainMapHeightAt", new[] { typeof(int), typeof(int) });
            if (GetRainMapHeightAtMethod is null)
                return false;

            var result = GetRainMapHeightAtMethod.Invoke(blockAccessor, new object[] { pos.X, pos.Z });
            if (result is int intHeight)
                return pos.Y >= intHeight;

            if (result is short shortHeight)
                return pos.Y >= shortHeight;

            if (result is float floatHeight)
                return pos.Y >= floatHeight;

            return false;
        }

        private static bool TryGetPrecipitation(object climate, out float precipitation)
        {
            precipitation = 0f;
            
            var precipitationProperty =
                climate.GetType().GetProperty("Precipitation")
            ??  climate.GetType().GetProperty("Rainfall");
            if (precipitationProperty == null)
                return false;

            var value = precipitationProperty.GetValue(climate);
            switch (value)
            {
                case float floatValue:
                    precipitation = floatValue;
                    return true;
                case double doubleValue:
                    precipitation = (float)doubleValue;
                    return true;
                case int intValue:
                    precipitation = intValue;
                    return true;
                default:
                    return false;
            }
        }
    }
}
