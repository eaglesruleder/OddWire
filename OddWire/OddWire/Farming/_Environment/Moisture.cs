using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public sealed class Moisture : IWaterable
{
    public float VisibleThreshold = 0.10f;
    public int WaterSearchRadius = 4;
    public float MinRetentionDays = 0.25f;
    public float WaterPerSecond = 0.5f;

    public float Moisture01;
    public float LastWaterDistance = 99f;
    public double PrevTimeMoistureUpdated = -1;

    public bool IsVisiblyMoist => Moisture01 > VisibleThreshold;

    public void SetRules
        (float visibleThreshold
        ,int waterSearchRadius
        ,float minRetentionDays
        ,float waterPerSecond
        )
    {
        VisibleThreshold = GameMath.Clamp(visibleThreshold, 0f, 1f);
        WaterSearchRadius = Math.Max(1, waterSearchRadius);
        MinRetentionDays = Math.Max(0.01f, minRetentionDays);
        WaterPerSecond = Math.Max(0f, waterPerSecond);
    }

    public void Reset(float moisture01 = 1f)
    {
        Moisture01 = GameMath.Clamp(moisture01, 0f, 1f);
        LastWaterDistance = 99f;
        PrevTimeMoistureUpdated = -1;
    }

    public void Water(float dt) => Moisture01 = Math.Min(1f, Moisture01 + dt * WaterPerSecond);

    public bool Tick
        (IWorldAccessor World
        ,BlockPos pos
        ,float supportRetentionDays
        )
    {
        if (PrevTimeMoistureUpdated < 0)
        {
            PrevTimeMoistureUpdated = World.Calendar.TotalHours;
            return false;
        }

        double hoursPassed = World.Calendar.TotalHours - PrevTimeMoistureUpdated;
        if (hoursPassed <= 0)
            return false;

        float waterDistance = NearbyWaterDistance(World.BlockAccessor, pos, WaterSearchRadius, out bool deferred);
        if (deferred)
            return false;

        bool dirty = false;

        PrevTimeMoistureUpdated = World.Calendar.TotalHours;
        if (!LastWaterDistance.Approx(waterDistance))
        {   LastWaterDistance = waterDistance;
            dirty = true;
        }

        float minMoisture = GameMath.Clamp(1f - waterDistance / WaterSearchRadius, 0, 1);
        float totalRetentionHours = Math.Max(MinRetentionDays, supportRetentionDays) * Math.Max(1f, World.Calendar.HoursPerDay);
        float newMoisture01 = Math.Max(minMoisture, Moisture01 - (float)hoursPassed / totalRetentionHours);
        if (!Moisture01.Approx(newMoisture01))
        {   Moisture01 = GameMath.Clamp(newMoisture01, 0, 1);
            dirty = true;
        }

        return dirty;
    }

    public static float NearbyWaterDistance(IBlockAccessor blockAccessor, BlockPos pos, int radius, out bool deferred)
    {
        float waterDistance = 99f;
        bool chunkMissing = false;

        blockAccessor.SearchFluidBlocks
            (new BlockPos(pos.X - radius, pos.Y, pos.Z - radius)
            ,new BlockPos(pos.X + radius, pos.Y, pos.Z + radius)
            ,(block, blockPos) =>
            {   if (block.LiquidCode == "water")
                    waterDistance = Math.Min(waterDistance, Math.Max(Math.Abs(blockPos.X - pos.X), Math.Abs(blockPos.Z - pos.Z)));
                return true;
            }
            ,(cx, cy, cz) => chunkMissing = true
            );

        deferred = chunkMissing;
        return deferred ? 99f : waterDistance;
    }

    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetFloat("moisture01", Moisture01);
        tree.SetFloat("lastWaterDistance", LastWaterDistance);
        tree.SetDouble("prevTimeMoistureUpdated", PrevTimeMoistureUpdated);

        tree.SetFloat("visibleThreshold", VisibleThreshold);
        tree.SetInt("waterSearchRadius", WaterSearchRadius);
        tree.SetFloat("minRetentionDays", MinRetentionDays);
        tree.SetFloat("waterPerSecond", WaterPerSecond);
    }

    public void FromTreeAttributes(ITreeAttribute tree)
    {
        Moisture01 = tree.GetFloat("moisture01");
        LastWaterDistance = tree.GetFloat("lastWaterDistance", 99f);
        PrevTimeMoistureUpdated = tree.GetDouble("prevTimeMoistureUpdated", -1);

        SetRules
            (tree.GetFloat("visibleThreshold", tree.GetFloat("moistVisibleThreshold", VisibleThreshold))
            ,tree.GetInt("waterSearchRadius", WaterSearchRadius)
            ,tree.GetFloat("minRetentionDays", MinRetentionDays)
            ,tree.GetFloat("waterPerSecond", WaterPerSecond)
            );
    }
    #endregion
}
