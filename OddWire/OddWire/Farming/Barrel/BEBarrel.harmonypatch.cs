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
        private const float SPOIL_DT_MODIFIER = 20 * 24 * 60 * 60;
        
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
            
            BarrelFailureRecipe? currentFailRecipe = null;
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

                if (currentFailRecipe is null
                ||  currentFailRecipe.SealHours > recipe.SealHours
                    )
                    currentFailRecipe = recipe;
            }
            
            if (currentFailRecipe?.Spoil is null)
                return;

            float spoilRate = 0;
            
            float temp = GetEnvTemperature(__instance);
            if (currentFailRecipe.Spoil.MinEnvTemp > temp
            ||  currentFailRecipe.Spoil.MaxEnvTemp < temp
                )
                spoilRate += currentFailRecipe.Spoil.TempSpoilChance ?? 0;
                
            if (currentFailRecipe.Spoil.WaterVulnerable
            &&  IsWetRecently(__instance)
                )
                spoilRate += currentFailRecipe.Spoil.WetSpoilChance ?? 0;
            
            if (spoilRate > 0
            &&  __instance.Api.World.Rand.NextDouble() < spoilRate * dt / SPOIL_DT_MODIFIER
            &&  currentFailRecipe.TryCraftNow(__instance.Api, sealedTime, slots)
               )
            {
                __instance.Sealed = false;
                __instance.MarkDirty(true);
                __instance.Api.World.BlockAccessor.MarkBlockEntityDirty(__instance.Pos);
            }
        }
        
        private static float GetEnvTemperature(BlockEntityBarrel be)
        {
            var api = be.Api;
            if (api?.World == null)
                return 0f;
            
            float temp =
                api.World.BlockAccessor.GetClimateAt
                    (be.Pos
                    ,EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly
                    ,api.World.Calendar.TotalDays
                    )?.Temperature
                ??  0f;

            if (HasGreenhouseTempBonus(api, be.Pos))
                temp += 5;
            
            return temp;
        }

        private static bool HasGreenhouseTempBonus(ICoreAPI api, BlockPos pos)
        {
            BlockPos upPos = pos.UpCopy();
            int rainMapY = api.World.BlockAccessor.GetRainMapHeightAt(upPos.X, upPos.Z);
            if (rainMapY <= upPos.Y)
                return false;
            
            var roomReg = api.ModLoader.GetModSystem<RoomRegistry>();
            if (roomReg == null)
                return false;

            Room room = roomReg.GetRoomForPosition(upPos);
            if (room == null)
                return false;
            
            return
                room.SkylightCount > room.NonSkylightCount
            &&  room.ExitCount == 0;
        }
        
        private static bool IsWetRecently(BlockEntityBarrel barrelEntity)
        {
            var api = barrelEntity.Api;
            var world = api?.World;
            if (world == null)
                return false;

            var blockAccessor = world.BlockAccessor;
            if (!TryIsSkyExposed(blockAccessor, barrelEntity.Pos))
                return false;

            var weather = api.ModLoader.GetModSystem<WeatherSystemBase>();
            if (weather == null)
                return false;

            double totalDays = world.Calendar.TotalDays;
            
            return weather.GetPrecipitation
                (barrelEntity.Pos, totalDays
                ,blockAccessor.GetClimateAt
                    (barrelEntity.Pos
                    ,EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly
                    ,totalDays
                    )
                ) > 0;
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
    }
}
