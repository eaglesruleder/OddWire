using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public static class EnvironmentSystemExtensions
{
    public static bool IsSkyExposed(this IBlockAccessor blockAccessor, BlockPos pos)
        => blockAccessor.GetRainMapHeightAt(pos.X, pos.Z) <= pos.Y;

    public static ClimateCondition GetClimateAtHours(this IWorldAccessor world, BlockPos pos, double totalHours) =>
        world.BlockAccessor.GetClimateAt
            (pos
            ,EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly
            ,totalHours / world.Calendar.HoursPerDay
            );

    public static float GetTotalRainfallSince(this WeatherSystemBase weather, BlockPos pos, double fromTotalHours, double toTotalHours)
    {
        if (toTotalHours <= fromTotalHours)
            return 0f;

        var calendar = weather.api.World.Calendar;

        ClimateCondition? baseClimate = null;
        
        float totalRainfall = 0f;
        double remainingDays = (toTotalHours - fromTotalHours) / calendar.HoursPerDay;
        while (remainingDays > 1)
        {
            double stepDays = remainingDays / 2f;
            double sampleDays = remainingDays + stepDays;

            baseClimate = weather.api.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues, sampleDays);
            
            totalRainfall += weather.GetPrecipitation(pos, sampleDays, baseClimate) * (float)stepDays;
            remainingDays -= stepDays;
        }
        totalRainfall += weather.GetPrecipitation(pos, toTotalHours, baseClimate) * calendar.HoursPerDay * (float)remainingDays;
        
        return totalRainfall;
    }

    public static float GetEnvironmentTemperatureC
        (this ICoreAPI api
        ,BlockPos pos
        ,double totalHours
        ,bool skyExposed
        ,float greenhouseTempBonusC
        ,out bool inGreenhouse
        )
    {
        inGreenhouse = false;

        ClimateCondition conds = api.World.GetClimateAtHours(pos, totalHours);
        float temp = conds?.Temperature ?? 0f;
        
        if (!skyExposed)
        {
            var room = api.ModLoader.GetModSystem<RoomRegistry>()?.GetRoomForPosition(pos.UpCopy());
            if (room != null
            &&  room.SkylightCount > room.NonSkylightCount
            &&  room.ExitCount == 0
                )
            {
                inGreenhouse = true;
                temp += greenhouseTempBonusC;
            }
        }

        return temp;
    }
}
