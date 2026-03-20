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
    
    //  Stress impacts Compost/SourCompost output ratio
    public float GetStress01() => 1f - GetHealth01();
    public float GetHealth01() =>
        GetAerationHealth01()
    *   GetTemperatureHealth01()
    *   GetMoistureHealth01();
    
    
    public float GetInoculumFactor01() =>
        Math.Clamp((float)(InoculumQty + CompostQty) / (Settings.Inoculum.MaxQty + Settings.CompostMaxQty), 0.1f, 1f);
    
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
    
    private float GetNutritionHeat()
    {
        float nutritionHeat = 0f;
        if (Settings.NutritionHeat is null)
            return 0f;

        foreach (var kvp in NutritionStacks)
            if (Settings.NutritionHeat.TryGetValue(kvp.Key.ToString(), out float heatC))
                nutritionHeat += heatC * kvp.Value / Math.Max(1f, Settings.Nutrition.MaxQty);

        return nutritionHeat;
    }
    
    
    public float GetAerationHealth01() => 1f - GetAerationStress01();
    public float GetAerationStress01()
    {
        if (_aeration01 < Settings.HypoxicThreshold)
            return Math.Clamp((Settings.HypoxicThreshold - _aeration01) / Settings.HypoxicTolerance, 0,1);
        return 0;
    }
    #endregion
    
    
    public bool CanHarvest() =>
        CompostQty >= Settings.CompostOutPerSuccess
    ||  InoculumQty >= Settings.InoculumPerSourDropped
    || (BrownsQty >= Settings.Browns.InitialQty
    &&  NutritionQty >= Settings.Nutrition.InitialQty
    &&  InoculumQty >= Settings.Inoculum.InitialQty
        );

    public int GetHarvestableCompostpileQty() =>
        Math.Min(Math.Min
            (BrownsQty / Settings.Browns.InitialQty
            ,NutritionQty / Settings.Nutrition.InitialQty
           ),InoculumQty / Settings.Inoculum.InitialQty
            );
    public bool HarvestCompostpile(BlockEntity be, float dropQuantityMultiplier)
    {
        int compostpileQty = GetHarvestableCompostpileQty();
        if (compostpileQty < 1)
            return false;

        int qty = be.Api.World.Rand.Next(compostpileQty) + 1;
        
        Block spawnBlock = be.Api.World.GetBlock(new AssetLocation("oddwire:Compostpile-#1"));

        int remaining = (int)Math.Ceiling(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = be.Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        BrownsQty = Math.Max(BrownsQty - Settings.Browns.InitialQty * qty, 0);
        TryRemoveRandomNutrition(be.Api.World.Rand, Settings.Nutrition.InitialQty * qty);
        InoculumQty = Math.Max(InoculumQty - Settings.Inoculum.InitialQty * qty, 0);

        return true;
    }
    
    public int GetHarvestableSourCompostQty() => InoculumQty / Settings.InoculumPerSourDropped;
    public bool HarvestSourCompost(BlockEntity be, float dropQuantityMultiplier)
    {
        int sourQty = GetHarvestableSourCompostQty();
        if (sourQty < 1)
            return false;

        int qty = be.Api.World.Rand.Next(sourQty) + 1;
        
        Item spawnBlock = be.Api.World.GetItem(new AssetLocation("oddwire:sourcompost"));

        int remaining = (int)Math.Ceiling(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = be.Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        InoculumQty = Math.Max(InoculumQty - Settings.InoculumPerSourDropped * qty, 0);
        return true;
    }
    
    public int GetHarvestableCompostQty() => CompostQty;
    public bool HarvestCompost(BlockEntity be, float dropQuantityMultiplier)
    {
        int compostQty = GetHarvestableCompostQty();
        if (compostQty < 1)
            return false;

        int qty = be.Api.World.Rand.Next(compostQty) + 1;

        Item spawnItem = be.Api.World.GetItem(new AssetLocation("game:compost"));

        int remaining = (int)Math.Ceiling(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = be.Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            be.Api.World.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(be.Api.World.Rand.NextDouble(), 0.5, be.Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        CompostQty = Math.Max(CompostQty - qty, 0);
        return true;
    }
    
    
    public void ResetOnPlaced(Block block)
    {
        int stackBonus = 0;
        if (int.TryParse(block.LastCodePart().Substring(1), out int parsedStackBonus))
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

        if (TryAddCompostPile(slot, out accepted))
        {
            RestoreAeration01(be, accepted * Settings.Aeration01PerCompostpileInput);
            return true;
        }
            
        if (TryAddRef(slot, out accepted, ref BrownsQty, Settings.Browns))
        {
            RestoreAeration01(be, accepted * Settings.Browns.Aeration01PerInput);
            return true;
        }
            
        if (TryAddRef(slot, out accepted, ref InoculumQty, Settings.Inoculum))
        {
            RestoreAeration01(be, accepted * Settings.Inoculum.Aeration01PerInput);
            return true;
        }
            
        if (TryAddNutrition(slot, out accepted))
        {
            RestoreAeration01(be, accepted * Settings.Nutrition.Aeration01PerInput);
            return true;
        }

        return false;
    }
    
    public bool TryAddRef(ItemSlot slot, out int accepted, ref int currentQty, CompostpileSettings.Ingredient ingredient)
    {
        accepted = 0;
        if (ingredient.ItemCodeAddRatios is null
        ||  ingredient.ItemCodeAddRatios.Count == 0
           )
            return false;

        int room = ingredient.MaxQty - currentQty;
        if (room < 1
        ||  slot.StackSize < 1
            )
            return false;

        string code =
            slot.Itemstack?.Item?.Code.ToString()
        ??  slot.Itemstack?.Block?.Code.ToString()
        ??  "";
        
        if(!ingredient.ItemCodeAddRatios.TryGetValue(code, out float ratio)
        ||  ratio <= 0f
        ||  slot.StackSize < Math.Max(ratio, 1)
            )
            return false;

        int adjustedLimit = 
            ratio >= 1f
        ?   (int)(Math.Min(ingredient.MaxInputPerAdd, room) * ratio)
        :   (int)Math.Min(ingredient.MaxInputPerAdd, room * ratio);
        
        int adjustedInput = Math.Min(slot.StackSize, adjustedLimit);
        if (ratio >= 1f)
            adjustedInput = (int)(Math.Floor(adjustedInput / ratio) * ratio);
        
        int adjustedOutput = (int)Math.Min(adjustedInput / ratio, room);

        currentQty += adjustedOutput;
        accepted = adjustedInput;

        
        return accepted > 0;
    }

    private bool TryAddCompostPile(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        AssetLocation blockCode = slot.Itemstack?.Block?.Code;
        if (string.IsNullOrEmpty(blockCode)
        || !blockCode.BeginsWith("oddwire","compostpile")
        || !int.TryParse(blockCode.EndVariant().Substring(1), out int stackBonus)
           )
            return false;

        stackBonus = Math.Max(stackBonus - 1, 0);
        
        int brownsAdd = Settings.Browns.InitialQty + stackBonus * Settings.Browns.SizeBonusQty;
        int nutritionAdd = Settings.Nutrition.InitialQty + stackBonus * Settings.Nutrition.SizeBonusQty;
        int inoculumAdd = Settings.Inoculum.InitialQty + stackBonus * Settings.Inoculum.SizeBonusQty;

        if (brownsAdd > Settings.Browns.MaxQty - BrownsQty
        ||  nutritionAdd > Settings.Nutrition.MaxQty - NutritionQty
        ||  inoculumAdd > Settings.Inoculum.MaxQty - InoculumQty
            )
            return false;

        BrownsQty += brownsAdd;
        NutritionStacks.TryGetValue(EnumFoodCategory.Unknown, out var cur);
        NutritionStacks[EnumFoodCategory.Unknown] = cur + nutritionAdd;
        InoculumQty += inoculumAdd;

        accepted = 1;
        return true;
    }
    
    private bool TryAddNutrition(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        var collectible = slot.Itemstack?.Collectible;
        var nutritionProps = collectible?.NutritionProps;
        if (nutritionProps is null)
            return false;

        int room = Settings.Nutrition.MaxQty - NutritionQty;
        if (room < 1)
            return false;

        int ratio = CompostpileSettings.Ingredient.GetStackNormalizedRatio(collectible);
        if (slot.StackSize < ratio)
            return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, Settings.Nutrition.MaxInputPerAdd);

        NutritionStacks.TryGetValue(nutritionProps.FoodCategory, out var cur);
        NutritionStacks[nutritionProps.FoodCategory] = cur + adjustedAccept;

        accepted = adjustedAccept * ratio;
        return true;
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
        while (remaining > 0 && keys.Count > 0 && nutritionRemaining > 0)
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
    private float _insulation01;
    private void PreUpdateState(BlockEntity be, double totalHours)
    {
        if (_lastPreUpdatedHours + 1 > totalHours)
            return;
        
        _skyExposed = be.Api.World.BlockAccessor.IsSkyExposed(be.Pos);
        _envTemp = be.Api.GetEnvironmentTemperatureC(be.Pos, totalHours, _skyExposed, Settings.GreenhouseHeat, out bool isInGreenhouse);
        
        _insulation01 = 0.25f + 0.75f * GetFullness01();
        if (!_skyExposed)
            _insulation01 += 0.10f;
        if (isInGreenhouse)
            _insulation01 += 0.05f;
        _insulation01 = Math.Clamp(_insulation01, 0, 1);

        _lastPreUpdatedHours = totalHours;
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
        
        float dtMoistureDays = (float)Math.Clamp((totalHours - PrevTimeMoistureUpdated) / be.Api.World.Calendar.HoursPerDay, 0, 9);
        if (dtMoistureDays <= 0)
            return false;
        
        float rainfallHours = 0;

        if (_skyExposed)
        {
            _weather ??= be.Api.ModLoader.GetModSystem<WeatherSystemBase>();
            rainfallHours = _weather?.GetTotalRainfallSince(be.Pos, PrevTimeMoistureUpdated, totalHours) ?? 0f;
        }
        
        if (rainfallHours > 0f)
            Moisture01 += rainfallHours / be.Api.World.Calendar.HoursPerDay * Settings.Moisture01GainPerRainyDay;

        float ambientDrying01 = Math.Clamp(0.6f + _envTemp / 35f, 0.25f, 1.75f);
        float retention01 = GameMath.Lerp(1.15f, 0.75f, _insulation01);

        Moisture01 -= dtMoistureDays / Settings.MoistureRetentionDays * ambientDrying01 * retention01;
        Moisture01 = Math.Clamp(Moisture01, 0f, 1f);
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
        
        float dtAerationDays = (float)Math.Clamp((totalHours - _prevTimeAerationUpdated) / be.Api.World.Calendar.HoursPerDay, 0, 9);
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
        
        float dtTemperatureHours = (float)Math.Clamp(totalHours - _prevTimeTemperatureUpdated, 0, 24);
        if (dtTemperatureHours <= 0f)
            return false;
        
        float nutrition01 = Math.Clamp((float)NutritionQty / Settings.Nutrition.MaxQty, 0f, 1f);
        float targetTemp =
            _envTemp
        +   Settings.HeatingRatePerHour * _insulation01 * nutrition01
        +   GetNutritionHeat();

        float coolingInsulation = GameMath.Lerp(1.6f, 0.7f, _insulation01);
        float coolingRate = Math.Clamp(Settings.CoolingRatePerHour / coolingInsulation, 0.01f, 0.5f);

        _temperature += (targetTemp - _temperature) * (1f - (float)Math.Exp(-coolingRate * dtTemperatureHours));
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

        float dtStressDays = (float)Math.Clamp((totalHours - _prevTimeStressUpdated) / be.Api.World.Calendar.HoursPerDay, 0, 9);
        if (dtStressDays <= 0f)
            return false;

        float targetStress01 = GetStress01();
        if (targetStress01 > Stress01)
            Stress01 += dtStressDays / Settings.StressGainDays;
        else
            Stress01 -= dtStressDays / Settings.StressRecoveryDays;

        Stress01 = Math.Clamp(Math.Max(Stress01, targetStress01), 0,1);
        _prevTimeStressUpdated = totalHours;

        return true;
    }
    
    private bool ProcessCompost(BlockEntity be, double totalHours)
    {
        if (PrevTimeProcessed < 0
        ||  PrevTimeProcessed > totalHours
        || (InoculumQty >= Settings.Inoculum.MaxQty
        &&  CompostQty >= Settings.CompostMaxQty
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
        
        int transitions = (int)Math.Min
            ((totalHours - PrevTimeProcessed) * Settings.BaseCompostRatePerHour * GetFactor()
            ,bulkPortions
            );
        if (transitions < 1)
            return false; // keep accruing progress
        
        (int compostOutput, int sourOutput) = ResolveOutputTransitions(transitions);
        int actualTransitions = compostOutput + sourOutput;
        if (actualTransitions < 1)
            return false; // keep accruing progress
        
        (float brownsInputPortions, float nutritionInputPortions) = ResolveInputPortions(be.Api.World.Rand, actualTransitions, brownsPortions, nutritionPortions);
        
        BrownsQty -= (int)Math.Min(brownsInputPortions * Settings.Browns.ConsumePerTransition, BrownsQty);
        TryRemoveRandomNutrition(be.Api.World.Rand, (int)(nutritionInputPortions * Settings.Nutrition.ConsumePerTransition));

        InoculumQty = Math.Clamp
           (InoculumQty
        +   sourOutput * Settings.InoculumOutPerFail
        -   compostOutput * Settings.Inoculum.ConsumePerTransition
           ,0,Settings.Inoculum.MaxQty
            );

        CompostQty = Math.Clamp
            (CompostQty + compostOutput * Settings.CompostOutPerSuccess
            ,0,Settings.CompostMaxQty
            );

        PrevTimeProcessed = totalHours;
        return true;
    }

    private (int compostOutput, int sourOutput) ResolveOutputTransitions(int transitions)
    {
        int sourOutput = (int)(transitions * Stress01);
        int compostOutput = transitions - sourOutput;

        // clamp(sour){compost+=overflow}
        int sourOutputRoom = (Settings.Inoculum.MaxQty - InoculumQty) / Settings.InoculumOutPerFail;
        if (sourOutput > sourOutputRoom)
        {
            int sourOverflow = sourOutput - sourOutputRoom;
            sourOutput = sourOutputRoom;
            compostOutput += sourOverflow;
        }

        // clamp(compost){sour+=overflow}
        int compostOutputRoom = (Settings.CompostMaxQty - CompostQty) / Settings.CompostOutPerSuccess;
        if (compostOutput > compostOutputRoom)
        {
            int compostOverflow = compostOutput - compostOutputRoom;
            compostOutput = compostOutputRoom;
            sourOutput += compostOverflow;
            compostOutputRoom = 0;
        }

        // bootstrap(compost with sour)
        int inoculumAfterSourQty = InoculumQty + sourOutput * Settings.InoculumOutPerFail;
        int compostPossibleByInoculum = inoculumAfterSourQty / Settings.Inoculum.ConsumePerTransition;
        if (compostOutput > compostPossibleByInoculum)
        {
            int overflowByInoculumLimit = compostOutput - compostPossibleByInoculum;

            int compostSubsidizedBySour = Math.Min
                (overflowByInoculumLimit * Settings.InoculumOutPerFail
            /   (Settings.InoculumOutPerFail + Settings.Inoculum.ConsumePerTransition)
                ,compostOutputRoom
                );

            compostOutput = compostPossibleByInoculum + compostSubsidizedBySour;
            sourOutput += overflowByInoculumLimit - compostSubsidizedBySour;
        }

        // clamp(sour, room)
        int inoculumChangeQty = 
            sourOutput * Settings.InoculumOutPerFail
        -   compostOutput * Settings.Inoculum.ConsumePerTransition;
        int inoculumRoomQty = Settings.Inoculum.MaxQty - InoculumQty;
        if (inoculumChangeQty > inoculumRoomQty)
        {
            int inoculumExcess = (int)Math.Ceiling((float)(inoculumChangeQty - inoculumRoomQty) / Settings.InoculumOutPerFail);
            sourOutput = Math.Max(sourOutput - inoculumExcess, 0);
        }
        
        return (compostOutput, sourOutput);
    }

    private (float brownsInputPortions, float nutritionInputPortions) ResolveInputPortions
        (Random rand
        ,int actualTransitions, float brownsPortions, float nutritionPortions
        )
    {
        float minBrowns = Math.Max(actualTransitions - nutritionPortions, 0f);
        float maxBrowns = Math.Min(actualTransitions, brownsPortions);

        float brownsInputPortions;
        if (maxBrowns > minBrowns)
        {
            float noise = 0.2f * (rand.NextSingle() - 0.5f) * (maxBrowns - minBrowns);
            float mean = actualTransitions * (brownsPortions / (brownsPortions + nutritionPortions));
            brownsInputPortions = Math.Clamp(mean + noise, minBrowns, maxBrowns);
        }
        else
            brownsInputPortions = minBrowns;
        float nutritionInputPortions = actualTransitions - brownsInputPortions;
        
        return (brownsInputPortions, nutritionInputPortions);
    }
    
    
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
}