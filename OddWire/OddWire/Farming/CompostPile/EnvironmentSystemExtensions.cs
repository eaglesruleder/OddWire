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

        double hoursPerDay = weather.api.World.Calendar.HoursPerDay;
        double totalDays = toTotalHours / hoursPerDay;
        double hoursPassed = toTotalHours - fromTotalHours;

        ClimateCondition? baseClimate = null;
        if (hoursPassed > 0)
            baseClimate = weather.api.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues, totalDays - hoursPassed / hoursPerDay / 2);

        float totalRainfall = 0f;
        while (hoursPassed > 0)
        {
            totalRainfall += weather.GetPrecipitation(pos, totalDays - hoursPassed / hoursPerDay, baseClimate);
            hoursPassed--;
        }

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
