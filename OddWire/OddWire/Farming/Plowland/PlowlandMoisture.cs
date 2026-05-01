using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public sealed class PlowlandMoisture
{
    private static readonly PlowlandSettings Settings = new();

    public float Moisture01;
    public float LastWaterDistance = 99f;
    public double PrevTimeMoistureUpdated = -1;

    public void Reset(float moisture01 = 1f)
    {
        Moisture01 = moisture01;
        LastWaterDistance = 99f;
        PrevTimeMoistureUpdated = -1;
    }

    public bool Water(float dt)
    {
        float newMoisture01 = Math.Min(1f, Moisture01 + dt / 2f);
        if (newMoisture01.Approx(Moisture01))
            return false;
        Moisture01 = newMoisture01;
        return true;
    }

    public bool Tick(BlockEntityPlowland be, bool supportIsValid, float supportRetentionDays)
    {
        double totalHours = be.Api.World.Calendar.TotalHours;
        if (PrevTimeMoistureUpdated < 0)
        {
            PrevTimeMoistureUpdated = totalHours;
            return false;
        }

        double hoursPassed = totalHours - PrevTimeMoistureUpdated;
        if (hoursPassed <= 0)
            return false;
        PrevTimeMoistureUpdated = totalHours;

        float waterDistance = NearbyWaterDistance(be.Api.World.BlockAccessor, be.Pos, Settings.WaterSearchRadius, out bool deferred);
        if (deferred)
            return false;

        LastWaterDistance = waterDistance;

        float minMoisture = ResolveMinMoisture(waterDistance, supportIsValid);
        float totalRetentionHours = Math.Max(0.25f, supportRetentionDays) * be.Api.World.Calendar.HoursPerDay;
        float newMoisture01 = Math.Max(minMoisture, Moisture01 - (float)hoursPassed / totalRetentionHours);

        if (newMoisture01.Approx(Moisture01))
            return false;

        if (Moisture01 > Settings.MoistVisibleThreshold
        !=  newMoisture01 > Settings.MoistVisibleThreshold
            )
            TrySwapMoistureVariant(be, newMoisture01 > Settings.MoistVisibleThreshold);

        Moisture01 = newMoisture01;
        return true;
    }

    public static float NearbyWaterDistance(IBlockAccessor blockAccessor, BlockPos pos, int radius, out bool deferred)
    {
        float waterDistance = 99f;
        bool chunkMissing = false;

        blockAccessor.SearchFluidBlocks
            (new BlockPos(pos.X - radius, pos.Y, pos.Z - radius)
            ,new BlockPos(pos.X + radius, pos.Y, pos.Z + radius)
            ,(block, blockPos) =>
            {
                if (block.LiquidCode == "water")
                    waterDistance = Math.Min(waterDistance, Math.Max(Math.Abs(blockPos.X - pos.X), Math.Abs(blockPos.Z - pos.Z)));
                return true;
            }
            ,(cx, cy, cz) => chunkMissing = true
            );

        deferred = chunkMissing;
        return deferred ? 99f : waterDistance;
    }

    private static float ResolveMinMoisture(float waterDistance, bool supportIsValid) =>
        supportIsValid
    ?   GameMath.Clamp(1f - waterDistance / Settings.WaterSearchRadius, 0, 1)
    :   0;

    private static void TrySwapMoistureVariant(BlockEntityPlowland be, bool isMoist)
    {
        string? fertilityCode = FertilitySet.GetCode(be.Block);
        if (fertilityCode is null
        ||  be.Block.Code is null
            )
            return;

        string moistKey = isMoist ? Settings.StateMoist : Settings.StateDry;
        AssetLocation newCode = new(be.Block.Code.Domain, $"plowland-{moistKey}-{fertilityCode}");
        Block newBlock = be.Api.World.GetBlock(newCode);
        if (newBlock is not null
        &&  newBlock.Id != 0
            )
            be.Api.World.BlockAccessor.ExchangeBlock(newBlock.BlockId, be.Pos);
    }

    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetFloat("moisture01", Moisture01);
        tree.SetFloat("lastWaterDistance", LastWaterDistance);
        tree.SetDouble("prevTimeMoistureUpdated", PrevTimeMoistureUpdated);
    }

    public void FromTreeAttributes(ITreeAttribute tree)
    {
        Moisture01 = tree.GetFloat("moisture01");
        LastWaterDistance = tree.GetFloat("lastWaterDistance", 99f);
        PrevTimeMoistureUpdated = tree.GetDouble("prevTimeMoistureUpdated", -1);
    }
    #endregion
}
