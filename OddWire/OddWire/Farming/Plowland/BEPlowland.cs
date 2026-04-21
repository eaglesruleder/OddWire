using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace OddWire.GameContent;

public sealed class BlockEntityPlowland : BlockEntity, IWaterable
{
    private readonly PlowlandEngine _engine = new();

    public float[] Nutrients => _engine.Nutrients;
    public float Moisture01 => _engine.Moisture01;

    #region Setup
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        #region Server tick
        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery3Seconds, 3000);
        #endregion
    }

    public void InitialiseFromPlow(Block targetBlockBefore, Block supportBlockBelow, float[] nutrients, float moisture01)
    {
        _engine.ResetOnPlowed(targetBlockBefore, supportBlockBelow, nutrients, moisture01);
        MarkDirty(true);
    }

    public void SetFertility(string fertilityCode)
    {
        _engine.SetFertility(fertilityCode);
        MarkDirty(true);
    }
    #endregion

    #region Tick
    private void OnEvery3Seconds(float dt)
    {
        if (_engine.Update(this))
            MarkDirty(true);
    }
    #endregion

    #region PlayerActions
    public void Water(float dt)
    {
        if (_engine.Water(this, dt))
            MarkDirty(true);
    }

    public bool TryFertilize(ItemSlot slot, out int consumed)
    {
        if (!_engine.TryFertilize(this, slot, out consumed))
            return false;

        MarkDirty(true);
        return true;
    }
    #endregion

    #region BlockInfo
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine($"Moisture: {(_engine.Moisture01 * 100f):0}%");
        dsc.AppendLine($"Support: {_engine.Support.SupportCode ?? "none"}");
        dsc.AppendLine($"Retention: {_engine.Support.RetentionDays:0.0} days");
        dsc.AppendLine($"NPK: {MathF.Round(_engine.Nutrients[0], 1)} / {MathF.Round(_engine.Nutrients[1], 1)} / {MathF.Round(_engine.Nutrients[2], 1)}");
    }
    #endregion

    #region Persistence
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        _engine.ToTreeAttributes(tree);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        _engine.FromTreeAttributes(tree);
    }
    #endregion
}
