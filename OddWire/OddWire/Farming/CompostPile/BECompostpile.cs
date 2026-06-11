using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using OddWire.Renderers;

namespace OddWire.GameContent;

public class BlockEntityCompostpile : BlockEntity, IHeatSource, IBlockTint, IWaterable
{
    private static readonly CompostpileSettings Settings = new();
    private WeatherSystemBase? _weather;
    
    #region IHeatSource
    private bool _neighbourHeatSource;
    private bool _neighboursDirty;
    public bool NeighboursDirty
    {   get => _neighboursDirty;
        set => _neighboursDirty |= value;
    }
    
    public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
    {
        if (Temperature < 35f)
            return 0f;
        
        float heatTemp = Temperature - 35;
        float sizeEmission = GameMath.Lerp(0.2f, 1.2f, GetFullness01());
        return 0.0015f * heatTemp * heatTemp * sizeEmission;
    }
    #endregion
    
    #region IBlockTint
    private BlockTintRenderer _tintRenderer;
    private MultiTextureMeshRef _tintMeshRef;
    
    private readonly BlockTint _blockTint = new()
        {NormalShaded = true
        ,RenderRange = 128
        };
    
    public BlockTint BlockTint
    { get {
        _blockTint.MeshRef = _tintMeshRef;
        _blockTint.Rgba = GetVisualTintRgba();
        _blockTint.Enabled = _tintMeshRef?.Disposed != true;
        return _blockTint;
    } }
    
    private void RegenTintMesh()
    {
        if (Api is not ICoreClientAPI capi)
            return;
        
        _tintMeshRef?.Dispose();
        _tintMeshRef = null;
        
        if (Block == null)
            return;
        
        capi.Tesselator.TesselateBlock(Block, out MeshData mesh);
        _tintMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
    }
    
    private void DisposeTintRenderer()
    {
        _tintRenderer?.Dispose();
        _tintRenderer = null;
        
        _tintMeshRef?.Dispose();
        _tintMeshRef = null;
    }
    
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator) => true;
    #endregion
    
    #region State
    public int TotalQty => BrownsQty + NutritionQty + InoculumQty + CompostQty;
    public float GetFullness01() => Math.Clamp((float)TotalQty / Settings.TotalMaxQty, 0, 1);
    
    public int BrownsQty;
    public readonly Dictionary<EnumFoodCategory, int> NutritionStacks = new();
    public int NutritionQty
    { get {
        int sum = 0;
        foreach (var kvp in NutritionStacks)
            sum += kvp.Value;
        return sum;
    } }
    
    public int InoculumQty;
    public int GetInoculumRoomQty() => Math.Max(Settings.Inoculum.MaxQty - InoculumQty, 0);
    
    public int CompostQty;
    public int GetCompostRoomQty() => Math.Max(Settings.CompostMaxQty - CompostQty, 0);
    
    public double PrevTimeMoistureUpdated = -1;
    public float Moisture01;
    
    private double _prevTimeTemperatureUpdated = -1;
    private float _temperature;
    public float Temperature => _temperature;
    
    private double _prevTimeAerationUpdated = -1;
    private float _aeration01 = 1f;
    public float Aeration01 => _aeration01;
    
    private double _prevTimeDecomposed = -1;
    private double _prevTimeProcessed = -1;
    
    private int _adjacentBlockCount;
    public int AdjacentBlockCount
    {   get =>  _adjacentBlockCount;
        set => _adjacentBlockCount = Math.Clamp(value, 0, 5);
    }
    
    public float AdjacentBlockHeat;
    #endregion
    
    #region Rate Factors & Health
    public float GetDecompositionFactor() => GetTemperatureFactor01();
    
    public float GetDecompositionEfficiency01() =>
        GetMoistureHealth01()
    *   GetAerationHealth01();
    
    public float GetCompostFactor()
    {
        if (InoculumQty < Settings.Inoculum.ConsumePerTransition)
            return 0;
        
        return
            GetTemperatureFactor01()
        *   GetInoculumFactor01()
        *   GetMoistureFactor01();
    }
    
    public float GetInoculumFactor01()
    {
        float inoculumFullness = (float)InoculumQty / Settings.Inoculum.MaxQty;
        return GameMath.Lerp(Settings.InoculumMinFactor, 1f, inoculumFullness * (2f - inoculumFullness));
    }
    
    public float GetTemperatureFactor01()
    {
        if (_temperature <  0) return 0.05f;
        if (_temperature < 20) return GameMath.Lerp(0.05f, 1.0f, (_temperature - 0f) / 20f);
        if (_temperature < 55) return 1.0f;
        if (_temperature < 70) return GameMath.Lerp(1.0f, 0.35f, (_temperature - 55f) / 15f);
        return 0.10f;
    }
    
    public float GetTemperatureHealth01() => 1f - GetTemperatureStress01();
    public float GetTemperatureStress01()
    {
        if (_temperature > Settings.OverheatThreshold)
            return Math.Clamp((_temperature - Settings.OverheatThreshold) / Settings.OverheatTolerance, 0, 1);
        return 0;
    }
    
    public float GetMoistureFactor01()
    {
        if (Moisture01 <= 0.05f)
            return 0.05f;
        
        float factor = Moisture01 <= Settings.Moisture01Optimal
        ?   GameMath.Lerp(0.1f, 1.0f, (Moisture01 - 0.05f) / (Settings.Moisture01Optimal - 0.05f))
        :   GameMath.Lerp(1.0f, 0.25f, (Moisture01 - Settings.Moisture01Optimal) / (1f - Settings.Moisture01Optimal));
        
        if (Moisture01 > Settings.DrowningThreshold)
            factor *= 0.6f;
        
        return Math.Clamp(factor, 0.05f, 1.0f);
    }
    
    public float GetMoistureHealth01() => 1f - GetMoistureStress01();
    public float GetMoistureStress01()
    {
        float moistureRisk01 = 0f;
        
        if (Moisture01 < Settings.DryThreshold)
            moistureRisk01 = Settings.DryStress * (Settings.DryThreshold - Moisture01) / Settings.DryThreshold;
        
        if (Moisture01 > Settings.DrowningThreshold)
        {
            float drowningRisk = Settings.DrowningStress * (Moisture01 - Settings.DrowningThreshold) / Settings.DrowningThreshold;
            float anaerobic01 = 1f - _aeration01;
            // Intent anaerobic01^2 relaxes penalty
            moistureRisk01 = drowningRisk * anaerobic01 * anaerobic01;
        }
        
        return Math.Clamp(moistureRisk01, 0, 1);
    }
    
    public float GetNutritionFactor()
    {
        if (NutritionStacks.Count < 1)
            return 1f;

        int count = 0;
        float weighted = 0f;
        foreach (var nutritionStack in NutritionStacks)
        {
            if (Settings.NutritionBonusFactor?.TryGetValue(nutritionStack.Key.ToString(), out float speed) != true)
                speed = Settings.NutritionBaseFactor;
            
            count += nutritionStack.Value;
            weighted += nutritionStack.Value * speed;
        }
        
        return (weighted + Settings.Nutrition.MaxQty - count) / Settings.Nutrition.MaxQty;
    }
    
    public float GetAerationHealth01() => 1f - GetAerationStress01();
    public float GetAerationStress01()
    {
        if (_aeration01 < Settings.HypoxicThreshold)
            return Math.Clamp((Settings.HypoxicThreshold - _aeration01) / Settings.HypoxicTolerance, 0, 1);
        return 0;
    }
    #endregion
    
    #region Interaction
    public void UpdateShapeStackSize() => SetShapeStackSize(TotalQty);
    public void SetShapeStackSize(int stackSize)
    {
        if (Api.Side != EnumAppSide.Server)
            return;
        
        int variantSize = Math.Clamp((int)Math.Ceiling((float)stackSize / 64), 1, CompostpileSettings.ShapeSizeLevels);
        AssetLocation loc = Block.CodeWithVariant("size", $"#{variantSize:0}");
        Block block = Api.World.GetBlock(loc);
        if (block == null
        ||  block.Code == Block.Code
            )
            return;
        
        Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
        Block = block;
    }
    
    //  Intent: CanHarvest treats Nutrition as lossy
    public bool CanHarvest() =>
        CompostQty > 0
    ||  InoculumQty > 0
    ||  BrownsQty > 0;
    
    //  Objective: Harvest all Compost and Compostpile, then remaining Browns & Inoculum, ignore nutrition
    public void Harvest(float dropQuantityMultiplier)
    {
        bool dirty =
            HarvestCompost(dropQuantityMultiplier)
        |   HarvestCompostpile(dropQuantityMultiplier);
        
        if (!dirty)
            dirty |=
                HarvestBrowns(dropQuantityMultiplier)
            |   HarvestInoculum(dropQuantityMultiplier);
        
        if (dirty)
        {
            UpdateShapeStackSize();
            MarkDirty(true);
        }
    }
    #endregion
    
    #region Harvest
    //  Intent: Nutrition is lossy
    private bool HarvestCompost(float dropQuantityMultiplier)
    {
        #region if(!CompostQty || !GetItem(Settings.HarvestCompostPath)) return false;
        if (CompostQty < 1)
            return false;
        
        Item spawnItem = Api.World.GetItem(new AssetLocation(Settings.HarvestCompostPath));
        if (spawnItem is null)
            return false;
        #endregion
        
        #region while(remaining) SpawnItemEntity(spawnNow = Rand(Min(remaining, Settings.HarvestCompostStackQty)) + 1);
        int available = Math.Min(CompostQty, Settings.HarvestCompostQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestCompostStackQty)) + 1;
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }
        #endregion
        
        CompostQty = Math.Max(CompostQty - available, 0);
        
        UpdateInsulation01();
        return true;
    }
    
    private int GetHarvestableCompostpileQty()
    {
        if (Settings.Browns.InitialQty < 1
        && Settings.Nutrition.InitialQty < 1
        && Settings.Inoculum.InitialQty < 1
           )
            return 0;
        
        return Math.Min(Math.Min
            (Settings.Browns.InitialQty > 0 ? BrownsQty / Settings.Browns.InitialQty : int.MaxValue
            ,Settings.Nutrition.InitialQty > 0 ? NutritionQty / Settings.Nutrition.InitialQty : int.MaxValue
           ),Settings.Inoculum.InitialQty > 0 ? InoculumQty / Settings.Inoculum.InitialQty : int.MaxValue
            );
    }
    
    private bool HarvestCompostpile(float dropQuantityMultiplier)
    {
        #region if(!GetHarvestableCompostpileQty || !GetBlock(Settings.HarvestCompostpilePath)) return false;
        int compostpileQty = GetHarvestableCompostpileQty();
        if (compostpileQty < 1)
            return false;
        
        Block spawnBlock = Api.World.GetBlock(new AssetLocation(Settings.HarvestCompostpilePath));
        if (spawnBlock is null
        ||  spawnBlock.Id == 0
            )
            return false;
        #endregion
        
        #region while(remaining) SpawnItemEntity(spawnNow = Rand(Min(remaining, Settings.HarvestCompostStackQty)) + 1);
        int available = Math.Min(compostpileQty, Settings.HarvestCompostpileQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Math.Min(remaining, Api.World.Rand.Next(Settings.HarvestCompostpileStackQty) + 1);
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }
        #endregion
        
        BrownsQty = Math.Max(BrownsQty - Settings.Browns.InitialQty * available, 0);
        TryRemoveCheapestNutrition(Settings.Nutrition.InitialQty * available);
        InoculumQty = Math.Max(InoculumQty - Settings.Inoculum.InitialQty * available, 0);
        
        UpdateInsulation01();
        return true;
    }
    
    private bool HarvestInoculum(float dropQuantityMultiplier)
    {
        #region if(!InoculumQty || !GetItem(Settings.Inoculum.HarvestItemPath)) return false;
        if (InoculumQty < 1)
            return false;
        
        Item spawnItem = Api.World.GetItem(new AssetLocation(Settings.Inoculum.HarvestItemPath));
        if (spawnItem is null)
            return false;
        #endregion
        
        #region while(remaining) SpawnItemEntity(spawnNow = Rand(Min(remaining, Settings.HarvestCompostStackQty)) + 1);
        int available = Math.Min(InoculumQty, Settings.Inoculum.HarvestQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Math.Min(remaining, Api.World.Rand.Next(Settings.Inoculum.HarvestStackQty) + 1);
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }
        #endregion
        
        InoculumQty = Math.Max(InoculumQty - available, 0);
        
        UpdateInsulation01();
        return true;
    }
    
    private bool HarvestBrowns(float dropQuantityMultiplier)
    {
        #region if(!BrownsQty || !GetItem(Settings.Browns.HarvestItemPath)) return false;
        if (BrownsQty < 1)
            return false;
        
        Item spawnItem = Api.World.GetItem(new AssetLocation(Settings.Browns.HarvestItemPath));
        if (spawnItem is null)
            return false;
        #endregion
        
        #region while(remaining) SpawnItemEntity(spawnNow = Rand(Min(remaining, Settings.HarvestCompostStackQty)) + 1);
        int available = Math.Min(BrownsQty, Settings.Browns.HarvestQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Math.Min(remaining, Api.World.Rand.Next(Settings.Browns.HarvestStackQty) + 1);
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }
        #endregion
        
        BrownsQty = Math.Max(BrownsQty - available, 0);
        
        UpdateInsulation01();
        return true;
    }
    
    private void ResetOnPlaced(Block block)
    {
        #region stackBonus = int.TryParse(block.Code?.EndVariant().Substring(1))
        string stackVariant = block.Code?.EndVariant();
        int stackBonus = 0;
        if (!(string.IsNullOrEmpty(stackVariant)
             ||   stackVariant.Length < 2
             ||   stackVariant[0] != '#'
               )
           &&  int.TryParse(stackVariant.Substring(1), out int parsedStackBonus)
          )
            stackBonus = Math.Max(0, parsedStackBonus - 1);
        #endregion
        
        #region Qty = InitialQty + stackBonus * SizeBonusQty
        BrownsQty = Math.Min(Settings.Browns.InitialQty + stackBonus * Settings.Browns.SizeBonusQty, Settings.Browns.MaxQty);
        
        NutritionStacks.Clear();
        int initNutritionQty = Math.Min(Settings.Nutrition.InitialQty + stackBonus * Settings.Nutrition.SizeBonusQty, Settings.Nutrition.MaxQty);
        if (initNutritionQty > 0)
            NutritionStacks[EnumFoodCategory.Unknown] = initNutritionQty;
        
        InoculumQty = Math.Min(Settings.Inoculum.InitialQty + stackBonus * Settings.Inoculum.SizeBonusQty, Settings.Inoculum.MaxQty);
        CompostQty = 0;
        #endregion
        
        #region Moisture, Temperature, Aeration = default
        Moisture01 = Settings.Moisture01Initial;
        PrevTimeMoistureUpdated = -1;
        _prevTimeProcessed = -1;
        _prevTimeDecomposed = -1;
        
        _prevTimeTemperatureUpdated = -1;
        _temperature = 0f;
        
        _prevTimeAerationUpdated = -1;
        _aeration01 = 1f;
        #endregion
    }
    #endregion
    
    #region TryAdd
    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        
        if (slot.StackSize < 1)
            return false;
        
        #region if(TryAdd()) restoreAeration = accepted * Settings.Aeration01Per; else return false;
        bool added = false;
        float restoreAeration = 0;
        
        if (TryAddCompostPile(slot, out accepted))
            { restoreAeration = accepted * Settings.Aeration01PerCompostpileInput; added = true; }
        else
        if (TryAddRef(slot, out accepted, ref BrownsQty, Settings.Browns))
            { restoreAeration = accepted * Settings.Browns.Aeration01PerInput; added = true; }
        else
        if (TryAddRef(slot, out accepted, ref InoculumQty, Settings.Inoculum, CompostQty))
            { restoreAeration = accepted * Settings.Inoculum.Aeration01PerInput; added = true; }
        else
        if (TryAddNutrition(slot, out accepted))
            { restoreAeration = accepted * Settings.Nutrition.Aeration01PerInput; added = true; }
        
        if (!added || accepted < 1)
            return false;
        #endregion
        
        RestoreAeration01(restoreAeration);
        UpdateInsulation01();
        
        UpdateShapeStackSize();
        MarkDirty(true);
        return true;
    }
    
    public bool TryAddRef(ItemSlot slot, out int accepted, ref int currentQty, CompostpileSettings.Ingredient ingredient, int imposeQty = 0)
    {
        accepted = 0;
        
        #region if(!roomQty || !ingredient.AddItemCodeRatios.TryGetValue()) return false;
        int roomQty = ingredient.MaxQty - (currentQty + imposeQty);
        if (ingredient.AddItemCodeRatios is null
        ||  ingredient.AddItemCodeRatios.Count == 0
        ||  roomQty < 1
        ||  slot.StackSize < 1
            )
            return false;
        
        string code =
            slot.Itemstack?.Item?.Code.ToString()
        ??  slot.Itemstack?.Block?.Code.ToString()
        ??  "";
        
        if (!ingredient.AddItemCodeRatios.TryGetValue(code, out float ratio)
        ||  ratio <= 0f
        ||  slot.StackSize < Math.Max(ratio, 1)
            )
            return false;
        #endregion
        
        #region adjusted = Min(ingredient.MaxInputPerAdd, roomQty) * ratio;
        int adjustedLimit =
            ratio >= 1f
        ?   (int)(Math.Min(ingredient.MaxInputPerAdd, roomQty) * ratio)
        :   (int)Math.Min(ingredient.MaxInputPerAdd, roomQty * ratio);
        
        int adjustedInput = Math.Min(slot.StackSize, adjustedLimit);
        if (ratio >= 1f)
            adjustedInput = (int)(Math.Floor(adjustedInput / ratio) * ratio);
        
        int adjustedOutput = (int)Math.Min(adjustedInput / ratio, roomQty);
        #endregion
        
        currentQty += adjustedOutput;
        accepted = adjustedInput;
        
        return accepted > 0;
    }

    private bool TryAddCompostPile(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        
        AssetLocation blockCode = slot.Itemstack?.Block?.Code;
        string stackVariant = blockCode?.EndVariant();
        if (blockCode is null
        || !blockCode.BeginsWith("oddwire", "compostpile")
        ||  string.IsNullOrEmpty(stackVariant)
        ||  stackVariant.Length < 2
        ||  stackVariant[0] != '#'
        || !int.TryParse(stackVariant.Substring(1), out int stackBonus)
           )
            return false;
        
        stackBonus = Math.Max(stackBonus - 1, 0);
        int brownsAdd = Settings.Browns.InitialQty + stackBonus * Settings.Browns.SizeBonusQty;
        int nutritionAdd = Settings.Nutrition.InitialQty + stackBonus * Settings.Nutrition.SizeBonusQty;
        int inoculumAdd = Settings.Inoculum.InitialQty + stackBonus * Settings.Inoculum.SizeBonusQty;
        
        #region if((!brownsRoom && !inoculumRoom) || !Accepted) return false;
        //  Intent: Nutrition is lossy, matching harvest behaviour.
        int brownsRoom = Math.Max(Settings.Browns.MaxQty - BrownsQty, 0);
        int nutritionRoom = Math.Max(Settings.Nutrition.MaxQty - NutritionQty, 0);
        int inoculumRoom = GetInoculumRoomQty();
        if (brownsRoom < 1
        &&  inoculumRoom < 1
            )
            return false;
        
        int brownsAccepted = Math.Min(brownsAdd, brownsRoom);
        int nutritionAccepted = Math.Min(nutritionAdd, nutritionRoom);
        int inoculumAccepted = Math.Min(inoculumAdd, inoculumRoom);
        
        if (brownsAccepted < 1
        &&  nutritionAccepted < 1
        &&  inoculumAccepted < 1
            )
            return false;
        #endregion
        
        #region Qty += Accepted
        BrownsQty += brownsAccepted;
        if (nutritionAccepted > 0)
        {
            NutritionStacks.TryGetValue(EnumFoodCategory.Unknown, out var cur);
            NutritionStacks[EnumFoodCategory.Unknown] = cur + nutritionAccepted;
        }
        InoculumQty += inoculumAccepted;
        #endregion
        
        //  Intent: Nutrition is lossy
        DropIngredientOverflow(Settings.Browns, brownsAdd - brownsAccepted);
        DropIngredientOverflow(Settings.Inoculum, inoculumAdd - inoculumAccepted);
        
        accepted = 1;
        return true;
    }
    
    private void DropIngredientOverflow(CompostpileSettings.Ingredient ingredient, int quantity)
    {
        #region if(!quantity || Side != Server || !dropItem) return;
        if (quantity < 1
        ||  Api?.Side != EnumAppSide.Server
            )
            return;
        
        Item? dropItem = Api.World.GetItem(new AssetLocation(ingredient.HarvestItemPath));
        if (dropItem is null)
            return;
        #endregion
        
        #region while(quantity) SpawnItemEntity(spawnNow = Min(quantity, ingredient.HarvestStackQty));
        while (quantity > 0)
        {
            int spawnNow = Math.Min(quantity, ingredient.HarvestStackQty);
            ItemStack stack = new ItemStack(dropItem, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            quantity -= spawnNow;
        }
        #endregion
    }
    
    private bool TryAddNutrition(ItemSlot slot, out int consumedQty)
    {
        consumedQty = 0;
        
        #region if(!nutritionBudget || !stackConsumeQty || !nutritionAddQty) return false;
        if (slot.Itemstack is null)
            return false;
        ItemStack stack = slot.Itemstack!;
        
        int roomQty = Settings.Nutrition.MaxQty - NutritionQty;
        int nutritionBudget = Math.Min(roomQty, Settings.Nutrition.MaxInputPerAdd);
        if (nutritionBudget < 1)
            return false;
        
        float nutritionPerInput = GetNutritionPerInput(stack);
        if (nutritionPerInput <= 0)
            return false;
        
        int stackConsumeQty = (int)MathF.Floor(nutritionBudget / nutritionPerInput);
        stackConsumeQty = Math.Min(stackConsumeQty, slot.StackSize);
        
        int nutritionAddQty = (int)MathF.Ceiling(stackConsumeQty * nutritionPerInput);
        nutritionAddQty = Math.Min(nutritionAddQty, nutritionBudget);
        if (nutritionAddQty < 1)
            return false;
        #endregion
        
        var nutritionType = stack.Collectible?.NutritionProps?.FoodCategory ?? EnumFoodCategory.Unknown;
        NutritionStacks.TryGetValue(nutritionType, out int cur);
        NutritionStacks[nutritionType] = cur + nutritionAddQty;
        
        consumedQty = stackConsumeQty;
        return true;
    }
    
    private float GetNutritionPerInput(ItemStack stack)
    {
        #region if(!transitionProps) return 1;
        var transitionProps =
            stack.Item?.TransitionableProps
        ??  stack.Block?.TransitionableProps;
        
        if (transitionProps is null
        ||  transitionProps.Length < 1
            )
            return 0;
        #endregion
        
        #region foreach(transitionProp) if(AddItemCodeRatios.TryGetValue() return itemCodeRatio * prop.TransitionRatio;
        foreach (var prop in transitionProps)
        {
            if (prop?.Type != EnumTransitionType.Perish
            ||  prop.TransitionedStack?.Code is null
                )
                continue;
            
            string transitionedCode =
               (prop.TransitionedStack.Code.Domain ?? "game") + ":"
            +   prop.TransitionedStack.Code.Path;
            if (Settings.Inoculum.AddItemCodeRatios?.TryGetValue(transitionedCode, out float inRatio) == true)
                return Math.Max(prop.TransitionRatio * inRatio, 0);
        }
        #endregion
        
        return 0;
    }
    #endregion
    
    #region TryRemove
    private bool TryGetCheapestNutritionCategory(out EnumFoodCategory result)
    {
        float? smallestVal = null;
        result = default;
        
        foreach (var kvp in NutritionStacks)
        {
            if (kvp.Value <= 0)
                continue;
            
            float value = 1f;
            if (Settings.NutritionBonusFactor?.TryGetValue(kvp.Key.ToString(), out float speed) == true)
                value = speed;
            
            if (smallestVal is null || smallestVal > value)
            {
                smallestVal = value;
                result = kvp.Key;
            }
        }
        return smallestVal is not null;
    }
    
    public void TryRemoveCheapestNutrition(int amount)
    {
        if (amount <= 0
        ||  NutritionStacks.Count == 0
            )
            return;
        
        int remaining = amount;
        while (remaining > 0)
        {
            if (!TryGetCheapestNutritionCategory(out EnumFoodCategory category))
                break;
            
            if (!NutritionStacks.TryGetValue(category, out int stackQty)
            ||  stackQty < 1
                )
            {
                NutritionStacks.Remove(category);
                continue;
            }
            
            int removeQty = Math.Min(stackQty, remaining);
            if (stackQty > removeQty)
            {
                remaining -= removeQty;
                NutritionStacks[category] -= removeQty;
            }
            else
            {
                remaining -= NutritionStacks[category];
                NutritionStacks.Remove(category);
            }
        }
    }
    
    public void TryRemoveRandomNutrition(Random rand, int amount)
    {
        if (amount <= 0
        ||  NutritionStacks.Count == 0
            )
            return;
        
        var keys = new List<EnumFoodCategory>(NutritionStacks.Keys);
        int nutritionRemaining = NutritionQty;
        int remaining = amount;
        while
           (keys.Count > 0
        &&  nutritionRemaining > 0
        &&  remaining > 0
            )
        {
            int index = rand.Next(keys.Count);
            var key = keys[index];
            
            int stackQty = NutritionStacks[key];
            if (stackQty <= 0)
            {
                NutritionStacks.Remove(key);
                keys.RemoveAt(index);
                continue;
            }
            
            // Intent: Removal chance scales per its share of nutritionRemaining
            int removeWeight = (int)Math.Ceiling(rand.NextSingle() * stackQty / nutritionRemaining);
            int maxRemove = Math.Min(removeWeight, remaining);
            if (maxRemove < 1)
                maxRemove = 1;
            
            int removeQty = Math.Min(rand.Next(maxRemove) + 1, stackQty);
            
            NutritionStacks[key] -= removeQty;
            if (NutritionStacks[key] < 1)
            {
                NutritionStacks.Remove(key);
                keys.RemoveAt(index);
            }
            
            nutritionRemaining -= removeQty;
            remaining -= removeQty;
        }
    }
    #endregion
    
    #region Lifecycle
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        //  Intent: moisture inits with ResetOnPlaced()/persistence; this recovers legacy unset state
        if (Moisture01 <= 0f
        &&  PrevTimeMoistureUpdated < 0
            )
            Moisture01 = Settings.Moisture01Initial;
        
        if (api.Side == EnumAppSide.Client
        &&  api is ICoreClientAPI capi
            )
        {
            RegenTintMesh();
            _tintRenderer ??= new BlockTintRenderer(capi, this);
        }
        
        if (api.Side == EnumAppSide.Server)
        {
            NeighboursDirty = true;
            RegisterGameTickListener(OnEvery12Seconds, 12000);
        }
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        
        ResetOnPlaced(Block);
        PreUpdateState(Api.World.Calendar.TotalHours);
        _prevTimeProcessed = Api.World.Calendar.TotalHours;
        
        NeighboursDirty = true;
        
        UpdateShapeStackSize();
    }
    
    public override void OnExchanged(Block block)
    {
        base.OnExchanged(block);
        
        if (Api?.Side == EnumAppSide.Client)
            RegenTintMesh();
    }
    
    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        DisposeTintRenderer();
    }
    
    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        DisposeTintRenderer();
    }
    #endregion
    
    #region NeighbourSamplingAndTickUpdate
    private void OnEvery12Seconds(float dt)
    {
        if (Api?.Side != EnumAppSide.Server)
            return;
        
        if (NeighboursDirty)
        {
            UpdateNeighbourBlocks();
            MarkDirty();
        }
        AdjacentBlockHeat = GetNeighbourHeat();
        
        if (Update(Api.World.Calendar.TotalHours))
        {
            UpdateShapeStackSize();
            MarkDirty(true);
        }
    }
    
    private float GetNeighbourHeat()
    {
        if (!_neighbourHeatSource)
            return 0f;
        
        float result = 0f;
        for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
        {
            if (i == BlockFacing.indexDOWN)
                continue;
            
            #region result += neighbour.GetHeatStrength()
            BlockPos neighbourPos = Pos.AddCopy(BlockFacing.ALLFACES[i]);
            result += Api.World.BlockAccessor
               ?.GetBlock(neighbourPos)
               ?.GetInterface<IHeatSource>(Api.World, neighbourPos)
               ?.GetHeatStrength(Api.World, neighbourPos, Pos.Copy())
            ??   0;
            #endregion
        }
        return result;
    }
    
    public void UpdateNeighbourBlocks()
    {
        int blockedSides = 0;
        _neighbourHeatSource = false;
        
        #region blockedSides = Sum(BlocksAirflow(NSEW,U))
        if (BlocksAirflow( BlockFacing.UP)) blockedSides++;
        if (BlocksAirflow(BlockFacing.NORTH)) blockedSides++;
        if (BlocksAirflow(BlockFacing.SOUTH)) blockedSides++;
        if (BlocksAirflow(BlockFacing.EAST)) blockedSides++;
        if (BlocksAirflow(BlockFacing.WEST)) blockedSides++;
        #endregion
        
        AdjacentBlockCount = blockedSides;
        _neighboursDirty = false;
    }
    
    private bool BlocksAirflow(BlockFacing neighbour)
    {
        #region if(!neighbourBlock) return false
        IBlockAccessor blockAccessor = Api.World.BlockAccessor;
        BlockPos neighbourPos = Pos.AddCopy(neighbour);
        Block neighbourBlock = blockAccessor.GetBlock(neighbourPos);
        if (neighbourBlock is null
        ||  neighbourBlock.Id == 0
           )
            return false;
        #endregion
        
        _neighbourHeatSource |= neighbourBlock.GetInterface<IHeatSource>(Api.World, neighbourPos) is not null;
        
        if (blockAccessor.GetBlockEntity(neighbourPos) is BlockEntityCompostpile)
            return true;
        
        if (neighbourBlock.Replaceable >= 6000)
            return false;
        
        return neighbourBlock.SideSolid[neighbour.Opposite.Index];
    }
    
    public void Water(float dt)
    {
        if (Api?.Side == EnumAppSide.Server
        &&  RestoreMoisture01(dt)
            )
            MarkDirty();
    }
    #endregion
    
    #region StateUpdates
    private void RestoreAeration01(float aeration)
    {
        if (aeration <= 0)
            return;
        
        UpdateAeration(Api.World.Calendar.TotalHours);
        _aeration01 = Math.Clamp(_aeration01 + aeration, 0, 1);
        _prevTimeAerationUpdated = Api.World.Calendar.TotalHours;
    }
    
    private bool RestoreMoisture01(float moisture)
    {
        if (moisture <= 0)
            return false;
        
        double totalHours = Api.World.Calendar.TotalHours;
        
        PreUpdateState(totalHours);
        UpdateMoisture(totalHours);
        
        if (Moisture01 >= 1)
            return false;
        
        Moisture01 = Math.Clamp(Moisture01 + moisture * Settings.Moisture01WaterRate * (1f - GetFullness01()), 0, 1);
        PrevTimeMoistureUpdated = totalHours;
        
        return true;
    }
    
    private bool Update(double totalHours)
    {
        if (Api is ICoreServerAPI sapi
        && !sapi.World.IsFullyLoadedChunk(Pos)
           )
            return false;
        
        // Intent: | not ||
        return
            UpdateState(totalHours)
        |   ProcessDecomposition(totalHours)
        |   ProcessCompost(totalHours);
    }
    
    #region CachedEnvironmentSampling
    private double _lastPreUpdatedHours = -1;
    private bool _skyExposed;
    private float _envTemp;
    private bool _inGreenhouse;
    private float _insulation01;
    #endregion
    
    private void PreUpdateState(double totalHours, bool forceRecalc = false)
    {
        bool nowSkyExposed = Api.World.BlockAccessor.IsSkyExposed(Pos);
        forceRecalc |= nowSkyExposed ^ _skyExposed;
        
        if (_lastPreUpdatedHours + 1 > totalHours
        && !forceRecalc
            )
            return;
        
        _skyExposed = nowSkyExposed;
        _envTemp = Api.GetEnvironmentTemperatureC(Pos, totalHours, _skyExposed, Settings.GreenhouseHeat, out _inGreenhouse);
        UpdateInsulation01();
        
        //  Intent: seed display temperature immediately on first env sample (placement or legacy load)
        if (_prevTimeTemperatureUpdated < 0)
        {
            _temperature = _envTemp;
            _prevTimeTemperatureUpdated = totalHours;
        }
        
        _lastPreUpdatedHours = totalHours;
    }

    private void UpdateInsulation01()
    {
        _insulation01 = 0.25f + 0.75f * GetFullness01();
        if (!_skyExposed)
            _insulation01 += 0.10f;
        if (_inGreenhouse)
            _insulation01 += 0.05f;
        _insulation01 = Math.Clamp(_insulation01, 0, 1);
    }

    private bool UpdateState(double totalHours)
    {
        PreUpdateState(totalHours);
        
        // Intent: | not ||
        return
            UpdateMoisture   (totalHours)
        |   UpdateAeration   (totalHours)
        |   UpdateTemperature(totalHours);
    }
    
    private bool UpdateMoisture(double totalHours)
    {
        #region if(0 > PrevTimeUpdated || > totalHours) { PrevTimeUpdated = totalHours; return true; }
        if (PrevTimeMoistureUpdated < 0
        ||  PrevTimeMoistureUpdated > totalHours
           )
        {
            PrevTimeMoistureUpdated = totalHours;
            return true;
        }
        #endregion
        
        float dtMoistureDays = (float)((totalHours - PrevTimeMoistureUpdated) / Api.World.Calendar.HoursPerDay);
        if (dtMoistureDays <= 0)
            return false;
        
        float rainfallHours = 0;
        if (_skyExposed)
        {
            _weather ??= Api.ModLoader.GetModSystem<WeatherSystemBase>();
            rainfallHours = _weather?.GetTotalRainfallSince(Pos, PrevTimeMoistureUpdated, totalHours) ?? 0f;
        }
        if (rainfallHours > 0f)
            Moisture01 += rainfallHours * Settings.Moisture01GainPerRainyDay / Api.World.Calendar.HoursPerDay;
        
        float ambientDrying01 = Math.Clamp(_envTemp / 20f, 0.05f, 1.75f);
        Moisture01 -= ambientDrying01 * dtMoistureDays / Settings.MoistureAmbientRetentionDays;
        
        float retention01 = GameMath.Lerp(0.05f, 0.50f, _insulation01);
        Moisture01 = Math.Clamp(Math.Max(Moisture01, retention01), 0f, 1f);
        
        PrevTimeMoistureUpdated = totalHours;
        return true;
    }

    private float GetAerationConsumption()
    {
        if (Settings.NutritionAerationConsumption is null)
            return 1f;
    
        int count = 0;
        float demand = 0f;
        foreach (var kvp in NutritionStacks)
        {
            if (Settings.NutritionAerationConsumption.TryGetValue(kvp.Key.ToString(), out float o2) != true)
                o2 = 1f;
        
            count += kvp.Value;
            demand += o2 * kvp.Value;
        }
    
        return (demand + (Settings.Nutrition.MaxQty - count)) / Settings.Nutrition.MaxQty;
    }
    
    private bool UpdateAeration(double totalHours)
    {
        #region if(0 > _prevTimeAerationUpdated || > totalHours) return true;
        if (_prevTimeAerationUpdated < 0
        ||  _prevTimeAerationUpdated > totalHours
           )
        {
            _prevTimeAerationUpdated = totalHours;
            return true;
        }
        #endregion
        
        float dtAerationDays = (float)((totalHours - _prevTimeAerationUpdated) / Api.World.Calendar.HoursPerDay);
        if (dtAerationDays <= 0)
            return false;
        
        #region _aeration01 -= compaction01 * airflowPenalty * oxygenDemand01 * dtDays / RetentionDays;
        float compaction01 = GameMath.Lerp(0.45f, 1.0f, GetFullness01());
        float airflowPenalty = 1f + _adjacentBlockCount * Settings.AerationBlockedPenalty;
        _aeration01 = Math.Clamp
           (_aeration01
        -   compaction01 * airflowPenalty * GetAerationConsumption() * dtAerationDays / Settings.AerationRetentionDays
           ,0, 1);
        _prevTimeAerationUpdated = totalHours;
        #endregion
        
        return true;
    }

    private float GetInternalHeat()
    {
        float nutritionHeat = 0f;
        if (Settings.NutritionHeat is not null)
            foreach (var kvp in NutritionStacks)
                if (Settings.NutritionHeat.TryGetValue(kvp.Key.ToString(), out float heatC))
                    nutritionHeat += heatC * kvp.Value / Math.Max(1f, Settings.Nutrition.MaxQty);
        
        return
           (Settings.HeatingRatePerHour + nutritionHeat)
        *   GetMoistureFactor01()
        *   GetInoculumFactor01()
        *   GetTemperatureFactor01()
        *   GameMath.Lerp(0.05f, 1.0f, _aeration01)
        *   GameMath.Lerp(0.85f, 1.0f, _insulation01);
    }

    private bool UpdateTemperature(double totalHours)
    {
        #region if(0 > _prevTimeUpdated || > totalHours) return true;
        if (_prevTimeTemperatureUpdated < 0
        ||  _prevTimeTemperatureUpdated > totalHours
           )
        {
            _prevTimeTemperatureUpdated = totalHours;
            _temperature = _envTemp;
            return true;
        }
        #endregion
        
        double dtTemperatureHours = totalHours - _prevTimeTemperatureUpdated;
        if (dtTemperatureHours <= 0f)
            return false;
        
        #region coolingAmount = coolingRate * dtTemperatureHours;
        float evaporativeCooling01 =
            Moisture01 > Settings.Moisture01Optimal
        ?   0.35f * (Moisture01 - Settings.Moisture01Optimal) / (1f - Settings.Moisture01Optimal)
        :   0f;
        
        float coolingInsulation = GameMath.Lerp(1.6f, 0.7f, _insulation01);
        float coolingRate = Math.Clamp
            (Settings.CoolingRatePerHour * (1f + evaporativeCooling01) / coolingInsulation
            , 0.01f, 0.5f
            );
        double coolingAmount = coolingRate * dtTemperatureHours;
        #endregion
        
        float targetTemp = _envTemp + GetInternalHeat() + AdjacentBlockHeat * Settings.NeighbourHeatRate;
        float coolingFactor = (float)(coolingAmount / (1f + coolingAmount)); // asymptotic: large dt approaches 1, never overshoots
        _temperature += (targetTemp - _temperature) * coolingFactor;
        _prevTimeTemperatureUpdated = totalHours;
        
        return true;
    }
    #endregion
    
    #region Processing
    private bool ProcessCompost(double totalHours)
    {
        #region if(neverProcessed || !browns/inoculumPortions || !compostRoom) { prevTime = now; return false; } 
        if (_prevTimeProcessed < 0
        ||  _prevTimeProcessed > totalHours
            )
        {
            _prevTimeProcessed = totalHours;
            return false;
        }
        
        float brownsPortions = (float)BrownsQty / Settings.Browns.ConsumePerTransition;
        float inoculumPortions = (float)InoculumQty / Settings.Inoculum.ConsumePerTransition;
        float transitionCapacity = Math.Min(Math.Min
            (brownsPortions
            ,inoculumPortions
           ),GetCompostRoomQty()
            );
        
        if (transitionCapacity < 1)
        {
            _prevTimeProcessed = totalHours;
            return false;
        }
        #endregion
        
        #region if(!(transitions = duration * BaseRate * Factor)) return false;
        double duration = totalHours - _prevTimeProcessed;
        float transitionRate = Settings.BaseCompostRatePerHour * GetCompostFactor();
        int transitions = (int)Math.Min
            (duration * transitionRate
            ,transitionCapacity
            );
        
        if (transitions < 1)
            return false; // keep accruing progress
        #endregion
        
        BrownsQty   -= Math.Min(transitions * Settings.Browns.ConsumePerTransition, BrownsQty);
        InoculumQty -= Math.Min(transitions * Settings.Inoculum.ConsumePerTransition, InoculumQty);
        CompostQty   = Math.Min(transitions + CompostQty, Settings.CompostMaxQty);
        
        _prevTimeProcessed += transitions / transitionRate;
        return true;
    }
    
    private bool ProcessDecomposition(double totalHours)
    {
        #region if(neverProcessed || !inoculumRoom) { prevTime = now; return false; }
        if (_prevTimeDecomposed < 0
        ||  _prevTimeDecomposed > totalHours
           )
        {
            _prevTimeDecomposed = totalHours;
            return false;
        }
        
        int inoculumRoom = GetInoculumRoomQty();
        if (inoculumRoom < 1)
        {
            _prevTimeDecomposed = totalHours;
            return false;
        }
        #endregion
        
        #region if(!(transitions = duration * BaseRate * Factor)) return false;
        double duration = totalHours - _prevTimeDecomposed;
        float transitionRate = Settings.BaseInoculumRatePerHour * GetDecompositionFactor();

        float efficiency01 = GetDecompositionEfficiency01();
        float baseTransitions = (float)(duration * transitionRate * efficiency01);
        float nutritionTransitions = 0;
        int transitions = 0;
        
        if (baseTransitions < inoculumRoom)
        {
            nutritionTransitions =
                Math.Min(baseTransitions * (GetNutritionFactor() - 1), NutritionQty)
            *   efficiency01;
            nutritionTransitions = Math.Min(nutritionTransitions, inoculumRoom - baseTransitions);
            
            transitions = (int)(baseTransitions + nutritionTransitions);
        }
        else
            transitions = (int)Math.Min(baseTransitions, inoculumRoom);
        
        if (transitions < 1)
            return false; // keep accruing progress
        #endregion
        
        if (nutritionTransitions > 0)
            TryRemoveRandomNutrition(Api.World.Rand, (int)(nutritionTransitions / efficiency01));
        InoculumQty += transitions;
        
        _prevTimeDecomposed = totalHours;
        return true;
    }
    #endregion
    
    private Vec4f GetVisualTintRgba()
    {
        if (TotalQty < 1)
            return new Vec4f(1f, 1f, 1f, 1f);
        
        float brownsFull01 = (float)BrownsQty / Settings.Browns.MaxQty;
        float greensFull01 = (float)NutritionQty / Settings.Nutrition.MaxQty;
        float inoculumFull01 = (float)(InoculumQty + CompostQty) / Settings.Inoculum.MaxQty;
        
        float red = GameMath.Lerp(0.85f, 1f, brownsFull01);
        float green = GameMath.Lerp(0.85f, 1f, greensFull01);
        float blue = GameMath.Lerp(0.85f, 1f, inoculumFull01);
        
        float moistureDarken = GameMath.Lerp(1f, 0.75f, Moisture01);
        float aerationDarken = GameMath.Lerp(0.75f, 1f, _aeration01);
        float brightness = moistureDarken * aerationDarken;
        
        return new Vec4f
            (Math.Clamp(red * brightness, 0, 1)
            ,Math.Clamp(green * brightness, 0, 1)
            ,Math.Clamp(blue * brightness, 0, 1)
            , 1f
            );
    }
    
    #region BlockInfoFormatting
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        #region inGreenhouse = room && room.skylight > room.notSkylight && !room.Exits
        bool skyExposed = Api.World.BlockAccessor?.IsSkyExposed(Pos) == true;
        bool inGreenhouse = false;
        if (!skyExposed)
        {
            var room = Api.ModLoader.GetModSystem<RoomRegistry>()?.GetRoomForPosition(Pos.UpCopy());
            inGreenhouse =
                room != null
            &&  room.SkylightCount > room.NonSkylightCount
            &&  room.ExitCount == 0;
        }
        #endregion
        
        #region Make roomLabel
        string roomLabel =
            inGreenhouse ? Lang.Get("oddwire:compostpile-room-greenhouse")
        :   skyExposed ? Lang.Get("oddwire:compostpile-room-outside")
        :   Lang.Get("oddwire:compostpile-room-inside");
        if (Settings.InfoDebug)
            roomLabel += Lang.Get(" ({0:0}°C)", Api.GetEnvironmentTemperatureC(Pos, Api.World.Calendar.TotalHours, skyExposed, Settings.GreenhouseHeat, out _));
        #endregion
        
        #region Write Composting Rate & Core
        dsc.AppendLine(Lang.Get("oddwire:compostpile-info-composting", GetCompostingStatus(), roomLabel));
        
        string colorTemp  = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, GetTemperatureFactor01() * 99)]);
        string colorMoist = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, GetMoistureFactor01()   * 99)]);
        string colorAir   = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, GetAerationHealth01()   * 99)]);
        dsc.AppendLine(Lang.Get
            ("oddwire:compostpile-info-state"
            ,colorTemp,  Temperature
            ,colorMoist, Moisture01 * 100f
            ,colorAir,   Aeration01 * 100f
            ));
        #endregion
        
        #region Write Debug Neighbours
        if (Settings.InfoDebug)
        {
            dsc.AppendLine(Lang.Get
                ("Neighbours: {0:0}/5 blocking{1}"
                ,AdjacentBlockCount
                ,AdjacentBlockHeat > 0 ? $" | Heat: {AdjacentBlockHeat:0}°C" : ""
                ));
            dsc.AppendLine();
        }
        #endregion
        
        #region Write Processing
        float compostFactor = GetCompostFactor();
        float compostRate = Settings.BaseCompostRatePerHour * compostFactor;
        
        float inoculumFactor = GetDecompositionFactor() * GetNutritionFactor() * GetDecompositionEfficiency01();
        float inoculumCreateRate = Settings.BaseInoculumRatePerHour * inoculumFactor;
        float inocConsumeRate = compostRate * Settings.Inoculum.ConsumePerTransition;
        float inoculumRate = inoculumCreateRate - inocConsumeRate;
        
        dsc.AppendLine
            (Lang.Get("oddwire:compostpile-info-culture", inoculumFactor * 100f)
        +   ", " + GetRateLabel(inoculumRate, Lang.Get("oddwire:var-inoculum"))
            );
        if (Settings.InfoDebug)
        {
            dsc.AppendLine(Lang.Get
                ("- Rate: Temp {0:0}% × Nutrition {1:0}%"
                ,GetTemperatureFactor01() * 100f
                ,GetNutritionFactor() * 100f
                ));
            dsc.AppendLine(Lang.Get
                ("- Health: Moisture {0:0}% × Aeration {1:0}%"
                ,GetMoistureHealth01() * 100f
                ,GetAerationHealth01() * 100f
                ));
            dsc.AppendLine();
        }
        
        dsc.AppendLine
            (Lang.Get("oddwire:compostpile-info-speed", compostFactor * 100f)
        +   ", " + GetRateLabel(compostRate, Lang.Get("item-compost"))
            );
        if (Settings.InfoDebug)
            dsc.AppendLine(Lang.Get
                ("- Rate - Inoc {0:0}% × Temp {1:0}% × Moist {2:0}%"
                ,GetInoculumFactor01() * 100f
                ,GetTemperatureFactor01() * 100f
                ,GetMoistureFactor01() * 100f
                ));
        dsc.AppendLine();
        #endregion
        
        #region Write Materials & Harvest
        string missingLabel = GetMissingMaterialLabel();
        if (!string.IsNullOrEmpty(missingLabel))
            dsc.AppendLine(missingLabel);
        
        if (Settings.InfoDebug)
        {
            string mixLabel = GetNutritionMixLabel();
            if (!string.IsNullOrEmpty(mixLabel))
                dsc.AppendLine(mixLabel);
        }
        
        dsc.AppendLine(Lang.Get
            ("oddwire:compostpile-info-harvest"
            ,CompostQty
            ,GetHarvestableCompostpileQty()
            ));
        #endregion
    }
    
    private string GetCompostingStatus()
    {
        #region Collect warning states
        List<string> states = new();
        AppendInoculumStatus(states);
        AppendTemperatureStatus(states);
        AppendMoistureStatus(states);
        AppendAerationStatus(states);
        #endregion
        
        if (states.Count < 1)
            return Lang.Get("oddwire:compostpile-status-active");
        
        return string.Join(", ", states);
    }
    private void AppendTemperatureStatus(List<string> states)
    {
        if (Temperature > Settings.OverheatThreshold)
            states.Add(Lang.Get("oddwire:compostpile-status-overheating"));
        else if (GetTemperatureFactor01() < Settings.InfoFactorWarningThreshhold)
        {
            if (Temperature >= 55f)
                states.Add(Lang.Get("oddwire:compostpile-status-hot"));
            else
            if (Temperature < 20f)
                states.Add(Lang.Get("oddwire:compostpile-status-cold"));
        }
    }
    private void AppendMoistureStatus(List<string> states)
    {
        // Intended: _aeration01 reduces, not invalidates Drowning
        if (Moisture01 > Settings.DrowningThreshold)
            states.Add(Lang.Get("oddwire:compostpile-status-drowning"));
        else if (GetMoistureFactor01() < Settings.InfoFactorWarningThreshhold)
        {
            if (Moisture01 < Settings.Moisture01Optimal)
                states.Add(Lang.Get("oddwire:compostpile-status-dry"));
        }
    }
    private void AppendAerationStatus(List<string> states)
    {
        if (Aeration01 < Settings.HypoxicThreshold)
            states.Add(Lang.Get("oddwire:compostpile-status-hypoxic"));
        else if (Aeration01 < Settings.HypoxicThreshold + Settings.HypoxicTolerance)
            states.Add(Lang.Get("oddwire:compostpile-status-turnsoon"));
    }
    private void AppendInoculumStatus(List<string> states)
    {
        if (InoculumQty < Settings.Inoculum.ConsumePerTransition)
            states.Add(Lang.Get("oddwire:compostpile-status-unseeded"));
    }
    private string GetRateLabel(float rate, string outputName)
    {
        if (rate <= 0f)
            return Lang.Get("oddwire:compostpile-rate-stalled");
        
        float hoursPerCompost = 1f / rate;
        float hoursPerDay = Api?.World?.Calendar?.HoursPerDay ?? 24f;
        
        if (hoursPerCompost < hoursPerDay)
            return Lang.Get("oddwire:compostpile-rate-hours", hoursPerCompost) + outputName;
        
        float daysPerCompost = hoursPerCompost / hoursPerDay;
        return Lang.Get("oddwire:compostpile-rate-days", daysPerCompost) + outputName;
    }
    private string GetMissingMaterialLabel()
    {
        float brownsPortions = (float)BrownsQty / Settings.Browns.ConsumePerTransition;
        float nutritionPortions = (float)NutritionQty / Settings.Nutrition.ConsumePerTransition;
        float inoculumPortions = (float)InoculumQty / Settings.Inoculum.ConsumePerTransition;
        
        float maxThresholdPortions =
            Math.Max(Math.Max(brownsPortions, nutritionPortions), inoculumPortions)
        -   Settings.InfoNeedsPortionsThreshhold;
        
        float minPortions = Math.Min(Math.Min(brownsPortions, nutritionPortions), inoculumPortions);
        if (minPortions > maxThresholdPortions
        && !Settings.InfoDebug
           )
            return "";
        
        List<string> parts = new();
        
        #region if(brownsPortions < maxThresholdPortions) parts += Browns
        if((brownsPortions < maxThresholdPortions
        &&  BrownsQty + Settings.Browns.ConsumePerTransition < Settings.Browns.MaxQty
            )
        ||  Settings.InfoDebug
           )
            parts.Add
                (Lang.Get("oddwire:compostpile-needs-ingredient"
                ,Lang.Get("oddwire:compostpile-ingredient-browns")
                ,BrownsQty
                ,Settings.Browns.MaxQty
                )
            +   (Settings.InfoDebug ? $" ({brownsPortions:0})" : "")
                );
        #endregion
        
        #region if(nutritionPortions < maxThresholdPortions) parts += Nutrition
        if((nutritionPortions < maxThresholdPortions
        &&  NutritionQty + Settings.Nutrition.ConsumePerTransition < Settings.Nutrition.MaxQty
           )
        ||  Settings.InfoDebug
           )
            parts.Add(
                Lang.Get("oddwire:compostpile-needs-ingredient"
                    ,Lang.Get("oddwire:compostpile-ingredient-nutrition")
                    ,NutritionQty
                    ,Settings.Nutrition.MaxQty
                    )
            +   (Settings.InfoDebug ? $" ({nutritionPortions:0})" : "")
                );
        #endregion
        
        #region if(inoculumPortions < maxThresholdPortions) parts += Inoculum
        if((inoculumPortions < maxThresholdPortions
        &&  InoculumQty + Settings.Inoculum.ConsumePerTransition < Settings.Inoculum.MaxQty
            )
        ||  Settings.InfoDebug
           )
            parts.Add(
                Lang.Get("oddwire:compostpile-needs-ingredient"
                    ,Lang.Get("oddwire:compostpile-ingredient-inoculum")
                    ,InoculumQty
                    ,Settings.Inoculum.MaxQty
                    )
            +   (Settings.InfoDebug ? $" ({inoculumPortions:0})" : "")
                );
        #endregion
        
        if (parts.Count < 1)
            return "";
        
        if (Settings.InfoDebug)
            return string.Join(" | ", parts);
        
        return Lang.Get("oddwire:compostpile-needs", string.Join(" | ", parts));
    }
    private string GetNutritionMixLabel()
    {
        if (NutritionStacks.Count < 1)
            return "";
        
        StringBuilder mix = new();
        foreach (var nutritionStack in NutritionStacks)
        {
            if (nutritionStack.Value < 1)
                continue;
            
            mix.Append(nutritionStack.Key);
            mix.Append(": ");
            mix.Append(nutritionStack.Value);
            mix.Append(", ");
        }
        
        if (mix.Length < 1)
            return "";
        
        return Lang.Get("oddwire:compostpile-info-nutmix", mix.ToString().Substring(0, mix.Length - 2));
    }
    #endregion
    
    #region Persistence
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        PrevTimeMoistureUpdated = tree.GetDouble("PrevTimeMoistureUpdated", -1);
        Moisture01 = tree.GetFloat("Moisture01");
        
        BrownsQty = tree.GetInt("BrownsQty");
        InoculumQty = tree.GetInt("InoculumQty");
        CompostQty = tree.GetInt("CompostQty");
        
        NutritionStacks.Clear();
        int nutritionLength = tree.GetInt("NutritionStacks.Count");
        for (int i = 0; i < nutritionLength; i++)
            NutritionStacks[(EnumFoodCategory)tree.GetInt($"NutritionStacks<{i}>")] = tree.GetInt($"NutritionStacks[{i}]");
        
        _prevTimeTemperatureUpdated = tree.GetDouble("_prevTimeTemperatureUpdated", -1);
        _temperature = tree.GetFloat("_temperature");
        
        _prevTimeAerationUpdated = tree.GetDouble("_prevTimeAerationUpdated", -1);
        _aeration01 = tree.GetFloat("_aeration01", 1f);
        
        _prevTimeDecomposed = tree.GetDouble("_prevTimeDecomposed", -1);
        _prevTimeProcessed = tree.GetDouble("_prevTimeProcessed", -1);
        
        _adjacentBlockCount = tree.GetInt("_adjacentBlockCount");
        AdjacentBlockHeat = tree.GetFloat("AdjacentBlockHeat");
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetDouble("PrevTimeMoistureUpdated", PrevTimeMoistureUpdated);
        tree.SetFloat("Moisture01", Moisture01);
        
        tree.SetInt("BrownsQty", BrownsQty);
        tree.SetInt("InoculumQty", InoculumQty);
        tree.SetInt("CompostQty", CompostQty);
        
        tree.SetInt("NutritionStacks.Count", NutritionStacks?.Count ?? 0);
        if (NutritionStacks is not null)
        {
            int i = 0;
            foreach (var stack in NutritionStacks)
            {
                tree.SetInt($"NutritionStacks<{i}>", (int)stack.Key);
                tree.SetInt($"NutritionStacks[{i}]", stack.Value);
                i++;
            }
        }
        
        tree.SetDouble("_prevTimeTemperatureUpdated", _prevTimeTemperatureUpdated);
        tree.SetFloat("_temperature", _temperature);
        
        tree.SetDouble("_prevTimeAerationUpdated", _prevTimeAerationUpdated);
        tree.SetFloat("_aeration01", _aeration01);
        
        tree.SetDouble("_prevTimeDecomposed", _prevTimeDecomposed);
        tree.SetDouble("_prevTimeProcessed", _prevTimeProcessed);
        
        tree.SetInt("_adjacentBlockCount", _adjacentBlockCount);
        tree.SetFloat("AdjacentBlockHeat", AdjacentBlockHeat);
    }
    #endregion
}