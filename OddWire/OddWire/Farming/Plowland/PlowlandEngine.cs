using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class PlowlandEngine
{
    private static readonly PlowlandSettings Settings = new();

    #region StoredState
    public float Moisture01;

    public readonly float[] OriginalNutrients = new float[3];
    public readonly float[] Nutrients = new float[3];
    public readonly float[] SlowReleaseNutrients = new float[3];

    public PlowlandSupportModel Support;
    public float LastWaterDistance = 99f;

    public double PrevTimeMoistureUpdated = -1;
    public double PrevTimeNutrientsUpdated = -1;
    #endregion

    #region Setup
    public void ResetOnPlowed(Block targetBlock, Block supportBlock, float[]? nutrients = null, float? moisture01 = null)
    {
        #region Resolve support and fertility seed
        Support = ResolveSupport(supportBlock);
        float[] original = ResolveOriginalNutrients(targetBlock, supportBlock);
        #endregion

        #region Reset nutrients from seed
        for (int i = 0; i < 3; i++)
        {
            OriginalNutrients[i] = original[i];
            Nutrients[i] = nutrients != null && nutrients.Length > i
                ? GameMath.Clamp(nutrients[i], 0f, Settings.MaxFertilizedNutrient)
                : original[i];
            SlowReleaseNutrients[i] = 0f;
        }
        #endregion

        #region Reset dynamic state
        Moisture01 = GameMath.Clamp(moisture01 ?? Support.WaterQuality01, 0f, 1f);
        LastWaterDistance = 99f;
        PrevTimeMoistureUpdated = -1;
        PrevTimeNutrientsUpdated = -1;
        #endregion
    }

    public void SetFertility(string fertilityCode)
    {
        if (!FertilitySet.Contains(fertilityCode))
            return;

        float fertility = FertilitySet.Value(fertilityCode);

        for (int i = 0; i < 3; i++)
        {
            OriginalNutrients[i] = fertility;
            Nutrients[i] = Math.Min(Nutrients[i], fertility);
        }
    }
    #endregion

    #region Tick
    public bool Update(BlockEntityPlowland be)
    {
        bool changed = false;

        #region Require valid world state
        if (be.Api?.Side != EnumAppSide.Server)
            return false;
        #endregion

        #region Refresh support from the block below
        changed |= RefreshSupport(be);
        #endregion

        #region Update moisture and nutrients
        changed |= UpdateMoisture(be);
        changed |= UpdateNutrients(be);
        #endregion

        return changed;
    }
    #endregion

    #region PlayerActions
    public bool Water(BlockEntityPlowland be, float dt)
    {
        float prev = Moisture01;
        Moisture01 = Math.Min(1f, Moisture01 + dt / 2f);
        return Math.Abs(Moisture01 - prev) > 0.001f;
    }

    public bool TryFertilize(BlockEntityPlowland be, ItemSlot slot, out int consumed)
    {
        consumed = 0;

        #region Require held item fertilizer props
        JsonObject? obj = slot.Itemstack?.Collectible?.Attributes?["fertilizerProps"];
        if (obj == null || !obj.Exists)
            return false;

        FertilizerProps? props = obj.AsObject<FertilizerProps>();
        if (props == null)
            return false;
        #endregion

        #region Apply slow release pool
        SlowReleaseNutrients[0] += Math.Min(Math.Max(0f, Settings.MaxFertilizedNutrient - SlowReleaseNutrients[0]), props.N);
        SlowReleaseNutrients[1] += Math.Min(Math.Max(0f, Settings.MaxFertilizedNutrient - SlowReleaseNutrients[1]), props.P);
        SlowReleaseNutrients[2] += Math.Min(Math.Max(0f, Settings.MaxFertilizedNutrient - SlowReleaseNutrients[2]), props.K);
        consumed = 1;
        #endregion

        return true;
    }
    #endregion

    #region UpdateMoisture
    private bool UpdateMoisture(BlockEntityPlowland be)
    {
        double totalHours = be.Api.World.Calendar.TotalHours;
        if (PrevTimeMoistureUpdated < 0)
        {
            PrevTimeMoistureUpdated = totalHours;
            return false;
        }

        #region Resolve elapsed time
        double hoursPassed = totalHours - PrevTimeMoistureUpdated;
        if (hoursPassed <= 0)
            return false;

        PrevTimeMoistureUpdated = totalHours;
        #endregion

        #region Resolve nearby water floor
        float waterDistance = ResolveNearbyWaterDistance(be, out bool deferred);
        if (deferred)
            return false;

        LastWaterDistance = waterDistance;
        float minMoisture = ResolveMinMoisture(waterDistance, Support);
        #endregion

        #region Dry toward support retention
        float prev = Moisture01;
        float totalRetentionHours = Math.Max(0.25f, Support.RetentionDays) * be.Api.World.Calendar.HoursPerDay;
        Moisture01 = Math.Max(minMoisture, Moisture01 - (float)hoursPassed / totalRetentionHours);
        #endregion

        return Math.Abs(Moisture01 - prev) > 0.001f;
    }
    #endregion

    #region UpdateNutrients
    private bool UpdateNutrients(BlockEntityPlowland be)
    {
        double totalHours = be.Api.World.Calendar.TotalHours;
        if (PrevTimeNutrientsUpdated < 0)
        {
            PrevTimeNutrientsUpdated = totalHours;
            return false;
        }

        #region Resolve elapsed time
        double hoursPassed = totalHours - PrevTimeNutrientsUpdated;
        if (hoursPassed <= 0)
            return false;

        PrevTimeNutrientsUpdated = totalHours;
        #endregion

        #region Recover toward original and release fertilizer
        bool changed = false;
        for (int i = 0; i < 3; i++)
        {
            float prev = Nutrients[i];

            if (Nutrients[i] < OriginalNutrients[i])
                Nutrients[i] = Math.Min(OriginalNutrients[i], Nutrients[i] + Settings.FertilityRecoveryPerTick * (float)hoursPassed / 3f);

            if (SlowReleaseNutrients[i] > 0)
            {
                float release = Math.Min(Settings.FertilizerReleasePerTick * (float)hoursPassed / 3f, SlowReleaseNutrients[i]);
                Nutrients[i] = Math.Min(Settings.MaxFertilizedNutrient, Nutrients[i] + release);
                SlowReleaseNutrients[i] = Math.Max(0f, SlowReleaseNutrients[i] - release);
            }

            changed |= Math.Abs(Nutrients[i] - prev) > 0.001f;
        }
        #endregion

        return changed;
    }
    #endregion


    #region Support
    private bool RefreshSupport(BlockEntityPlowland be)
    {
        Block supportBlock = be.Api.World.BlockAccessor.GetBlock(be.Pos.DownCopy());
        PlowlandSupportModel nextSupport = ResolveSupport(supportBlock);
        if (nextSupport.Equals(Support))
            return false;

        Support = nextSupport;
        return true;
    }
    #endregion

    #region Rules
    public static bool CanPlowTarget(Block targetBlock)
    {
        if (targetBlock is null
        ||  targetBlock.Id == 0
        ||  targetBlock.IsLiquid()
            )
            return false;

        if (targetBlock is BlockPlowland
        ||  targetBlock is BlockFarmland
            )
            return true;

        return targetBlock.BlockMaterial == EnumBlockMaterial.Soil;
    }

    public static PlowlandSupportModel ResolveSupport(Block supportBlock)
    {
        if (supportBlock is null
        ||  supportBlock.Id == 0
        ||  supportBlock.IsLiquid()
            )
            return new PlowlandSupportModel(false, null, 0f, 0f, PlowlandSettings.DefaultFertility);

        string? supportCode = supportBlock.Code?.ToShortString();
        string fertilityCode = FertilitySet.GetCode(supportBlock)!;

        if (supportBlock is BlockFarmland)
            return new PlowlandSupportModel(true, supportCode, 4.5f, 1.00f, fertilityCode);

        if (supportBlock is BlockPlowland)
            return new PlowlandSupportModel(true, supportCode, 4.25f, 1.00f, fertilityCode);

        if (supportBlock.BlockMaterial == EnumBlockMaterial.Soil)
            return new PlowlandSupportModel(true, supportCode, 4.0f, 1.00f, fertilityCode);

        return new PlowlandSupportModel(false, null, 0f, 0f, PlowlandSettings.DefaultFertility);
    }

    public static float[] ResolveOriginalNutrients(Block targetBlock, Block supportBlock)
    {
        string? fertilityCode = targetBlock?.LastCodePart();
        if (!FertilitySet.Contains(fertilityCode))
            fertilityCode = supportBlock?.LastCodePart();

        return FertilitySet.MakeUniformNutrients(fertilityCode);
    }


    public static AssetLocation ResolvePlowlandCode(string domain, string state, string fertilityCode)
    {
        return new AssetLocation(domain, $"plowland-{state}-{fertilityCode}");
    }

    private static float ResolveMinMoisture(float waterDistance, PlowlandSupportModel support)
    {
        if (!support.IsValid)
            return 0f;

        float waterFloor = GameMath.Clamp(1f - waterDistance / Settings.WaterSearchRadius, 0f, 1f);
        return waterFloor * support.WaterQuality01;
    }
    #endregion

    #region WaterSearch
    private float ResolveNearbyWaterDistance(BlockEntityPlowland be, out bool deferred)
    {
        float waterDistance = 99f;

        bool chunkMissing = false;
        be.Api.World.BlockAccessor.SearchFluidBlocks(
            new BlockPos(be.Pos.X - Settings.WaterSearchRadius, be.Pos.Y, be.Pos.Z - Settings.WaterSearchRadius),
            new BlockPos(be.Pos.X + Settings.WaterSearchRadius, be.Pos.Y, be.Pos.Z + Settings.WaterSearchRadius),
            (block, pos) =>
            {
                if (block.LiquidCode == "water")
                    waterDistance = Math.Min(waterDistance, Math.Max(Math.Abs(pos.X - be.Pos.X), Math.Abs(pos.Z - be.Pos.Z)));
                return true;
            },
            (cx, cy, cz) => chunkMissing = true
        );

        deferred = chunkMissing;
        return deferred ? 99f : waterDistance;
    }
    #endregion

    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetFloat("moisture01", Moisture01);
        tree.SetFloat("lastWaterDistance", LastWaterDistance);
        tree.SetDouble("prevTimeMoistureUpdated", PrevTimeMoistureUpdated);
        tree.SetDouble("prevTimeNutrientsUpdated", PrevTimeNutrientsUpdated);

        tree.SetString("supportCode", Support.SupportCode);
        tree.SetBool("supportIsValid", Support.IsValid);
        tree.SetFloat("supportRetentionDays", Support.RetentionDays);
        tree.SetFloat("supportWaterQuality01", Support.WaterQuality01);
        tree.SetString("supportFertilityCode", Support.FertilityCode);

        tree.SetFloat("origN", OriginalNutrients[0]);
        tree.SetFloat("origP", OriginalNutrients[1]);
        tree.SetFloat("origK", OriginalNutrients[2]);

        tree.SetFloat("nutrN", Nutrients[0]);
        tree.SetFloat("nutrP", Nutrients[1]);
        tree.SetFloat("nutrK", Nutrients[2]);

        tree.SetFloat("slowN", SlowReleaseNutrients[0]);
        tree.SetFloat("slowP", SlowReleaseNutrients[1]);
        tree.SetFloat("slowK", SlowReleaseNutrients[2]);
    }

    public void FromTreeAttributes(ITreeAttribute tree)
    {
        Moisture01 = tree.GetFloat("moisture01");
        LastWaterDistance = tree.GetFloat("lastWaterDistance");
        PrevTimeMoistureUpdated = tree.GetDouble("prevTimeMoistureUpdated");
        PrevTimeNutrientsUpdated = tree.GetDouble("prevTimeNutrientsUpdated");

        Support = new PlowlandSupportModel(
            tree.GetBool("supportIsValid"),
            tree.GetString("supportCode"),
            tree.GetFloat("supportRetentionDays"),
            tree.GetFloat("supportWaterQuality01"),
            tree.GetString("supportFertilityCode") ?? PlowlandSettings.DefaultFertility
        );

        OriginalNutrients[0] = tree.GetFloat("origN");
        OriginalNutrients[1] = tree.GetFloat("origP");
        OriginalNutrients[2] = tree.GetFloat("origK");

        Nutrients[0] = tree.GetFloat("nutrN");
        Nutrients[1] = tree.GetFloat("nutrP");
        Nutrients[2] = tree.GetFloat("nutrK");

        SlowReleaseNutrients[0] = tree.GetFloat("slowN");
        SlowReleaseNutrients[1] = tree.GetFloat("slowP");
        SlowReleaseNutrients[2] = tree.GetFloat("slowK");
    }
    #endregion
}
