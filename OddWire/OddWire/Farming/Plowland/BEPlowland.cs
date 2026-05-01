using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class BlockEntityPlowland : BlockEntity, IWaterable
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
    public string? SupportFertilityCode;

    public float LastWaterDistance = 99f;

    public double PrevTimeMoistureUpdated = -1;
    public double PrevTimeNutrientsUpdated = -1;
    #endregion

    #region Lifecycle
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery3Seconds, 3000);
    }

    public void Initialise
        (float[]? nutrients = null
        ,float? moisture01 = null
        )
    {
        #region Reset support from block below
        if (Api is null)
            UpdateSupportFromBlock(null);
        else
            UpdateSupportFromBlock(Api.World.BlockAccessor.GetBlock(Pos.DownCopy()));
        #endregion

        #region Reset nutrients from plowland fertility
        float original = FertilitySet.Value(Block);
        for (int i = 0; i < 3; i++)
        {
            OriginalNutrients[i] = GameMath.Clamp(original, 0f, Settings.MaxFertilizedNutrient);
            Nutrients[i] = nutrients != null && nutrients.Length > i
                ? GameMath.Clamp(nutrients[i], 0f, Settings.MaxFertilizedNutrient)
                : OriginalNutrients[i];
            SlowReleaseNutrients[i] = 0f;
        }
        #endregion

        #region Reset dynamic state
        Moisture01 = GameMath.Clamp(moisture01 ?? (SupportIsValid ? 1f : 0f), 0f, 1f);
        LastWaterDistance = 99f;
        PrevTimeMoistureUpdated = -1;
        PrevTimeNutrientsUpdated = -1;
        #endregion
    }

    public void SetOriginalFertility(string fertilityCode)
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

    private void OnEvery3Seconds(float dt)
    {
        if (Update())
            MarkDirty(true);
    }
    #endregion

    #region Tick
    private bool Update()
    {
        bool changed = false;

        #region Require valid world state
        if (Api?.Side != EnumAppSide.Server)
            return false;
        #endregion

        #region Refresh support from the block below
        changed |= RefreshSupport();
        #endregion

        #region Update moisture and nutrients
        changed |= UpdateMoisture();
        changed |= UpdateNutrients();
        #endregion

        return changed;
    }
    #endregion

    #region PlayerActions
    public void Water(float dt)
    {
        float prev = Moisture01;
        Moisture01 = Math.Min(1f, Moisture01 + dt / 2f);
        if (Math.Abs(Moisture01 - prev) > 0.001f)
            MarkDirty(true);
    }

    public bool TryFertilize(ItemSlot slot, out int consumed)
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

        MarkDirty(true);
        return true;
    }
    #endregion

    #region BlockInfo
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine($"Moisture: {(Moisture01 * 100f):0}%");
        dsc.AppendLine($"Support: {SupportCode ?? "none"}");
        dsc.AppendLine($"Retention: {SupportRetentionDays:0.0} days");
        dsc.AppendLine($"NPK: {MathF.Round(Nutrients[0], 1)} / {MathF.Round(Nutrients[1], 1)} / {MathF.Round(Nutrients[2], 1)}");
    }
    #endregion

    #region UpdateMoisture
    private bool UpdateMoisture()
    {
        double totalHours = Api.World.Calendar.TotalHours;
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
        float waterDistance = ResolveNearbyWaterDistance(out bool deferred);
        if (deferred)
            return false;

        LastWaterDistance = waterDistance;
        float minMoisture = ResolveMinMoisture(waterDistance);
        #endregion

        #region Dry toward support retention
        float prev = Moisture01;
        float totalRetentionHours = Math.Max(0.25f, SupportRetentionDays) * Api.World.Calendar.HoursPerDay;
        Moisture01 = Math.Max(minMoisture, Moisture01 - (float)hoursPassed / totalRetentionHours);
        #endregion

        #region Swap block variant when moisture crosses visible threshold
        if ((prev > Settings.MoistVisibleThreshold) != (Moisture01 > Settings.MoistVisibleThreshold))
            TrySwapMoistureVariant(Moisture01 > Settings.MoistVisibleThreshold);
        #endregion

        return Math.Abs(Moisture01 - prev) > 0.001f;
    }
    #endregion

    #region UpdateNutrients
    private bool UpdateNutrients()
    {
        double totalHours = Api.World.Calendar.TotalHours;
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
    public static bool IsValidSupportBlock(Block? supportBlock) =>
        TryResolveSupportState
            (supportBlock
            ,out _
            ,out _
            ,out _
            );

    private bool RefreshSupport()
    {
        Block supportBlock = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());

        string? prevSupportCode = SupportCode;
        bool prevSupportIsValid = SupportIsValid;
        float prevSupportRetentionDays = SupportRetentionDays;
        string? prevSupportFertilityCode = SupportFertilityCode;

        UpdateSupportFromBlock(supportBlock);

        return prevSupportCode != SupportCode
            ||  prevSupportIsValid != SupportIsValid
            ||  Math.Abs(prevSupportRetentionDays - SupportRetentionDays) > 0.001f
            ||  prevSupportFertilityCode != SupportFertilityCode;
    }

    private void UpdateSupportFromBlock(Block? supportBlock)
    {
        if (TryResolveSupportState
            (supportBlock
            ,out string? supportCode
            ,out float supportRetentionDays
            ,out string? supportFertilityCode
            ))
        {
            SupportCode = supportCode;
            SupportIsValid = true;
            SupportRetentionDays = supportRetentionDays;
            SupportFertilityCode = supportFertilityCode;
            return;
        }

        SupportCode = null;
        SupportIsValid = false;
        SupportRetentionDays = Settings.DefaultRetentionDays;
        SupportFertilityCode = null;
    }

    private static bool TryResolveSupportState
        (Block? supportBlock
        ,out string? supportCode
        ,out float supportRetentionDays
        ,out string? supportFertilityCode
        )
    {
        supportCode = null;
        supportRetentionDays = Settings.DefaultRetentionDays;
        supportFertilityCode = null;

        if (supportBlock is null
        ||  supportBlock.Id == 0
        ||  supportBlock.IsLiquid()
            )
            return false;

        if (supportBlock.BlockMaterial != EnumBlockMaterial.Soil)
            return false;

        supportFertilityCode = FertilitySet.GetCode(supportBlock);
        float fertilityValue = FertilitySet.Value(supportFertilityCode);

        supportCode = supportBlock.Code?.ToShortString();
        supportRetentionDays = Settings.DefaultRetentionDays * (fertilityValue / 100f);
        return true;
    }
    #endregion

    #region WaterSearch
    private float ResolveNearbyWaterDistance(out bool deferred)
    {
        float waterDistance = 99f;

        bool chunkMissing = false;
        Api.World.BlockAccessor.SearchFluidBlocks(
            new BlockPos(Pos.X - Settings.WaterSearchRadius, Pos.Y, Pos.Z - Settings.WaterSearchRadius),
            new BlockPos(Pos.X + Settings.WaterSearchRadius, Pos.Y, Pos.Z + Settings.WaterSearchRadius),
            (block, pos) =>
            {
                if (block.LiquidCode == "water")
                    waterDistance = Math.Min(waterDistance, Math.Max(Math.Abs(pos.X - Pos.X), Math.Abs(pos.Z - Pos.Z)));
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

        return GameMath.Clamp(1f - waterDistance / Settings.WaterSearchRadius, 0f, 1f);
    }

    private void TrySwapMoistureVariant(bool isMoist)
    {
        string? fertilityCode = FertilitySet.GetCode(Block);
        if (fertilityCode is null
        ||  Block.Code is null
            )
            return;

        string moistKey = isMoist ? Settings.StateMoist : Settings.StateDry;
        AssetLocation newCode = new(Block.Code.Domain, $"plowland-{moistKey}-{fertilityCode}");
        Block newBlock = Api.World.GetBlock(newCode);
        if (newBlock is not null
        &&  newBlock.Id != 0
            )
            Api.World.BlockAccessor.ExchangeBlock(newBlock.BlockId, Pos);
    }
    #endregion

    #region Persistence
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetFloat("moisture01", Moisture01);
        tree.SetFloat("lastWaterDistance", LastWaterDistance);
        tree.SetDouble("prevTimeMoistureUpdated", PrevTimeMoistureUpdated);
        tree.SetDouble("prevTimeNutrientsUpdated", PrevTimeNutrientsUpdated);

        tree.SetString("supportCode", SupportCode);
        tree.SetBool("supportIsValid", SupportIsValid);
        tree.SetFloat("supportRetentionDays", SupportRetentionDays);
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

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        Moisture01 = tree.GetFloat("moisture01");
        LastWaterDistance = tree.GetFloat("lastWaterDistance");
        PrevTimeMoistureUpdated = tree.GetDouble("prevTimeMoistureUpdated");
        PrevTimeNutrientsUpdated = tree.GetDouble("prevTimeNutrientsUpdated");

        SupportCode = tree.GetString("supportCode");
        SupportIsValid = tree.GetBool("supportIsValid");
        SupportRetentionDays = tree.GetFloat("supportRetentionDays");
        SupportFertilityCode = tree.GetString("supportFertilityCode");

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
