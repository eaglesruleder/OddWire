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
    private readonly PlowlandMoisture _moisture = new();
    private readonly NPK _npk = new();

    public float Moisture01 => _moisture.Moisture01;
    public NPK Nutrients => _npk;

    #region StoredState
    public string? SupportCode;
    public bool SupportIsValid;
    public float SupportRetentionDays = 4f;
    public string? SupportFertilityCode;
    #endregion

    #region PlayerActions
    public void Water(float dt)
    {
        if (_moisture.Water(dt))
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

        _npk.Fertilize(props);
        consumed = 1;
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
        (NPK? nutrients = null
        ,float? moisture01 = null
        )
    {
        UpdateSupport();
        _moisture.Reset(GameMath.Clamp(moisture01 ?? (SupportIsValid ? 1f : 0f), 0f, 1f));
        _npk.Initialise(FertilitySet.Value(Block), nutrients);
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
        |   _moisture.Tick(this, SupportIsValid, SupportRetentionDays)
        |   _npk.Tick(Api.World.Calendar.TotalHours)
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
    #endregion

    #region BlockInfo
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine($"Moisture: {(Moisture01 * 100f):0}%");
        dsc.AppendLine($"Support: {SupportCode ?? "none"}");
        dsc.AppendLine($"Retention: {SupportRetentionDays:0.0} days");
        dsc.AppendLine($"NPK: {MathF.Round(_npk['N'], 1)} / {MathF.Round(_npk['P'], 1)} / {MathF.Round(_npk['K'], 1)}");
    }
    #endregion

    #region Persistence
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        _moisture.ToTreeAttributes(tree);
        _npk.ToTreeAttributes(tree);

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

        SupportCode = tree.GetString("supportCode");
        SupportIsValid = tree.GetBool("supportIsValid");
        SupportRetentionDays = tree.GetFloat("supportRetentionDays");
        SupportFertilityCode = tree.GetString("supportFertilityCode");
    }
    #endregion
}
