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

    public readonly int[] OriginalNutrients = new int[3];
    public readonly float[] Nutrients = new float[3];
    public readonly float[] SlowReleaseNutrients = new float[3];

    public PlowlandSupportModel Support;
    public float LastWaterDistance = 99f;

    public double PrevTimeMoistureUpdated = -1;
    public double PrevTimeNutrientsUpdated = -1;
    #endregion

    #region Setup
    public void ResetOnPlowed(Block targetBlock, Block supportBlock)
    {
        #region Resolve support and fertility seed
        Support = ResolveSupport(supportBlock);
        int[] original = ResolveOriginalNutrients(targetBlock, supportBlock);
        #endregion

        #region Reset nutrients from seed
        for (int i = 0; i < 3; i++)
        {
            OriginalNutrients[i] = original[i];
            Nutrients[i] = original[i];
            SlowReleaseNutrients[i] = 0;
        }
        #endregion

        #region Reset dynamic state
        Moisture01 = Support.WaterQuality01;
        LastWaterDistance = 99f;
        PrevTimeMoistureUpdated = -1;
        PrevTimeNutrientsUpdated = -1;
        #endregion
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

        #region Update visible block state
        changed |= UpdateBlockState(be);
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

    #region UpdateVisualBlock
    private bool UpdateBlockState(BlockEntityPlowland be)
    {
        #region Resolve target block code
        Block currentBlock = be.Api.World.BlockAccessor.GetBlock(be.Pos);
        string state = Moisture01 > Settings.MoistVisibleThreshold ? PlowlandSettings.StateMoist : PlowlandSettings.StateDry;
        string fertilityCode = ResolveVisibleFertilityCode(OriginalNutrients);

        AssetLocation targetCode = ResolvePlowlandCode(currentBlock.Code.Domain, state, fertilityCode);
        Block? targetBlock = be.Api.World.GetBlock(targetCode);
        if (targetBlock == null || targetBlock.Id == currentBlock.Id)
            return false;
        #endregion

        #region Exchange block variant
        be.Api.World.BlockAccessor.ExchangeBlock(targetBlock.Id, be.Pos);
        be.Api.World.BlockAccessor.MarkBlockDirty(be.Pos);
        return true;
        #endregion
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

        if (targetBlock is BlockPlowland)
            return false;

        if (targetBlock is BlockFarmland)
            return true;

        return targetBlock.BlockMaterial == EnumBlockMaterial.Soil;
    }

    public static PlowlandSupportModel ResolveSupport(Block supportBlock)
    {
        if (supportBlock is null
        ||  supportBlock.Id == 0
        ||  supportBlock.IsLiquid()
            )
            return new PlowlandSupportModel(false, null, 0f, 0f, PlowlandSettings.FertilityLow);

        string? supportCode = supportBlock.Code?.ToShortString();
        string fertilityCode = ResolveFertilityCode(supportBlock);

        if (supportBlock is BlockFarmland)
            return new PlowlandSupportModel(true, supportCode, 4.5f, 1.00f, fertilityCode);

        return supportBlock.BlockMaterial switch
        {
            EnumBlockMaterial.Soil   => new PlowlandSupportModel(true, supportCode, 4.0f, 1.00f, fertilityCode),
            EnumBlockMaterial.Sand   => new PlowlandSupportModel(true, supportCode, 2.5f, 0.65f, fertilityCode),
            EnumBlockMaterial.Gravel => new PlowlandSupportModel(true, supportCode, 1.5f, 0.35f, fertilityCode),
            _                        => new PlowlandSupportModel(true, supportCode, 3.0f, 0.50f, fertilityCode),
        };
    }

    public static int[] ResolveOriginalNutrients(Block targetBlock, Block supportBlock)
    {
        string fertilityCode = ResolveFertilityCode(targetBlock);
        
        if (!Settings.FertilityByCode.ContainsKey(fertilityCode))
            fertilityCode = ResolveFertilityCode(supportBlock);
        
        if (!Settings.FertilityByCode.ContainsKey(fertilityCode))
            fertilityCode = PlowlandSettings.FertilityLow;

        int fertility = Settings.FertilityByCode[fertilityCode];
        return new[] { fertility, fertility, fertility };
    }

    public static string ResolveVisibleFertilityCode(int[] nutrients)
    {
        if (nutrients is null
        ||  nutrients.Length < 3
            )
            return PlowlandSettings.FertilityLow;

        float average = (nutrients[0] + nutrients[1] + nutrients[2]) / 3f;

        if (average <= Settings.FertilityByCode[PlowlandSettings.FertilityVeryLow]) return PlowlandSettings.FertilityVeryLow;
        if (average <= Settings.FertilityByCode[PlowlandSettings.FertilityLow])     return PlowlandSettings.FertilityLow;
        if (average <= Settings.FertilityByCode[PlowlandSettings.FertilityMedium])  return PlowlandSettings.FertilityMedium;
        if (average <= Settings.FertilityByCode[PlowlandSettings.FertilityCompost]) return PlowlandSettings.FertilityCompost;
        return PlowlandSettings.FertilityHigh;
    }

    public static string ResolveFertilityCode(Block? block)
    {
        string? code = block?.LastCodePart();
        if (code != null && Settings.FertilityByCode.ContainsKey(code))
            return code;

        if (block is BlockFarmland)
        {
            code = block.LastCodePart(0);
            if (code != null && Settings.FertilityByCode.ContainsKey(code))
                return code;
        }

        return PlowlandSettings.FertilityLow;
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

        tree.SetInt("origN", OriginalNutrients[0]);
        tree.SetInt("origP", OriginalNutrients[1]);
        tree.SetInt("origK", OriginalNutrients[2]);

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
            tree.GetString("supportFertilityCode") ?? PlowlandSettings.FertilityLow
        );

        OriginalNutrients[0] = tree.GetInt("origN");
        OriginalNutrients[1] = tree.GetInt("origP");
        OriginalNutrients[2] = tree.GetInt("origK");

        Nutrients[0] = tree.GetFloat("nutrN");
        Nutrients[1] = tree.GetFloat("nutrP");
        Nutrients[2] = tree.GetFloat("nutrK");

        SlowReleaseNutrients[0] = tree.GetFloat("slowN");
        SlowReleaseNutrients[1] = tree.GetFloat("slowP");
        SlowReleaseNutrients[2] = tree.GetFloat("slowK");
    }
    #endregion
}
