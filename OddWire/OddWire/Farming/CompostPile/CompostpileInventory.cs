using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class CompostpileInventory
{
    private WeatherSystemBase? _weather;
    private CompostpileSettings Settings => CompostpileSettings.Default;

    #region StoredState
    public int TotalQty => BrownsQty + NutritionQty + InoculumQty + CompostQty;
    private float GetFullness01() => Math.Clamp((float)TotalQty / Settings.TotalMaxQty, 0,1);

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
    public int CompostQty;

    public double PrevTimeMoistureUpdated = -1;
    public float Moisture01;
    
    private double _prevTimeTemperatureUpdated = -1;
    private float _temperature;
    public float Temperature => _temperature;
    
    private double _prevTimeAerationUpdated = -1;
    private float _aeration01 = 1f;
    public float Aeration01 => _aeration01;
    
    private double _prevTimeStressUpdated = -1;
    public float Stress01;
    
    public double PrevTimeProcessed = -1;
    #endregion
    
    
    #region RateHelpers
    //  Factor impacts Processing Rate
    public float GetFactor()
    {
        if ((InoculumQty < 1 && CompostQty < 1)
        ||  (BrownsQty < 1 && NutritionQty < 1)
            )
            return 0f;
        
        return
            GetNutritionFactor()
        *   GetInoculumFactor01()
        *   GetTemperatureFactor01()
        *   GetMoistureFactor01();
    }
    
    //  Stress impacts Compost/Inoculum output ratio
    public float GetStress01() => 1f - GetHealth01();
    public float GetHealth01() =>
        GetAerationHealth01()
    *   GetTemperatureHealth01()
    *   GetMoistureHealth01();
    
    
    //  Intent: Compost counts toward Factor so efficiency doesn't crash as inoculum converts
    public float GetInoculumFactor01() =>
        Math.Clamp((float)(InoculumQty + CompostQty) / Settings.Inoculum.MaxQty, 0.1f, 1f);
    
    public int GetInoculumRoomQty() =>
        Math.Max(Settings.Inoculum.MaxQty - (InoculumQty + CompostQty), 0);
    
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
            return Math.Clamp((_temperature - Settings.OverheatThreshold) / Settings.OverheatTolerance, 0,1);
        return 0;
    }
    
    public float GetMoistureFactor01()
    {
        if (Moisture01 <= 0.05f)
            return 0.05f;

        float factor = Moisture01 <= Settings.Moisture01Optimal
        ?   GameMath.Lerp(0.1f, 1.0f, (Moisture01 - 0.05f) / (Settings.Moisture01Optimal - 0.05f))
        :   GameMath.Lerp(1.0f, 0.25f, (Moisture01 - Settings.Moisture01Optimal) / (1f - Settings.Moisture01Optimal));
        
        if (Moisture01 > 0.9f)
            factor *= 0.6f;
        
        return Math.Clamp(factor, 0.05f, 1.0f);
    }

    public float GetMoistureHealth01() => 1f - GetMoistureStress01();
    public float GetMoistureStress01()
    {
        float moistureRisk01 = 0f;
        
        if (Moisture01 < 0.05f)
            moistureRisk01 = 0.6f * (0.05f - Moisture01) / 0.05f;
        
        if (Moisture01 > Settings.DrowningThreshold)
        {
            float drowningRisk = (Moisture01 - Settings.DrowningThreshold) / Settings.DrowningTolerance;
            float anaerobic01 = 1f - _aeration01;
            moistureRisk01 = drowningRisk * anaerobic01 * anaerobic01;
        }
        
        return Math.Clamp(moistureRisk01,0,1);
    }
    
    public float GetNutritionFactor()
    {
        if (NutritionStacks.Count < 1)
            return 0f;
        
        float weighted = 0f;
        foreach (var nutritionStack in NutritionStacks)
        {
            if (Settings.NutritionSpeed?.TryGetValue(nutritionStack.Key.ToString(), out float speed) != true)
                speed = 1;
            
            weighted += nutritionStack.Value * speed;
        }

        return weighted / Settings.Nutrition.MaxQty;
    }
    
    
    public float GetAerationHealth01() => 1f - GetAerationStress01();
    public float GetAerationStress01()
    {
        if (_aeration01 < Settings.HypoxicThreshold)
            return Math.Clamp((Settings.HypoxicThreshold - _aeration01) / Settings.HypoxicTolerance, 0,1);
        return 0;
    }
    #endregion


    #region Harvest
    //  Intent: Nutrition is lossy
    public bool CanHarvest() =>
        CompostQty > 0
    ||  InoculumQty > 0
    ||  BrownsQty > 0;
    
    public bool HarvestCompost(BlockEntity be, float dropQuantityMultiplier)
    {
        if (CompostQty < 1)
            return false;
        
        Item spawnItem = be.Api.World.GetItem(new AssetLocation(Settings.HarvestCompostPath));

        int available = Math.Min(CompostQty, Settings.HarvestCompostQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = be.Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestCompostStackQty)) + 1;
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        CompostQty = Math.Max(CompostQty - available, 0);
        
        PreUpdateInsulation01();
        return true;
    }
    
    
    public int GetHarvestableCompostpileQty() => Math.Min(Math.Min
        (BrownsQty / Settings.Browns.InitialQty
        ,NutritionQty / Settings.Nutrition.InitialQty
       ),InoculumQty / Settings.Inoculum.InitialQty
        );
    public bool HarvestCompostpile(BlockEntity be, float dropQuantityMultiplier)
    {
        int compostpileQty = GetHarvestableCompostpileQty();
        if (compostpileQty < 1)
            return false;
        
        Block spawnBlock = be.Api.World.GetBlock(new AssetLocation(Settings.HarvestCompostpilePath));

        int available = Math.Min(compostpileQty, Settings.HarvestCompostpileQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Math.Min(remaining, be.Api.World.Rand.Next(Settings.HarvestCompostpileStackQty)+1);
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        BrownsQty = Math.Max(BrownsQty - Settings.Browns.InitialQty * available, 0);
        TryRemoveCheapestNutrition(Settings.Nutrition.InitialQty * available);
        InoculumQty = Math.Max(InoculumQty - Settings.Inoculum.InitialQty * available, 0);

        PreUpdateInsulation01();
        return true;
    }
    
    
    public bool HarvestInoculum(BlockEntity be, float dropQuantityMultiplier)
    {
        if (InoculumQty < 1)
            return false;
        
        Item spawnBlock = be.Api.World.GetItem(new AssetLocation(Settings.Inoculum.HarvestItemPath));

        int available = Math.Min(InoculumQty, Settings.Inoculum.HarvestQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Math.Min(remaining, be.Api.World.Rand.Next(Settings.Inoculum.HarvestStackQty)+1);
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        InoculumQty = Math.Max(InoculumQty - available, 0);
        
        PreUpdateInsulation01();
        return true;
    }
    
    
    public bool HarvestBrowns(BlockEntity be, float dropQuantityMultiplier)
    {
        if (BrownsQty < 1)
            return false;
        
        Item spawnBlock = be.Api.World.GetItem(new AssetLocation(Settings.Browns.HarvestItemPath));

        int available = Math.Min(BrownsQty, Settings.Browns.HarvestQty);
        int remaining = (int)Math.Ceiling(available * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Math.Min(remaining, be.Api.World.Rand.Next(Settings.Browns.HarvestStackQty)+1);
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        BrownsQty = Math.Max(BrownsQty - available, 0);
        
        PreUpdateInsulation01();
        return true;
    }
    #endregion


    #region Input
    public void ResetOnPlaced(Block block)
    {
        string stackVariant = block.Code?.EndVariant();
        int stackBonus = 0;
        if(!(string.IsNullOrEmpty(stackVariant)
        ||   stackVariant.Length < 2
        ||   stackVariant[0] != '#'
            )
        &&  int.TryParse(stackVariant.Substring(1), out int parsedStackBonus)
            )
            stackBonus = Math.Max(0, parsedStackBonus - 1);

        BrownsQty = Math.Min(Settings.Browns.InitialQty + stackBonus * Settings.Browns.SizeBonusQty, Settings.Browns.MaxQty);

        NutritionStacks.Clear();
        NutritionStacks[EnumFoodCategory.Unknown] = Math.Min(Settings.Nutrition.InitialQty + stackBonus * Settings.Nutrition.SizeBonusQty, Settings.Nutrition.MaxQty);

        InoculumQty = Math.Min(Settings.Inoculum.InitialQty + stackBonus * Settings.Inoculum.SizeBonusQty, Settings.Inoculum.MaxQty);
        CompostQty = 0;

        Moisture01 = Settings.Moisture01Initial;
        PrevTimeMoistureUpdated = -1;
        PrevTimeProcessed = -1;

        _prevTimeTemperatureUpdated = -1;
        _temperature = 0f;

        _prevTimeAerationUpdated = -1;
        _aeration01 = 1f;

        _prevTimeStressUpdated = -1;
        Stress01 = 0f;
    }
    
    public bool TryAdd(BlockEntity be, ItemSlot slot, out int accepted)
    {
        accepted = 0;
        if (slot.StackSize < 1)
            return false;

        bool added = false;
        float restoreAeration = 0;

        if (TryAddCompostPile(be, slot, out accepted))
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

        if (!added)
            return false;
        
        RestoreAeration01(be, restoreAeration);
        PreUpdateInsulation01();
        return true;
    }
    
    public bool TryAddRef(ItemSlot slot, out int accepted, ref int currentQty, CompostpileSettings.Ingredient ingredient, int imposeQty = 0)
    {
        accepted = 0;
        if (ingredient.AddItemCodeRatios is null
        ||  ingredient.AddItemCodeRatios.Count == 0
           )
            return false;

        int roomQty = ingredient.MaxQty - (currentQty + imposeQty);
        if (roomQty < 1
        ||  slot.StackSize < 1
            )
            return false;

        string code =
            slot.Itemstack?.Item?.Code.ToString()
        ??  slot.Itemstack?.Block?.Code.ToString()
        ??  "";
        
        if(!ingredient.AddItemCodeRatios.TryGetValue(code, out float ratio)
        ||  ratio <= 0f
        ||  slot.StackSize < Math.Max(ratio, 1)
            )
            return false;

        int adjustedLimit = 
            ratio >= 1f
        ?   (int)(Math.Min(ingredient.MaxInputPerAdd, roomQty) * ratio)
        :   (int)Math.Min(ingredient.MaxInputPerAdd, roomQty * ratio);
        
        int adjustedInput = Math.Min(slot.StackSize, adjustedLimit);
        if (ratio >= 1f)
            adjustedInput = (int)(Math.Floor(adjustedInput / ratio) * ratio);
        
        int adjustedOutput = (int)Math.Min(adjustedInput / ratio, roomQty);

        currentQty += adjustedOutput;
        accepted = adjustedInput;

        
        return accepted > 0;
    }

    private bool TryAddCompostPile(BlockEntity be, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        AssetLocation blockCode = slot.Itemstack?.Block?.Code;
        string stackVariant = blockCode?.EndVariant();
        if (string.IsNullOrEmpty(blockCode)
        || !blockCode.BeginsWith("oddwire","compostpile")
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

        int brownsRoom = Math.Max(Settings.Browns.MaxQty - BrownsQty, 0);
        int nutritionRoom = Math.Max(Settings.Nutrition.MaxQty - NutritionQty, 0);
        if (brownsRoom < 1
        &&  nutritionRoom < 1
            )
            return false;

        int inoculumRoom = GetInoculumRoomQty();

        int brownsAccepted = Math.Min(brownsAdd, brownsRoom);
        int nutritionAccepted = Math.Min(nutritionAdd, nutritionRoom);
        int inoculumAccepted = Math.Min(inoculumAdd, inoculumRoom);

        if (brownsAccepted < 1
        &&  nutritionAccepted < 1
        &&  inoculumAccepted < 1
            )
            return false;

        BrownsQty += brownsAccepted;

        if (nutritionAccepted > 0)
        {
            NutritionStacks.TryGetValue(EnumFoodCategory.Unknown, out var cur);
            NutritionStacks[EnumFoodCategory.Unknown] = cur + nutritionAccepted;
        }

        InoculumQty += inoculumAccepted;

        DropIngredientOverflow(be, Settings.Browns, brownsAdd - brownsAccepted);

        // Intentional: Nutrition overflow from bundled compostpile input stays lossy, matching harvest behaviour.
        DropIngredientOverflow(be, Settings.Inoculum, inoculumAdd - inoculumAccepted);

        accepted = 1;
        return true;
    }

    private void DropIngredientOverflow(BlockEntity be, CompostpileSettings.Ingredient ingredient, int quantity)
    {
        if (quantity < 1
        ||  be.Api?.Side != EnumAppSide.Server
            )
            return;

        Item dropItem = be.Api.World.GetItem(new AssetLocation(ingredient.HarvestItemPath));
        if (dropItem is null)
            return;

        while (quantity > 0)
        {
            int dropNow = Math.Min(quantity, ingredient.HarvestStackQty);
            ItemStack stack = new ItemStack(dropItem, dropNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            quantity -= dropNow;
        }
    }
    
    private bool TryAddNutrition(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        ItemStack stack = slot.Itemstack;
        var collectible = stack?.Collectible;
        var nutritionProps = collectible?.NutritionProps;
        if (nutritionProps is null)
            return false;

        int roomQty = Settings.Nutrition.MaxQty - NutritionQty;
        if (roomQty < 1)
            return false;
    
        float nutritionQtyPerInput = GetNutritionRotQtyPerInput(stack);
        if (nutritionQtyPerInput <= 0f)
            return false;

        // MaxInputPerAdd refs NutritionQty, not StackSize
        int nutritionMaxAdd = Math.Min(roomQty, Settings.Nutrition.MaxInputPerAdd);
        float nutritionAdd =
            nutritionMaxAdd > nutritionQtyPerInput
        ?   MathF.Floor(nutritionMaxAdd / nutritionQtyPerInput)
        :   MathF.Ceiling(nutritionMaxAdd / nutritionQtyPerInput);
        
        int stackConsumeQty = (int)Math.Min(nutritionAdd, slot.StackSize);
        if (stackConsumeQty < 1)
            return false;
        
        int nutritionAddQty = (int)(stackConsumeQty * nutritionQtyPerInput);
        if (nutritionAddQty < 1)
            return false;

        NutritionStacks.TryGetValue(nutritionProps.FoodCategory, out int cur);
        NutritionStacks[nutritionProps.FoodCategory] = cur + nutritionAddQty;

        accepted = stackConsumeQty;
        return true;
    }

    private float GetNutritionRotQtyPerInput(ItemStack stack)
    {
        var transitionProps =
            stack.Item?.TransitionableProps
        ??  stack.Block?.TransitionableProps;

        if (transitionProps is null
        ||  transitionProps.Length < 1
            )
            return 1f;

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
                return Math.Max(prop.TransitionRatio * inRatio, 0f);
        }

        return 1f;
    }
    #endregion


    #region NutritionRemoval
    private bool TryGetCheapestNutritionCategory(out EnumFoodCategory result)
    {
        bool found = false;
        float smallestVal = float.MaxValue;
        result = default;
        
        foreach (var kvp in NutritionStacks)
        {
            if (kvp.Value <= 0)
                continue;

            float value = 1f;
            if (Settings.NutritionSpeed?.TryGetValue(kvp.Key.ToString(), out float speed) == true)
                value = speed;
            
            if (!found
            ||  value < smallestVal
               )
            {
                found = true;
                smallestVal = value;
                result = kvp.Key;
            }
        }
        
        return found;
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
            if(!TryGetCheapestNutritionCategory(out EnumFoodCategory category))
                break;

            if(!NutritionStacks.TryGetValue(category, out int stackQty)
            ||  stackQty < 1
                )
            {
                NutritionStacks.Remove(category);
                continue;
            }

            int removeQty = Math.Min(stackQty, remaining);
            
            if (stackQty > removeQty)
                NutritionStacks[category] -= removeQty;
            else
                NutritionStacks.Remove(category);

            remaining -= removeQty;
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
           (remaining > 0
        &&  nutritionRemaining > 0
        &&  keys.Count > 0
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

            int removeWeight = (int)Math.Ceiling(rand.NextSingle() * stackQty / nutritionRemaining);
            int maxRemove = Math.Min(removeWeight, remaining);
            if (maxRemove < 1)
                maxRemove = 1;

            int removeQty = rand.Next(maxRemove) + 1;
            removeQty = Math.Min(removeQty, stackQty);

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


    #region StateUpdates
    public void RestoreAeration01(BlockEntity be, float aeration)
    {
        if (aeration <= 0)
            return;

        UpdateAeration(be, be.Api.World.Calendar.TotalHours);
        _aeration01 = Math.Clamp(_aeration01 + aeration, 0,1);
        _prevTimeAerationUpdated = be.Api.World.Calendar.TotalHours;
    }
    
    public bool RestoreMoisture01(BlockEntity be, float moisture)
    {
        if (moisture <= 0)
            return false;
        
        double totalHours = be.Api.World.Calendar.TotalHours;
        
        PreUpdateState(be, totalHours);
        UpdateMoisture(be, totalHours);

        if (Moisture01 >= 1)
            return false;
        
        Moisture01 = Math.Clamp(Moisture01 + moisture, 0,1);
        PrevTimeMoistureUpdated = totalHours;

        return true;
    }
    
    
    public bool Update(BlockEntity be, double totalHours)
    {
        if (be.Api is ICoreServerAPI sapi
        && !sapi.World.IsFullyLoadedChunk(be.Pos)
           )
            return false;

        return
            UpdateState(be, totalHours)
        |   ProcessCompost(be, totalHours);
    }

    private double _lastPreUpdatedHours = -1;
    private bool _skyExposed;
    private float _envTemp;
    private bool _inGreenhouse;
    private float _insulation01;
    private void PreUpdateState(BlockEntity be, double totalHours, bool forceRecalc = false)
    {
        if (_lastPreUpdatedHours + 1 > totalHours
        && !forceRecalc
            )
            return;

        PreUpdateEnv(be, totalHours);
        PreUpdateInsulation01();
        
        _lastPreUpdatedHours = totalHours;
    }

    private void PreUpdateEnv(BlockEntity be, double totalHours)
    {
        _skyExposed = be.Api.World.BlockAccessor.IsSkyExposed(be.Pos);
        _envTemp = be.Api.GetEnvironmentTemperatureC(be.Pos, totalHours, _skyExposed, Settings.GreenhouseHeat, out _inGreenhouse);
    }
    
    private void PreUpdateInsulation01()
    {
        _insulation01 = 0.25f + 0.75f * GetFullness01();
        if (!_skyExposed)
            _insulation01 += 0.10f;
        if (_inGreenhouse)
            _insulation01 += 0.05f;
        _insulation01 = Math.Clamp(_insulation01, 0, 1);
    }
    
    private bool UpdateState(BlockEntity be, double totalHours)
    {
        PreUpdateState(be, totalHours);
        return
            UpdateMoisture   (be, totalHours)
        |   UpdateAeration   (be, totalHours)
        |   UpdateTemperature(be, totalHours)
        |   UpdateStress     (be, totalHours);
    }

    private bool UpdateMoisture(BlockEntity be, double totalHours)
    {
        if (PrevTimeMoistureUpdated < 0
        ||  PrevTimeMoistureUpdated > totalHours
           )
        {
            PrevTimeMoistureUpdated = totalHours;
            return true;
        }
        
        float dtMoistureDays = (float)((totalHours - PrevTimeMoistureUpdated) / be.Api.World.Calendar.HoursPerDay);
        if (dtMoistureDays <= 0)
            return false;
        
        float rainfallHours = 0;

        if (_skyExposed)
        {
            _weather ??= be.Api.ModLoader.GetModSystem<WeatherSystemBase>();
            rainfallHours = _weather?.GetTotalRainfallSince(be.Pos, PrevTimeMoistureUpdated, totalHours) ?? 0f;
        }
        
        if (rainfallHours > 0f)
            Moisture01 += rainfallHours * Settings.Moisture01GainPerRainyDay / be.Api.World.Calendar.HoursPerDay;
        
        float ambientDrying01 = Math.Clamp(_envTemp / 20f, 0.05f, 1.75f);
        Moisture01 -= ambientDrying01 * dtMoistureDays / Settings.MoistureAmbientRetentionDays;
        
        float retention01 = GameMath.Lerp(0.05f, 0.50f, _insulation01);
        Moisture01 = Math.Clamp(Math.Max(Moisture01, retention01), 0f, 1f);
        
        PrevTimeMoistureUpdated = totalHours;
        return true;
    }

    private bool UpdateAeration(BlockEntity be, double totalHours)
    {
        if (_prevTimeAerationUpdated < 0
        ||  _prevTimeAerationUpdated > totalHours
           )
        {
            _prevTimeAerationUpdated = totalHours;
            return true;
        }
        
        float dtAerationDays = (float)((totalHours - _prevTimeAerationUpdated) / be.Api.World.Calendar.HoursPerDay);
        if (dtAerationDays <= 0)
            return false;

        float compaction01 = GameMath.Lerp(0.45f, 1.0f, GetFullness01());
        _aeration01 = Math.Clamp
            (_aeration01
         -   dtAerationDays * compaction01 / Settings.AerationRetentionDays
            ,0,1);
        _prevTimeAerationUpdated = totalHours;

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

    private bool UpdateTemperature(BlockEntity be, double totalHours)
    {
        if (_prevTimeTemperatureUpdated < 0
        ||  _prevTimeTemperatureUpdated > totalHours
           )
        {
            _prevTimeTemperatureUpdated = totalHours;
            _temperature = _envTemp;
            return true;
        }
        
        double dtTemperatureHours = totalHours - _prevTimeTemperatureUpdated;
        if (dtTemperatureHours <= 0f)
            return false;
        
        float evaporativeCooling01 =
            Moisture01 > Settings.Moisture01Optimal
        ?   0.35f * (Moisture01 - Settings.Moisture01Optimal) / (1f - Settings.Moisture01Optimal)
        :   0f;

        float coolingInsulation = GameMath.Lerp(1.6f, 0.7f, _insulation01);
        float coolingRate = Math.Clamp
            (Settings.CoolingRatePerHour * (1f + evaporativeCooling01) / coolingInsulation
            ,0.01f, 0.5f
            );
        double coolingAmount = coolingRate * dtTemperatureHours;
        float coolingFactor = (float)(coolingAmount / (1f + coolingAmount));
        
        float targetTemp = _envTemp + GetInternalHeat();
        _temperature += (targetTemp - _temperature) * coolingFactor;
        _prevTimeTemperatureUpdated = totalHours;

        return true;
    }

    private bool UpdateStress(BlockEntity be, double totalHours)
    {
        if (_prevTimeStressUpdated < 0
        ||  _prevTimeStressUpdated > totalHours
           )
        {
            _prevTimeStressUpdated = totalHours;
            return true;
        }

        float dtStressDays = (float)((totalHours - _prevTimeStressUpdated) / be.Api.World.Calendar.HoursPerDay);
        if (dtStressDays <= 0f)
            return false;

        float targetStress01 = GetStress01();
        float responseDays =
            targetStress01 > Stress01
        ?   Settings.StressGainDays
        :   Settings.StressRecoveryDays;

        Stress01 += (targetStress01 - Stress01) * Math.Clamp(dtStressDays / responseDays, 0,1);
        Stress01 = Math.Clamp(Stress01, 0,1);
        _prevTimeStressUpdated = totalHours;

        return true;
    }
    #endregion


    #region Processing
    private bool ProcessCompost(BlockEntity be, double totalHours)
    {
        if (PrevTimeProcessed < 0
        ||  PrevTimeProcessed > totalHours
        || (InoculumQty + CompostQty >= Settings.Inoculum.MaxQty
        &&  Settings.Inoculum.ConsumePerTransition < Settings.CompostOutPerSuccess
           ))
        {
            PrevTimeProcessed = totalHours;
            return false;
        }

        float brownsPortions = (float)BrownsQty / Settings.Browns.ConsumePerTransition;
        float nutritionPortions = (float)NutritionQty / Settings.Nutrition.ConsumePerTransition;
        float bulkPortions = brownsPortions + nutritionPortions;

        if (bulkPortions < 1f)
        {
            PrevTimeProcessed = totalHours;
            return false;
        }
        
        float transitionRate = Settings.BaseCompostRatePerHour * GetFactor();
        double durationTransitions = (totalHours - PrevTimeProcessed) * transitionRate;
        int transitions = (int)Math.Min(durationTransitions, bulkPortions);
        if (transitions < 1)
            return false; // keep accruing progress
        
        (int compostOutput, int failedOutput) = ResolveOutputTransitions(transitions);
        int actualTransitions = compostOutput + failedOutput;
        if (actualTransitions < 1)
            return false; // keep accruing progress
        
        (float brownsInputPortions, float nutritionInputPortions) = ResolveInputPortions(actualTransitions, brownsPortions, nutritionPortions);
        
        BrownsQty -= (int)Math.Min(brownsInputPortions * Settings.Browns.ConsumePerTransition, BrownsQty);
        TryRemoveRandomNutrition(be.Api.World.Rand, (int)(nutritionInputPortions * Settings.Nutrition.ConsumePerTransition));

        CompostQty = Math.Clamp
            (CompostQty + compostOutput * Settings.CompostOutPerSuccess
            ,0,Settings.Inoculum.MaxQty
            );
        
        InoculumQty = Math.Clamp
           (InoculumQty
        +   failedOutput * Settings.InoculumOutPerFail
        -   compostOutput * Settings.Inoculum.ConsumePerTransition
            ,0,Settings.Inoculum.MaxQty - CompostQty
            );

        PrevTimeProcessed += Math.Floor(durationTransitions) / transitionRate;
        return true;
    }

    private (int compostOutput, int failedOutput) ResolveOutputTransitions(int transitions)
    {
        int failedOutput = (int)(transitions * Stress01);
        int compostOutput = transitions - failedOutput;

        ClampFailedToOutputRoom(ref failedOutput, out int failedOverflow);
        compostOutput += failedOverflow;

        ClampCompostToOutputRoom(ref compostOutput, out int compostOverflow);
        failedOutput += compostOverflow;
        
        BootstrapCompostWithFailed(ref compostOutput, ref failedOutput);
        
        ClampFailedToFinalRoom(ref compostOutput, ref failedOutput);
        
        return (compostOutput, failedOutput);
    }

    private void ClampFailedToOutputRoom(ref int failedOutput, out int transitionsOverflow)
    {
        int failedOutputRoom = GetInoculumRoomQty() / Settings.InoculumOutPerFail;
        if (failedOutput <= failedOutputRoom)
        {
            transitionsOverflow = 0;
            return;
        }
        
        transitionsOverflow = failedOutput - failedOutputRoom;
        failedOutput = failedOutputRoom;
    }

    private void ClampCompostToOutputRoom(ref int compostOutput, out int transitionsOverflow)
    {
        int compostOutputNet = Settings.CompostOutPerSuccess - Settings.Inoculum.ConsumePerTransition;
        if (compostOutputNet < 1)
        {
            transitionsOverflow = 0;
            return;
        }
        
        int compostOutputRoom = GetInoculumRoomQty() / compostOutputNet;
        if (compostOutput <= compostOutputRoom)
        {
            transitionsOverflow = 0;
            return;
        }
        
        transitionsOverflow = compostOutput - compostOutputRoom;
        compostOutput = compostOutputRoom;
    }

    private void BootstrapCompostWithFailed(ref int compostOutput, ref int failedOutput)
    {
        int inoculumAfterFailedQty = InoculumQty + failedOutput * Settings.InoculumOutPerFail;
        int compostPossibleByInoculum = inoculumAfterFailedQty / Settings.Inoculum.ConsumePerTransition;
        if (compostOutput <= compostPossibleByInoculum)
            return;
        
        int overflowByInoculumLimit = compostOutput - compostPossibleByInoculum;
        int compostSubsidizedByFailed = 
            overflowByInoculumLimit * Settings.InoculumOutPerFail
        /  (Settings.InoculumOutPerFail + Settings.Inoculum.ConsumePerTransition);

        compostOutput = compostPossibleByInoculum + compostSubsidizedByFailed;
        failedOutput += overflowByInoculumLimit - compostSubsidizedByFailed;
    }
    
    private void ClampFailedToFinalRoom(ref int compostOutput, ref int failedOutput)
    {
        int inoculumChangeQty = 
            failedOutput * Settings.InoculumOutPerFail
        +   compostOutput * (Settings.CompostOutPerSuccess - Settings.Inoculum.ConsumePerTransition);
        int inoculumRoomQty = GetInoculumRoomQty();
    
        if (inoculumChangeQty <= inoculumRoomQty)
            return;
        
        int inoculumExcess = (int)Math.Ceiling((float)(inoculumChangeQty - inoculumRoomQty) / Settings.InoculumOutPerFail);
        failedOutput = Math.Max(failedOutput - inoculumExcess, 0);
    }
    
    private (float brownsInputPortions, float nutritionInputPortions) ResolveInputPortions(int actualTransitions, float brownsPortions, float nutritionPortions)
    {
        float minBrowns = Math.Max(actualTransitions - nutritionPortions, 0f);
        float maxBrowns = Math.Min(actualTransitions, brownsPortions);

        float brownsInputPortions;
        if (maxBrowns > minBrowns)
        {
            float mean = actualTransitions * (brownsPortions / (brownsPortions + nutritionPortions));
            brownsInputPortions = Math.Clamp(mean, minBrowns, maxBrowns);
        }
        else
            brownsInputPortions = minBrowns;
        float nutritionInputPortions = actualTransitions - brownsInputPortions;
        
        return (brownsInputPortions, nutritionInputPortions);
    }
    #endregion


    #region Visuals
    public Vec4f GetVisualTintRgba()
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
            (Math.Clamp(red * brightness, 0,1)
            ,Math.Clamp(green * brightness, 0,1)
            ,Math.Clamp(blue * brightness, 0,1)
            , 1f
            );
    }
    #endregion


    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree, string? key = null)
    {
        tree.SetDouble($"{key}.PrevTimeMoistureUpdated", PrevTimeMoistureUpdated);
        tree.SetFloat($"{key}.Moisture01", Moisture01);

        tree.SetDouble($"{key}.PrevTimeProcessed", PrevTimeProcessed);

        tree.SetInt($"{key}.BrownsQty", BrownsQty);
        tree.SetInt($"{key}.InoculumQty", InoculumQty);
        tree.SetInt($"{key}.CompostQty", CompostQty);

        tree.SetInt($"{key}.NutritionStacks.Count", NutritionStacks?.Count ?? 0);
        if (NutritionStacks is not null)
        {
            int i = 0;
            foreach (var stack in NutritionStacks)
            {
                tree.SetInt($"{key}.NutritionStacks<{i}>", (int)stack.Key);
                tree.SetInt($"{key}.NutritionStacks[{i}]", stack.Value);
                i++;
            }
        }
        
        tree.SetDouble($"{key}._prevTimeTemperatureUpdated", _prevTimeTemperatureUpdated);
        tree.SetFloat($"{key}._temperature", _temperature);

        tree.SetDouble($"{key}._prevTimeAerationUpdated", _prevTimeAerationUpdated);
        tree.SetFloat($"{key}._aeration01", _aeration01);

        tree.SetDouble($"{key}._prevTimeStressUpdated", _prevTimeStressUpdated);
        tree.SetFloat($"{key}.Stress01", Stress01);
    }

    public void FromTreeAttributes(ITreeAttribute tree, string? key = null)
    {
        PrevTimeMoistureUpdated = tree.GetDouble($"{key}.PrevTimeMoistureUpdated", -1);
        Moisture01 = tree.GetFloat($"{key}.Moisture01");

        PrevTimeProcessed = tree.GetDouble($"{key}.PrevTimeProcessed", -1);

        BrownsQty = tree.GetInt($"{key}.BrownsQty");
        InoculumQty = tree.GetInt($"{key}.InoculumQty");
        CompostQty = tree.GetInt($"{key}.CompostQty");

        NutritionStacks.Clear();
        int nutritionLength = tree.GetInt($"{key}.NutritionStacks.Count");
        for (int i = 0; i < nutritionLength; i++)
            NutritionStacks[(EnumFoodCategory)tree.GetInt($"{key}.NutritionStacks<{i}>")] = tree.GetInt($"{key}.NutritionStacks[{i}]");
        
        _prevTimeTemperatureUpdated = tree.GetDouble($"{key}._prevTimeTemperatureUpdated", -1);
        _temperature = tree.GetFloat($"{key}._temperature");

        _prevTimeAerationUpdated = tree.GetDouble($"{key}._prevTimeAerationUpdated", -1);
        _aeration01 = tree.GetFloat($"{key}._aeration01", 1f);

        _prevTimeStressUpdated = tree.GetDouble($"{key}._prevTimeStressUpdated", -1);
        Stress01 = tree.GetFloat($"{key}.Stress01", 0f);
    }
    #endregion
}