using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class BlockEntityPlowland : BlockEntitySoilNutrition, IWaterable, IFarmlandBlockEntity, ICropland, IAnimalFoodSource
{
    private static readonly PlowlandSettings Settings = new();
    private readonly CropGrowth _crop = new();
    private readonly TreeAttribute _cropAttrs = new();

    #region IFarmlandBlockEntity
    BlockPos IFarmlandBlockEntity.Pos => Pos;
    public ITreeAttribute CropAttributes => _cropAttrs;
    public double TotalHoursForNextStage => _crop.TotalHoursForNextStage;
    public double TotalHoursFertilityCheck => totalHoursLastUpdate;
    #endregion

    #region IWaterable
    public void Water(float dt)
    {
        WaterFarmland(dt, false);
        MarkDirty(true);
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
    public string Type => "food";
    public Vec3d Position => Pos.ToVec3d().Add(0.5, 1, 0.5);
    
    public bool IsSuitableFor(Entity entity, CreatureDiet diet)
    {
        if (diet is null)
            return false;
        
        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock is null)
            return false;
        
        string[] foodTags = cropBlock.Attributes?["foodTags"].AsArray<string>([]) ?? [];
        return diet.Matches(EnumFoodCategory.NoNutrition, foodTags);
    }

    public float ConsumeOnePortion(Entity entity)
    {
        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock is null)
            return 0;

        Block deadCropBlock = Api.World.GetBlock(new AssetLocation("deadcrop"));
        if (deadCropBlock is null
        ||  deadCropBlock.Id == 0
            )
            return 0;

        Api.World.BlockAccessor.SetBlock(deadCropBlock.Id, upPos);
        if (Api.World.BlockAccessor.GetBlockEntity(upPos) is BlockEntityDeadCrop beDead)
        {
            beDead.Inventory[0].Itemstack = new ItemStack(cropBlock);
            beDead.deathReason = EnumCropStressType.Eaten;
        }
        return 1f;
    }
    #endregion

    #region StoredState
    public string? SupportCode;
    public float SupportRetentionDays = Settings.DefaultRetentionDays;
    public string? SupportFertilityCode;
    #endregion

    #region Lifecycle
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
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
        int originalVal = (int)FertilitySet.Value(Block);
        originalFertility[0] = originalVal;
        originalFertility[1] = originalVal;
        originalFertility[2] = originalVal;
        nutrients[0] = GameMath.Clamp(initNutrients[0], 0f, Settings.Max);
        nutrients[1] = GameMath.Clamp(initNutrients[1], 0f, Settings.Max);
        nutrients[2] = GameMath.Clamp(initNutrients[2], 0f, Settings.Max);
        
        moistureLevel = GameMath.Clamp(moisture01, 0f, 1f);
        lastMoistureLevelUpdateTotalDays = Api.World.Calendar.TotalDays;
        UpdateSupport(); // Must precede tryUpdateMoistureLevel — sets totalHoursWaterRetention
        tryUpdateMoistureLevel(Api.World.Calendar.TotalDays, true);
        
        UpdateFarmlandBlock();
        MarkDirty(true);
    }

    public override void OnCropBlockBroken()
    {
        _crop.OnCropBlockBroken();
        base.OnCropBlockBroken();
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
        var baseEnd = onEnd;

        onInterval = (hourInterval, conds, lightGrowthSpeedFactor, growthPaused) =>
        {
            UpdateSupport();
            baseInterval?.Invoke(hourInterval, conds, lightGrowthSpeedFactor, growthPaused);
            _crop.CheckDamage(conds, Api.World);
            TickCrop(hourInterval, lightGrowthSpeedFactor, growthPaused);
        };
        onEnd = () => baseEnd?.Invoke();
    }
    
    public void AddSlowRelease(float n, float p, float k)
    {
        slowReleaseNutrients[0] = Math.Min(slowReleaseNutrients[0] + n, 150);
        slowReleaseNutrients[1] = Math.Min(slowReleaseNutrients[1] + p, 150);
        slowReleaseNutrients[2] = Math.Min(slowReleaseNutrients[2] + k, 150);
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
            if (SupportCode is null
            &&  SupportFertilityCode is null
                )
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
        SupportRetentionDays = Settings.DefaultRetentionDays * (FertilitySet.Value(SupportFertilityCode) / 100f);
        
        totalHoursWaterRetention = Api.World.Calendar.HoursPerDay * Math.Max(Settings.MinRetentionDays, SupportRetentionDays);
        return true;
    }

    private void ResetSupport()
    {
        SupportCode = null;
        SupportFertilityCode = null;
        SupportRetentionDays = 0;
        totalHoursWaterRetention = Api.World.Calendar.HoursPerDay * Settings.MinRetentionDays;
    }

    private void TickCrop(double hourInterval, double lightGrowthSpeedFactor, bool growthPaused)
    {
        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock?.CropProps is null)
            return;
        
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
                ,out float consumedAmount
            ))
            return;

        if (consumedAmount > 0)
            ConsumeNutrientsDistributed(consumedNutrient, consumedAmount);
    }
    
    private static readonly Vec3i[] HorizontalOffsets            = new Vec3i[]{new(1,0,0),new(-1,0,0),new(0,0,1),new(0,0,-1)};
    private static readonly Vec3i[] HorizontalAndDiagonalOffsets = new Vec3i[]{new(1,0,0),new(-1,0,0),new(0,0,1),new(0,0,-1),new(1,0,1),new(1,0,-1),new(-1,0,1),new(-1,0,-1)};

    public void ConsumeNutrientsDistributed(EnumSoilNutrient nutrient, float amount, int loadQty = 2, bool diagonal = false)
    {
        int nutrientIndex = (int)nutrient;
        
        #region loadBlocks = GetBESoil(checkCoords).Where(!Crop).SortDesc(nutrient)[:loadQty]
        loadQty = Math.Clamp(loadQty, 0, diagonal ? 8 : 4);
        BlockEntitySoilNutrition?[] loadBESN = new BlockEntitySoilNutrition[loadQty];
        
        Vec3i[] checkCoords = diagonal ? HorizontalAndDiagonalOffsets : HorizontalOffsets;
        foreach (Vec3i offset in checkCoords)
        {
            BlockPos adjacentPos = Pos.AddCopy(offset);
            if (Api.World.BlockAccessor.GetBlockEntity(adjacentPos) is not BlockEntitySoilNutrition adjacentBESN
            ||  Api.World.BlockAccessor.GetBlock(adjacentPos.UpCopy()).CropProps != null
               )
                continue;

            float adjacentNutrient = adjacentBESN.Nutrients[nutrientIndex];
            for (int i = 0; i < loadBESN.Length; i++)
            {
                if (loadBESN[i] != null
                &&  adjacentNutrient <= loadBESN[i].Nutrients[nutrientIndex]
                   )
                    continue;

                for (int j = loadBESN.Length - 1; j > i; j--)
                    loadBESN[j] = loadBESN[j - 1];

                loadBESN[i] = adjacentBESN;
                break;
            }
        }
        #endregion

        #region if(nutrientTotal <= 0) return
        float nutrientTotal = nutrients[nutrientIndex];
        foreach (var besn in loadBESN)
        {
            if (besn is null)
                break;
            nutrientTotal += besn.Nutrients[nutrientIndex];
        }

        if (nutrientTotal <= 0f)
            return;
        #endregion

        #region foreach(loadBESN+this) base.ConsumeNutrients(amount * share/nutrientTotal)
        ConsumeNutrients(nutrient, amount * (nutrients[nutrientIndex] / nutrientTotal));
        foreach (var besn in loadBESN)
        {
            if (besn is null)
                break;
            besn.ConsumeNutrients(nutrient, amount * (besn.Nutrients[nutrientIndex] / nutrientTotal));
        }
        #endregion
    }

    protected override void UpdateFarmlandBlock()
    {
        if (Api?.World is null
        ||  Block.Code is null
            )
            return;

        string? fertilityCode = FertilitySet.GetCode(Block);
        string? sideCode = Block.Variant["side"];
        if (fertilityCode is null
        ||  sideCode is null
            )
            return;

        string moistureCode = moistureLevel > 0.1f ? Settings.StateMoist : Settings.StateDry;
        AssetLocation newCode = new(Block.Code.Domain, $"plowland-{sideCode}-{moistureCode}-{fertilityCode}");
        Block newBlock = Api.World.GetBlock(newCode);
        if (newBlock is null
        ||  newBlock.Id == 0
        ||  newBlock.Id == Block.Id
            )
            return;

        Api.World.BlockAccessor.ExchangeBlock(newBlock.BlockId, Pos);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        MarkDirty(true);
    }
    #endregion

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        dsc.AppendLine($"Support: {SupportCode ?? "none"}");
        dsc.AppendLine($"Retention: {SupportRetentionDays:0.0} days");

        Block? cropBlock = _crop.GetCrop(Api.World);
        if (cropBlock?.CropProps is not null)
        {
            dsc.AppendLine($"Crop: {cropBlock.GetPlacedBlockName(Api.World, upPos)}");
            dsc.AppendLine($"Stage: {_crop.GetCropStage(cropBlock)} / {cropBlock.CropProps.GrowthStages}");
        }
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        _crop.ToTreeAttributes(tree);

        tree.SetString("supportCode",          SupportCode);
        tree.SetFloat ("supportRetentionDays",  SupportRetentionDays);
        tree.SetString("supportFertilityCode",  SupportFertilityCode);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        _crop.FromTreeAttributes(tree);

        SupportCode          = tree.GetString("supportCode");
        SupportRetentionDays = tree.GetFloat ("supportRetentionDays", Settings.DefaultRetentionDays);
        SupportFertilityCode = tree.GetString("supportFertilityCode");
        
        if (Api?.World is not null)
            totalHoursWaterRetention = Api.World.Calendar.HoursPerDay * Math.Max(Settings.MinRetentionDays, SupportRetentionDays);
    }
}