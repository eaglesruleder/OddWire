using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class BlockEntityPlowland : BlockEntitySoilNutrition, IWaterable, IFarmlandBlockEntity, ICropland, IAnimalFoodSource
{
    private static readonly PlowlandSettings Settings = new();
    private readonly CropGrowth    _crop      = new();
    private readonly TreeAttribute _cropAttrs = new();

    #region IFarmlandBlockEntity
    // Nutrients, MoistureLevel, OriginalFertility, UpPos — all from BlockEntitySoilNutrition
    BlockPos              IFarmlandBlockEntity.Pos => Pos;
    public ITreeAttribute CropAttributes           => _cropAttrs;
    public double         TotalHoursForNextStage   => _crop.TotalHoursForNextStage;
    // Vanilla BEFarmland throws NotImplementedException here — returning last update time is safe
    public double         TotalHoursFertilityCheck => totalHoursLastUpdate;
    #endregion

    #region IWaterable
    public void Water(float dt)
    {
        // waterNeighbours: false — mirrors vanilla's one-level spread intent.
        // Spread TO plowland is handled by BlockEntitySoilNutrition_WaterFarmland_Patch on the source block.
        // Plowland receives spread but does not re-initiate it.
        WaterFarmland(dt, false);
        MarkDirty(true); // BESN's guard (> 0.05 delta) never fires at watering-can dt rates
    }
    #endregion

    #region ICropland
    public bool TryPlant(Block cropBlock, ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel)
    {
        if (cropBlock.CropProps is null)
            return false;

        float growthRate = GetGrowthRate(cropBlock.CropProps.RequiredNutrient);
        if (!_crop.TryPlant(cropBlock, itemslot, byEntity, blockSel, Api.World, growthRate))
            return false;

        MarkDirty(true);
        return true;
    }

    public ItemStack[] GetDrops(ItemStack[] drops) =>
        _crop.GetDrops(drops, Api.World, Block.Attributes);
    #endregion

    #region IAnimalFoodSource
    public bool IsSuitableFor(Entity entity, CreatureDiet diet)
    {
        if (diet is null) return false;
        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock is null) return false;
        string[] foodTags = cropBlock.Attributes?["foodTags"].AsArray<string>([]) ?? [];
        return diet.Matches(EnumFoodCategory.NoNutrition, foodTags);
    }

    public float ConsumeOnePortion(Entity entity)
    {
        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock is null) return 0;

        Block deadCropBlock = Api.World.GetBlock(new AssetLocation("deadcrop"));
        if (deadCropBlock is null || deadCropBlock.Id == 0) return 0;

        Api.World.BlockAccessor.SetBlock(deadCropBlock.Id, upPos);
        if (Api.World.BlockAccessor.GetBlockEntity(upPos) is BlockEntityDeadCrop beDead)
        {
            beDead.Inventory[0].Itemstack = new ItemStack(cropBlock);
            beDead.deathReason            = EnumCropStressType.Eaten;
        }
        return 1f;
    }

    public Vec3d Position => Pos.ToVec3d().Add(0.5, 1, 0.5);
    public string Type    => "food";
    #endregion

    #region StoredState
    public string? SupportCode;
    public float   SupportRetentionDays = Settings.DefaultRetentionDays;
    public string? SupportFertilityCode;
    // Crop state (damage flags, growth timer) owned by CropGrowth — persisted via _crop.ToTreeAttributes
    #endregion

    #region Lifecycle
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api); // sets msFarming, upPos, growthRateMul from world config, tick listener
        _crop.Init(Pos);
        _crop.SetRules(growthRateMul);

        if (api is ICoreServerAPI)
            api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        float fertility = FertilitySet.Value(Block);
        Initialise(new[] { fertility, fertility, fertility }, 0f);
    }

    public void Initialise(float[] initNutrients, float moisture01)
    {
        float originalVal    = FertilitySet.Value(Block);
        originalFertility[0] = (int)originalVal;
        originalFertility[1] = (int)originalVal;
        originalFertility[2] = (int)originalVal;

        nutrients[0] = GameMath.Clamp(initNutrients[0], 0f, Settings.Max);
        nutrients[1] = GameMath.Clamp(initNutrients[1], 0f, Settings.Max);
        nutrients[2] = GameMath.Clamp(initNutrients[2], 0f, Settings.Max);

        moistureLevel = GameMath.Clamp(moisture01, 0f, 1f);
        lastMoistureLevelUpdateTotalDays = Api.World.Calendar.TotalDays;

        UpdateSupport(); // must run first — sets totalHoursWaterRetention
        tryUpdateMoistureLevel(Api.World.Calendar.TotalDays, true); // water scan — sets lastWaterDistance, floors moistureLevel

        UpdateFarmlandBlock();
        MarkDirty(true);
    }

    public override void OnCropBlockBroken()
    {
        _crop.OnCropBlockBroken(); // resets growth timer and damage flags
        base.OnCropBlockBroken();  // resets damageAccum
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        if (Api.Side == EnumAppSide.Server)
            Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        if (Api?.Side == EnumAppSide.Server)
            Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
    }

    protected override void beginIntervalledUpdate
        (out FarmlandFastForwardUpdate onInterval
        ,out FarmlandUpdateEnd onEnd
        )
    {
        base.beginIntervalledUpdate(out onInterval, out onEnd);

        var baseInterval = onInterval;
        var baseEnd      = onEnd;

        onInterval = (hourInterval, conds, lightGrowthSpeedFactor, growthPaused) =>
        {
            // UpdateSupport first — sets totalHoursWaterRetention before base moisture update runs
            UpdateSupport();
            baseInterval?.Invoke(hourInterval, conds, lightGrowthSpeedFactor, growthPaused);
            _crop.CheckDamage(conds, Api.World);
            TickCrop(hourInterval, lightGrowthSpeedFactor, growthPaused);
        };

        onEnd = () => baseEnd?.Invoke();
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

        string? newSupportCode          = supportBlock.Code?.ToShortString();
        string? newSupportFertilityCode = FertilitySet.GetCode(supportBlock);

        if (newSupportCode          == SupportCode
        &&  newSupportFertilityCode == SupportFertilityCode
           )
            return false;

        SupportCode          = newSupportCode;
        SupportFertilityCode = newSupportFertilityCode;
        SupportRetentionDays = Settings.DefaultRetentionDays * (FertilitySet.Value(SupportFertilityCode) / 100f);

        // Drive vanilla's retention rate from our support block fertility
        totalHoursWaterRetention = Api.World.Calendar.HoursPerDay * Math.Max(Settings.MinRetentionDays, SupportRetentionDays);
        return true;
    }

    private void ResetSupport()
    {
        SupportCode          = null;
        SupportFertilityCode = null;
        SupportRetentionDays = 0;
        totalHoursWaterRetention = Api.World.Calendar.HoursPerDay * Settings.MinRetentionDays;
    }

    private void TickCrop(double hourInterval, double lightGrowthSpeedFactor, bool growthPaused)
    {
        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock?.CropProps is null)
            return;

        // GetGrowthRate includes the 1.22 moisture curve — no separate moisture check needed here
        float growthRate = GetGrowthRate(cropBlock.CropProps.RequiredNutrient) * (float)lightGrowthSpeedFactor;

        if (!_crop.Tick
            (totalHoursLastUpdate
            ,hourInterval
            ,moistureLevel
            ,growthPaused
            ,Api.World
            ,this
            ,growthRate
            ,out EnumSoilNutrient consumedNutrient
            ,out float            consumedAmount
            ))
            return;

        if (consumedAmount > 0)
            ConsumeNutrients(consumedNutrient, consumedAmount);
    }

    protected override void UpdateFarmlandBlock()
    {
        if (Api?.World is null
        ||  Block.Code is null
            )
            return;

        string? fertilityCode = FertilitySet.GetCode(Block);
        if (fertilityCode is null)
            return;

        string        moistureCode = moistureLevel > 0.1f ? Settings.StateMoist : Settings.StateDry;
        AssetLocation newCode      = new(Block.Code.Domain, $"plowland-{moistureCode}-{fertilityCode}");
        Block         newBlock     = Api.World.GetBlock(newCode);
        if (newBlock is null
        ||  newBlock.Id == 0
        ||  newBlock.Id == Block.Id
            )
            return;

        Api.World.BlockAccessor.ExchangeBlock(newBlock.BlockId, Pos);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        MarkDirty(true); // ensure current state is serialized during transition
    }
    #endregion

    #region BlockInfo
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc); // NPK, moisture, growth speed — all from vanilla

        dsc.AppendLine($"Support: {SupportCode ?? "none"}");
        dsc.AppendLine($"Retention: {SupportRetentionDays:0.0} days");

        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock?.CropProps is not null)
        {
            dsc.AppendLine($"Crop: {cropBlock.GetPlacedBlockName(Api.World, upPos)}");
            dsc.AppendLine($"Stage: {_crop.GetCropStage(cropBlock)} / {cropBlock.CropProps.GrowthStages}");
        }
    }
    #endregion

    #region Persistence
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        _crop.ToTreeAttributes(tree); // includes growth timer and damage flags

        tree.SetString("supportCode",         SupportCode);
        tree.SetFloat ("supportRetentionDays", SupportRetentionDays);
        tree.SetString("supportFertilityCode", SupportFertilityCode);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        _crop.FromTreeAttributes(tree); // includes growth timer and damage flags

        SupportCode          = tree.GetString("supportCode");
        SupportRetentionDays = tree.GetFloat ("supportRetentionDays", Settings.DefaultRetentionDays);
        SupportFertilityCode = tree.GetString("supportFertilityCode");

        // Restore retention floor after load — UpdateSupport recalculates on next tick
        if (Api?.World is not null)
            totalHoursWaterRetention = Api.World.Calendar.HoursPerDay * Math.Max(Settings.MinRetentionDays, SupportRetentionDays);
    }
    #endregion
}
