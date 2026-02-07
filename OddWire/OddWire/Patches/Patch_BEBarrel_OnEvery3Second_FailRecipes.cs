using System;
using System.Linq;
using HarmonyLib;
using OddWire.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.Patches
{
    [HarmonyPatch(typeof(BlockEntityBarrel), "OnEvery3Second")]
    public static class Patch_BEBarrel_OnEvery3Second_FailRecipes
    {
        [HarmonyPostfix]
        public static void Postfix(BlockEntityBarrel __instance, float dt)
        {
            if (__instance.Api?.Side != EnumAppSide.Server)
            {
                return;
            }

            if (!__instance.Sealed)
            {
                return;
            }

            if (__instance.CurrentRecipe == null)
            {
                return;
            }

            var slots = new ItemSlot[] { __instance.Inventory[0], __instance.Inventory[1] };
            int outsize = 0;
            var fail = __instance.Api.GetBarrelFailRecipes()
                .FirstOrDefault(recipe => recipe?.Spoil != null && recipe.Enabled && recipe.Matches(slots, out outsize));

            if (fail == null)
            {
                return;
            }

            float temp = GetEnvTemp(__instance.Api, __instance.Pos);
            bool outOfRange =
                (fail.Spoil.MinEnvTemp != null && temp < fail.Spoil.MinEnvTemp.Value)
                || (fail.Spoil.MaxEnvTemp != null && temp > fail.Spoil.MaxEnvTemp.Value);

            bool wet = fail.Spoil.WaterVulnerable && IsWetRecently(__instance);

            float spoil = __instance.WatchedAttributes.GetFloat("barrelSpoil", 0f);
            if (outOfRange)
            {
                spoil += 0.05f;
            }

            if (wet)
            {
                spoil += 0.10f;
            }

            spoil = GameMath.Clamp(spoil, 0f, 1f);
            __instance.WatchedAttributes.SetFloat("barrelSpoil", spoil);

            if (spoil >= 1f)
            {
                double sealedHours = __instance.Api.World.Calendar.TotalHours - __instance.SealedSinceTotalHours;
                bool crafted = fail.TryCraftNow(__instance.Api, sealedHours, slots) == true;
                if (crafted)
                {
                    __instance.WatchedAttributes.SetFloat("barrelSpoil", 0f);
                    __instance.Sealed = false;
                    __instance.MarkDirty(true);
                    __instance.Api.World.BlockAccessor.MarkBlockEntityDirty(__instance.Pos);
                }
            }
        }

        private static float GetEnvTemp(ICoreAPI api, BlockPos pos)
        {
            return api.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues)?.Temperature ?? 0f;
        }

        private static bool IsWetRecently(BlockEntityBarrel be)
        {
            var world = be.Api?.World;
            if (world == null)
            {
                return false;
            }

            var blockAccessor = world.BlockAccessor;
            var climate = blockAccessor.GetClimateAt(be.Pos, EnumGetClimateMode.NowValues);
            if (climate == null)
            {
                return false;
            }

            if (!TryGetPrecipitation(climate, out var precipitation) || precipitation <= 0f)
            {
                return false;
            }

            if (!TryIsSkyExposed(blockAccessor, be.Pos))
            {
                return false;
            }

            return true;
        }

        private static bool TryIsSkyExposed(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var method = blockAccessor.GetType().GetMethod("GetRainMapHeightAt", new[] { typeof(int), typeof(int) });
            if (method == null)
            {
                return false;
            }

            var result = method.Invoke(blockAccessor, new object[] { pos.X, pos.Z });
            if (result is int intHeight)
            {
                return pos.Y >= intHeight;
            }

            if (result is short shortHeight)
            {
                return pos.Y >= shortHeight;
            }

            if (result is float floatHeight)
            {
                return pos.Y >= floatHeight;
            }

            return false;
        }

        private static bool TryGetPrecipitation(object climate, out float precipitation)
        {
            precipitation = 0f;

            var precipitationProperty = climate.GetType().GetProperty("Precipitation")
                ?? climate.GetType().GetProperty("Rainfall");

            if (precipitationProperty == null)
            {
                return false;
            }

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
