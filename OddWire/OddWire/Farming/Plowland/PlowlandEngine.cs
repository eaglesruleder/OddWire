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

    public string? SupportCode;
    public bool SupportIsValid;
    public float SupportRetentionDays = 4f;
    public float SupportWaterQuality01 = 1f;
    public string SupportFertilityCode = PlowlandSettings.DefaultFertility;

    public float LastWaterDistance = 99f;

    public double PrevTimeMoistureUpdated = -1;
    public double PrevTimeNutrientsUpdated = -1;
    #endregion

    #region Setup
    public void Initialise
        (float[] originalNutrients
        ,float[]? nutrients = null
        ,float? moisture01 = null
        ,string? supportCode = null
        ,bool supportIsValid = false
        ,float? supportRetentionDays = null
        ,float? supportWaterQuality01 = null
        ,string? supportFertilityCode = null
        )
    {
        #region Reset support state
        SupportCode = supportCode;
        SupportIsValid = supportIsValid;
        SupportRetentionDays = Math.Max(0.25f, supportRetentionDays ?? Settings.DefaultRetentionDays);
        SupportWaterQuality01 = GameMath.Clamp(supportWaterQuality01 ?? 1f, 0f, 1f);
        SupportFertilityCode = FertilitySet.Contains(supportFertilityCode)
            ? supportFertilityCode!
            : PlowlandSettings.DefaultFertility;
        #endregion

        #region Reset nutrients from seed
        for (int i = 0; i < 3; i++)
        {
            float original = originalNutrients.Length > i
                ? originalNutrients[i]
                : 0f;

            OriginalNutrients[i] = GameMath.Clamp(original, 0f, Settings.MaxFertilizedNutrient);
            Nutrients[i] = nutrients != null && nutrients.Length > i
                ? GameMath.Clamp(nutrients[i], 0f, Settings.MaxFertilizedNutrient)
                : OriginalNutrients[i];
            SlowReleaseNutrients[i] = 0f;
        }
        #endregion

        #region Reset dynamic state
        Moisture01 = GameMath.Clamp(moisture01 ?? SupportWaterQuality01, 0f, 1f);
        LastWaterDistance = 99f;
        PrevTimeMoistureUpdated = -1;
        PrevTimeNutrientsUpdated = -1;
        #endregion
    }

    public void SetFertility(string fertilityCode)
    {
        if (!FertilitySet.Contains(fertilityCode))
            return;

        SupportFertilityCode = fertilityCode;

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
        float minMoisture = ResolveMinMoisture(waterDistance);
        #endregion

        #region Dry toward support retention
        float prev = Moisture01;
        float totalRetentionHours = Math.Max(0.25f, SupportRetentionDays) * be.Api.World.Calendar.HoursPerDay;
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

        string? prevSupportCode = SupportCode;
        bool prevSupportIsValid = SupportIsValid;
        float prevSupportRetentionDays = SupportRetentionDays;
        float prevSupportWaterQuality01 = SupportWaterQuality01;
        string prevSupportFertilityCode = SupportFertilityCode;

        UpdateSupportFromBlock(supportBlock);

        return prevSupportCode != SupportCode
            ||  prevSupportIsValid != SupportIsValid
            ||  Math.Abs(prevSupportRetentionDays - SupportRetentionDays) > 0.001f
            ||  Math.Abs(prevSupportWaterQuality01 - SupportWaterQuality01) > 0.001f
            ||  prevSupportFertilityCode != SupportFertilityCode;
    }

    private void UpdateSupportFromBlock(Block supportBlock)
    {
        if (supportBlock is null
        ||  supportBlock.Id == 0
        ||  supportBlock.IsLiquid()
            )
        {
            SupportCode = null;
            SupportIsValid = false;
            SupportRetentionDays = Settings.DefaultRetentionDays;
            SupportWaterQuality01 = 0f;
            SupportFertilityCode = PlowlandSettings.DefaultFertility;
            return;
        }

        SupportCode = supportBlock.Code?.ToShortString();
        SupportIsValid = true;
        SupportWaterQuality01 = 1f;
        SupportFertilityCode = FertilitySet.GetCode(supportBlock) ?? PlowlandSettings.DefaultFertility;

        if (supportBlock is BlockFarmland)
        {
            SupportRetentionDays = 4.5f;
            return;
        }

        if (supportBlock is BlockPlowland)
        {
            SupportRetentionDays = 4.25f;
            return;
        }

        if (supportBlock.BlockMaterial == EnumBlockMaterial.Soil)
        {
            SupportRetentionDays = 4f;
            return;
        }

        SupportIsValid = false;
        SupportCode = null;
        SupportRetentionDays = Settings.DefaultRetentionDays;
        SupportWaterQuality01 = 0f;
        SupportFertilityCode = PlowlandSettings.DefaultFertility;
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

    private float ResolveMinMoisture(float waterDistance)
    {
        if (!SupportIsValid)
            return 0f;

        float waterFloor = GameMath.Clamp(1f - waterDistance / Settings.WaterSearchRadius, 0f, 1f);
        return waterFloor * SupportWaterQuality01;
    }
    #endregion

    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetFloat("moisture01", Moisture01);
        tree.SetFloat("lastWaterDistance", LastWaterDistance);
        tree.SetDouble("prevTimeMoistureUpdated", PrevTimeMoistureUpdated);
        tree.SetDouble("prevTimeNutrientsUpdated", PrevTimeNutrientsUpdated);

        tree.SetString("supportCode", SupportCode);
        tree.SetBool("supportIsValid", SupportIsValid);
        tree.SetFloat("supportRetentionDays", SupportRetentionDays);
        tree.SetFloat("supportWaterQuality01", SupportWaterQuality01);
        tree.SetString("supportFertilityCode", SupportFertilityCode);

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

        SupportCode = tree.GetString("supportCode");
        SupportIsValid = tree.GetBool("supportIsValid");
        SupportRetentionDays = tree.GetFloat("supportRetentionDays");
        SupportWaterQuality01 = tree.GetFloat("supportWaterQuality01");
        SupportFertilityCode = tree.GetString("supportFertilityCode") ?? PlowlandSettings.DefaultFertility;

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
