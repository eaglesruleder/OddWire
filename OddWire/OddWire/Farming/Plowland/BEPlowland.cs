using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

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
    
    #region PlayerActions
    public void Water(float dt)
    {
        float newMoisture01 = Math.Min(1f, Moisture01 + dt / 2f);
        if (newMoisture01.Approx(Moisture01))
            return;
        Moisture01 = newMoisture01;
        MarkDirty(true);
    }

    public bool TryFertilize(ItemSlot slot, out int consumed)
    {
        consumed = 0;

        #region if(!slot.Attributes["fertilizerProps"]) return;
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
        Reset();
        UpdateSupport();

        Moisture01 = GameMath.Clamp(moisture01 ?? (SupportIsValid ? 1f : 0f), 0f, 1f);

        #region Nutrients = Clamp(nutrients ?? OriginalNutrients, 0, MaxFertilizedNutrient)
        float original = FertilitySet.Value(Block);
        for (int i = 0; i < 3; i++)
        {
            OriginalNutrients[i] = GameMath.Clamp(original, 0f, Settings.MaxFertilizedNutrient);
            Nutrients[i] = nutrients?.Length > i
            ?   GameMath.Clamp(nutrients[i], 0f, Settings.MaxFertilizedNutrient)
            :   OriginalNutrients[i];
            SlowReleaseNutrients[i] = 0f;
        }
        #endregion
    }

    private void Reset()
    {
        Moisture01 = 1;
        LastWaterDistance = 99f;
        PrevTimeMoistureUpdated = -1;
        PrevTimeNutrientsUpdated = -1;
    }

    private void ResetSupport()
    {
        SupportCode = null;
        SupportFertilityCode = null;
        SupportRetentionDays = 0;
        SupportIsValid = false;
    }

    private void OnEvery3Seconds(float dt)
    {
        if (Api?.Side != EnumAppSide.Server)
            return;

        if (UpdateSupport()
        |   UpdateMoisture()
        |   UpdateNutrients()
            )
            MarkDirty(true);
    }

    private bool UpdateSupport()
    {
        Block supportBlock = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());
        if (supportBlock is null || supportBlock.Id == 0
        ||  supportBlock.IsLiquid()
        ||  supportBlock.BlockMaterial != EnumBlockMaterial.Soil
           )
        {
            if (SupportCode is null && SupportFertilityCode is null)
                return false;
            ResetSupport();
            return true;
        }

        string? newSupportCode = supportBlock.Code?.ToShortString();
        string? newSupportFertilityCode = FertilitySet.GetCode(supportBlock);

        if (newSupportCode == SupportCode
        &&  newSupportFertilityCode == SupportFertilityCode
           )
            return false;

        SupportCode = newSupportCode;
        SupportFertilityCode = newSupportFertilityCode;
        SupportIsValid = true;
        SupportRetentionDays = Settings.DefaultRetentionDays * (FertilitySet.Value(SupportFertilityCode) / 100f);
        return true;
    }
    
    private bool UpdateMoisture()
    {
        #region if (hoursPassed <= 0) return false;
        double totalHours = Api.World.Calendar.TotalHours;
        if (PrevTimeMoistureUpdated < 0)
        {
            PrevTimeMoistureUpdated = totalHours;
            return false;
        }
        
        double hoursPassed = totalHours - PrevTimeMoistureUpdated;
        if (hoursPassed <= 0)
            return false;
        PrevTimeMoistureUpdated = totalHours;
        #endregion
        
        float waterDistance = ResolveNearbyWaterDistance(out bool deferred);
        if (deferred)
            return false;

        LastWaterDistance = waterDistance;

        float totalRetentionHours = Math.Max(0.25f, SupportRetentionDays) * Api.World.Calendar.HoursPerDay;
        float newMoisture01 = Math.Max(ResolveMinMoisture(waterDistance), Moisture01 - (float)hoursPassed / totalRetentionHours);
        if (newMoisture01.Approx(Moisture01))
            return false;
        
        if (Moisture01 > Settings.MoistVisibleThreshold
        !=  newMoisture01 > Settings.MoistVisibleThreshold
            )
            TrySwapMoistureVariant(newMoisture01 > Settings.MoistVisibleThreshold);
        
        Moisture01 = newMoisture01;
        return true;
    }
    
    private bool UpdateNutrients()
    {
        #region if (hoursPassed <= 0) return false;
        double totalHours = Api.World.Calendar.TotalHours;
        if (PrevTimeNutrientsUpdated < 0)
        {
            PrevTimeNutrientsUpdated = totalHours;
            return false;
        }
        
        double hoursPassed = totalHours - PrevTimeNutrientsUpdated;
        if (hoursPassed <= 0)
            return false;
        PrevTimeNutrientsUpdated = totalHours;
        #endregion
        
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

            changed |= !Nutrients[i].Approx(prev);
        }
        return changed;
    }
    #endregion
    
    #region WaterSearch
    private float ResolveNearbyWaterDistance(out bool deferred)
    {
        float waterDistance = 99f;

        bool chunkMissing = false;
        Api.World.BlockAccessor.SearchFluidBlocks
            (new BlockPos(Pos.X - Settings.WaterSearchRadius, Pos.Y, Pos.Z - Settings.WaterSearchRadius)
            ,new BlockPos(Pos.X + Settings.WaterSearchRadius, Pos.Y, Pos.Z + Settings.WaterSearchRadius)
            ,(block, pos) =>
            {
                if (block.LiquidCode == "water")
                    waterDistance = Math.Min(waterDistance, Math.Max(Math.Abs(pos.X - Pos.X), Math.Abs(pos.Z - Pos.Z)));
                return true;
            }
            ,(cx, cy, cz) => chunkMissing = true
            );
        deferred = chunkMissing;
        
        return deferred ? 99f : waterDistance;
    }

    private float ResolveMinMoisture(float waterDistance) =>
        SupportIsValid
    ?   GameMath.Clamp(1f - waterDistance / Settings.WaterSearchRadius, 0, 1)
    :   0;

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
    
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine($"Moisture: {(Moisture01 * 100f):0}%");
        dsc.AppendLine($"Support: {SupportCode ?? "none"}");
        dsc.AppendLine($"Retention: {SupportRetentionDays:0.0} days");
        dsc.AppendLine($"NPK: {MathF.Round(Nutrients[0], 1)} / {MathF.Round(Nutrients[1], 1)} / {MathF.Round(Nutrients[2], 1)}");
    }

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
