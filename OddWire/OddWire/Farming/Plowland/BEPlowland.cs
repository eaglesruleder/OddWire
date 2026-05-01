using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class BlockEntityPlowland : BlockEntity, IWaterable, IFarmlandBlockEntity
{
    private static readonly PlowlandSettings Settings = new();
    private readonly Moisture _moisture = new();
    private readonly NPK _npk = new();
    private readonly CropGrowth _crop = new();

    public float Moisture01 => _moisture.Moisture01;
    public NPK Nutrients => _npk;
    public float MoistureLevel { get; }
    public bool IsVisiblyMoist { get; }
    public int[] OriginalFertility { get; }

    #region IFarmlandBlockEntity
    BlockPos IFarmlandBlockEntity.Pos => Pos;
    public BlockPos UpPos { get; }
    public ITreeAttribute CropAttributes { get; } = new TreeAttribute();
    public double TotalHoursForNextStage => _crop.TotalHoursForNextStage;
    public double TotalHoursFertilityCheck => throw new NotImplementedException();

    float[] IFarmlandBlockEntity.Nutrients => _nutrients;
    #endregion

    #region StoredState
    public string? SupportCode;
    public bool SupportIsValid;
    public float SupportRetentionDays = 4f;
    public string? SupportFertilityCode;
    private float[] _nutrients;
    #endregion

    #region PlayerActions
    public void Water(float dt)
    {
        float prevMoisture = _moisture.Moisture01;
        _moisture.Water(dt);
        if(prevMoisture.Approx(_moisture.Moisture01))
            return;

        UpdateMoistureVariant();
        MarkDirty(true);
    }

    public bool TryFertilise(ItemSlot slot, out int consumed)
    {
        consumed = 0;

        #region if(!slot.Attributes["fertilizerProps"]) return;
        JsonObject? obj = slot.Itemstack?.Collectible?.Attributes?["fertilizerProps"];
        if (obj?.Exists != true)
            return false;

        FertilizerProps? props = obj.AsObject<FertilizerProps>();
        if (props == null)
            return false;
        #endregion

        Fertilise(props);
        consumed = 1;
        MarkDirty(true);
        return true;
    }

    private void Fertilise(FertilizerProps props)
    {
        _npk.AddOverTime('N', props.N);
        _npk.AddOverTime('P', props.P);
        _npk.AddOverTime('K', props.K);
    }
    #endregion

    #region Lifecycle
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        _crop.Init(Pos);

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery3Seconds, 3000);
    }

    public void Initialise
        (NPK? nutrients = null
        ,float? moisture01 = null
        )
    {
        UpdateSupport();
        
        _moisture.Reset(GameMath.Clamp(moisture01 ?? (SupportIsValid ? 1f : 0f), 0f, 1f));
        _moisture.SetRules
            (Settings.MoistVisibleThreshold
            ,Settings.WaterSearchRadius
            ,Settings.MinRetentionDays
            ,Settings.WaterPerSecond
            );
        UpdateMoistureVariant();
        
        _npk.SetRules
            (Settings.Max
            ,Settings.RecoveryPerTick
            ,Settings.ReleasePerTick
            );
        _npk.Initialise(FertilitySet.Value(Block), nutrients);
        
        _crop.SetRules(Settings.GrowthRateMul);
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

        bool dirty =
            UpdateSupport()
        ||  _npk.Tick(Api.World.Calendar.TotalHours);

        if (_moisture.Tick(Api.World, Pos, SupportRetentionDays))
        {
            UpdateMoistureVariant();
            dirty = true;
        }

        if (TickCrop())
            dirty = true;

        if (dirty)
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

    private bool UpdateMoistureVariant()
    {
        string? fertilityCode = FertilitySet.GetCode(Block);
        if (fertilityCode is null
        ||  Block.Code is null
            )
            return false;

        string moistureCode = _moisture.IsVisiblyMoist ? Settings.StateMoist : Settings.StateDry;
        AssetLocation newCode = new(Block.Code.Domain, $"plowland-{moistureCode}-{fertilityCode}");
        Block newBlock = Api.World.GetBlock(newCode);
        if (newBlock is null
        ||  newBlock.Id == 0
        ||  newBlock.Id == Block.Id
            )
            return false;

        Api.World.BlockAccessor.ExchangeBlock(newBlock.BlockId, Pos);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        return true;
    }
    
    private bool TickCrop()
    {
        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock?.CropProps is null)
            return false;

        char nutrientKey = cropBlock.CropProps.RequiredNutrient.ToString()[0];
        float growthRate  = GameMath.Clamp(_npk[nutrientKey] / 100f, 0f, 2f);

        if(!_crop.Tick
            (Api.World.Calendar.TotalHours
            ,0.0
            ,_moisture.Moisture01
            ,false
            ,Api.World
            ,this
            ,growthRate
            ,out EnumSoilNutrient consumedNutrient
            ,out float consumedAmount
            ))
            return false;

        if (consumedAmount > 0)
        {
            char key = consumedNutrient.ToString()[0];
            _npk[key] = Math.Max(0f, _npk[key] - consumedAmount);
        }

        return true;
    }
    #endregion

    #region BlockInfo
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine($"Moisture: {(Moisture01 * 100f):0}%");
        dsc.AppendLine($"Support: {SupportCode ?? "none"}");
        dsc.AppendLine($"Retention: {SupportRetentionDays:0.0} days");
        dsc.AppendLine($"NPK: {MathF.Round(_npk['N'], 1)} / {MathF.Round(_npk['P'], 1)} / {MathF.Round(_npk['K'], 1)}");

        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock?.CropProps is not null)
        {
            dsc.AppendLine($"Crop: {cropBlock.GetPlacedBlockName(Api.World, _crop.UpPos)}");
            dsc.AppendLine($"Stage: {_crop.GetCropStage(cropBlock)} / {cropBlock.CropProps.GrowthStages}");
        }
    }
    #endregion

    #region Persistence
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        _moisture.ToTreeAttributes(tree);
        _npk.ToTreeAttributes(tree);
        _crop.ToTreeAttributes(tree);

        tree.SetString("supportCode", SupportCode);
        tree.SetBool("supportIsValid", SupportIsValid);
        tree.SetFloat("supportRetentionDays", SupportRetentionDays);
        tree.SetString("supportFertilityCode", SupportFertilityCode);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        _moisture.FromTreeAttributes(tree);
        _npk.FromTreeAttributes(tree);
        _crop.FromTreeAttributes(tree);

        SupportCode = tree.GetString("supportCode");
        SupportIsValid = tree.GetBool("supportIsValid");
        SupportRetentionDays = tree.GetFloat("supportRetentionDays");
        SupportFertilityCode = tree.GetString("supportFertilityCode");
    }
    #endregion
}
