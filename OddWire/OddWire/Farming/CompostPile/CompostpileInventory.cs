using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public sealed class CompostpileInventory
{
    public static CompostpileSettings Settings { get; } = new
        (browns: new CompostpileIngredientSettings
            (name: "browns"
                ,initQty: 16
                ,placedBonusQty: 44
                ,maxQty: 64 * 3
                ,maxInput: 16
                ,inPerCompostPortion: 16
                ,inPerSourPortion: 8
                ,addItemCodeRatios: new Dictionary<string, float>
                {{"game:drygrass", 1f}
                }
            )
        ,nutrition: new CompostpileIngredientSettings
            (name: "nutrition"
                ,initQty: 16
                ,placedBonusQty: 12
                ,maxQty: 64
                ,maxInput: 8
                ,inPerCompostPortion: 8
                ,inPerSourPortion: 4
            )
        ,inoculum: new CompostpileIngredientSettings
            (name: "inoculum"
                ,initQty: 2
                ,placedBonusQty: 8
                ,maxQty: 16
                ,maxInput: 4
                ,inPerCompostPortion: 1
                ,inPerSourPortion: 1
                ,addItemCodeRatios: new Dictionary<string, float>
                {{"game:compost", 1f}
                ,{"game:rot", 2}
                ,{"oddwire:sourcompost", 4}
                }
            )
            ,baseCompostRatePerHour: 0.33f
            ,defaultMoisture01: 0.55f
            ,optimalMoisture01: 0.60f
            ,rainToMoisturePerDay: 0.40f
            ,dryoutPerDayAt20C: 0.25f
            ,greenhouseTempBonusC: 5f
                
            ,outputMaxQty: 48
            ,outputOutPerCompostPortion: 1
            ,inoculumOutPerSourPortion: 1
                
            ,harvestMaxPerStack: 8
        );

    public double PrevTimeMoistureUpdated = -1;
    public float Moisture01;

    public double PrevTimeComposted = -1;

    public int BrownsQty;
    public readonly Dictionary<EnumFoodCategory, int> NutritionStacks = new();
    public int InoculumQty;
    public int OutputQty;

    public int NutritionQty
    { get {
        int sum = 0;
        foreach (var kvp in NutritionStacks)
            sum += kvp.Value;
        return sum;
    } }


    #region RateHelpers
    public float GetCompostRatePerHour(ICoreAPI api, Block block, BlockPos pos, double totalHours)
    {
        if (InoculumQty < 1 && OutputQty < 1)
            return 0f;

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, Settings.GreenhouseTempBonusC, out _);

        return
            Settings.BaseCompostRatePerHour
        *   GetInoculumFactor01()
        *   GetTemperatureFactor01(envTemp)
        *   GetMoistureFactor01(Moisture01)
        *   GetNutritionFactor01(block);
    }

    public float GetSpoilRate01(ICoreAPI api, Block block, BlockPos pos, double totalHours)
        => Math.Clamp(GetSpoilRate(api, block, pos, totalHours), 0f, 1f);
    
    public float GetSpoilRate(ICoreAPI api, Block block, BlockPos pos, double totalHours)
    {
        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, Settings.GreenhouseTempBonusC, out _);

        JsonObject? spoilTemps = block.Attributes?["spoilTempByCategory"];
        if (spoilTemps is null
        ||  NutritionStacks.Count == 0
           )
            return 0f;

        float tempRisk01 = 0f;
        foreach (var kvp in NutritionStacks)
        {
            string keyA = kvp.Key.ToString();
            float thresh = spoilTemps[keyA]?.AsFloat(float.NaN) ?? float.NaN;
            if (float.IsNaN(thresh)) continue;

            if (envTemp > thresh)
            {
                float risk = Math.Clamp((envTemp - thresh) / 15f, 0f, 1f);
                if (risk > tempRisk01) tempRisk01 = risk;
            }
        }

        float moistureRisk01 = 0f;
        if (Moisture01 < 0.05f)
            moistureRisk01 = Math.Max(moistureRisk01, 0.6f * Math.Clamp((0.05f - Moisture01) / 0.05f, 0f, 1f));
        else if (Moisture01 > 0.85f)
            moistureRisk01 = Math.Clamp((Moisture01 - 0.85f) / 0.15f, 0f, 1f);

        return 1f - (1f - tempRisk01) * (1f - moistureRisk01);
    }
    
    
    public float GetInoculumFactor01() =>
        Math.Clamp((float)(InoculumQty + OutputQty) / (Settings.Inoculum.MaxQty + Settings.OutputMaxQty), 0.1f, 1f);
    
    public float GetTemperatureFactor01(float tempC)
    {
        if (tempC <  0) return 0.05f;
        if (tempC < 10) return GameMath.Lerp(0.05f, 0.6f, (tempC - 0f) / 10f);
        if (tempC < 20) return GameMath.Lerp(0.6f, 1.0f, (tempC - 10f) / 10f);
        if (tempC < 55) return 1.0f;
        if (tempC < 70) return GameMath.Lerp(1.0f, 0.35f, (tempC - 55f) / 15f);
        return 0.10f;
    }
    
    public float GetMoistureFactor01(float moisture01)
    {
        if (moisture01 <= 0.05f) return 0.05f;

        float factor = moisture01 <= Settings.OptimalMoisture01
        ?   GameMath.Lerp(0.1f, 1.0f, (moisture01 - 0.05f) / (Settings.OptimalMoisture01 - 0.05f))
        :   GameMath.Lerp(1.0f, 0.25f, (moisture01 - Settings.OptimalMoisture01) / (1f - Settings.OptimalMoisture01));
        
        if (moisture01 > 0.9f)
            factor *= 0.6f;
        return Math.Clamp(factor, 0.05f, 1.0f);
    }
    
    public float GetNutritionFactor01(Block block)
    {
        if (NutritionStacks.Count < 1)
            return 0f;

        JsonObject? speedByCat = block.Attributes?["nutritionSpeedByCategory"];
        float weighted = 0f;

        foreach (var kvp in NutritionStacks)
        {
            float mult = speedByCat?[kvp.Key.ToString()]?.AsFloat(1f) ?? 1f;
            weighted += mult * kvp.Value;
        }

        return weighted / Settings.Nutrition.MaxQty;
    }
    #endregion
    
    public bool CanHarvest(out int compostPileQty, out int sourCompostQty, out int compostQty)
    {
        int bulkPortions = (int)((float)BrownsQty / Settings.Browns.InitQty + (float)NutritionQty / Settings.Nutrition.InitQty);
        compostPileQty = Math.Min(bulkPortions, InoculumQty / Settings.Inoculum.InitQty);
        sourCompostQty = Math.Max(InoculumQty - bulkPortions * Settings.Inoculum.InitQty, 0);
        compostQty = OutputQty;

        return compostPileQty > 0 || sourCompostQty > 0 || compostQty > 0;
    }
    
    public void ResetQuantitiesOnPlaced(Block block)
    {
        int.TryParse(block.LastCodePart().Substring(1), out int stackBonus);
        stackBonus = Math.Max(0, stackBonus - 1);

        BrownsQty = Settings.Browns.InitQty + stackBonus * Settings.Browns.PlacedBonusQty;

        NutritionStacks.Clear();
        NutritionStacks[EnumFoodCategory.Unknown] = Settings.Nutrition.InitQty + stackBonus * Settings.Nutrition.PlacedBonusQty;

        InoculumQty = Settings.Inoculum.InitQty + stackBonus * Settings.Inoculum.PlacedBonusQty;
        OutputQty = 0;

        if (Moisture01 <= 0f && PrevTimeMoistureUpdated < 0)
            Moisture01 = Settings.DefaultMoisture01;
    }
    
    public bool TryAdd(ICoreAPI api, ItemSlot slot, out int accepted)
    {
        accepted = 0;
        if (slot.StackSize < 1)
            return false;

        if (TryAddCompostPile(slot, out accepted)
        ||  Settings.Browns.TryAddRef(slot, out accepted, ref BrownsQty)
        ||  TryAddNutrition(slot, out accepted)
        ||  Settings.Inoculum.TryAddRef(slot, out accepted, ref InoculumQty)
            )
            return accepted > 0;

        return false;
    }

    private bool TryAddCompostPile(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        AssetLocation blockCode = slot.Itemstack?.Block?.Code;
        if (string.IsNullOrEmpty(blockCode)
        || !blockCode.BeginsWith("oddwire","compmpostpile")
        || !int.TryParse(blockCode.EndVariant().Substring(1), out int stackBonus)
           )
            return false;

        stackBonus = Math.Max(stackBonus - 1, 0);
        
        int brownsAdd = Settings.Browns.InitQty + stackBonus * Settings.Browns.PlacedBonusQty;
        int nutritionAdd = Settings.Nutrition.InitQty + stackBonus * Settings.Nutrition.PlacedBonusQty;
        int inoculumAdd = Settings.Inoculum.InitQty + stackBonus * Settings.Inoculum.PlacedBonusQty;

        if (brownsAdd > Settings.Browns.MaxQty - BrownsQty
        ||  nutritionAdd > Settings.Nutrition.MaxQty - NutritionQty
        ||  inoculumAdd > Settings.Inoculum.MaxQty - InoculumQty
            )
            return false;

        BrownsQty += brownsAdd;
        NutritionStacks[EnumFoodCategory.Unknown] += nutritionAdd;
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

        int ratio = CompostpileIngredientSettings.GetStackNormalizedRatio(collectible);
        if (slot.StackSize < ratio)
            return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, Settings.Nutrition.MaxInput);

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

    
    public void UpdateMoisture(ICoreAPI api, BlockPos pos, double totalHours)
    {
        if (PrevTimeMoistureUpdated < 0)
            PrevTimeMoistureUpdated = totalHours;

        float dtDays = (float)Math.Min((totalHours - PrevTimeMoistureUpdated) / 24.0, 14.0);

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        if (skyExposed)
        {
            var conds = api.World.GetClimateAtHours(pos, totalHours);
            float wetGain = Math.Clamp(conds?.Rainfall ?? 0f, 0f, 1f) * dtDays * Settings.RainToMoisturePerDay;
            Moisture01 = Math.Clamp(Moisture01 + wetGain, 0f, 1f);
        }

        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, Settings.GreenhouseTempBonusC, out bool inGreenhouse);

        float tempDryMultiplier = Math.Clamp(0.5f + envTemp / 40f, 0.2f, 2.0f);
        float shelterMultiplier = (skyExposed ? 1.0f : 0.75f) * (inGreenhouse ? 0.85f : 1.0f);

        float dryLoss = dtDays * Settings.DryoutPerDayAt20C * tempDryMultiplier * shelterMultiplier;
        Moisture01 = Math.Clamp(Moisture01 - dryLoss, 0f, 1f);

        PrevTimeMoistureUpdated = totalHours;
    }
    
    public bool ProcessCompost(ICoreAPI api, Block block, BlockPos pos, double totalHours)
    {
        if (PrevTimeComposted < 0
        || (InoculumQty >= Settings.Inoculum.MaxQty
        &&  OutputQty >= Settings.OutputMaxQty
           ))
        {
            PrevTimeComposted = totalHours;
            return false;
        }

        float brownsPortions = (float)BrownsQty / Settings.Browns.InPerCompostPortion;
        float nutritionPortions = (float)NutritionQty / Settings.Nutrition.InPerCompostPortion;
        float bulkPortions = brownsPortions + nutritionPortions;

        if (bulkPortions < 1f)
        {
            PrevTimeComposted = totalHours;
            return false;
        }

        int transitions = (int)Math.Min
           ((totalHours - PrevTimeComposted) * GetCompostRatePerHour(api, block, pos, totalHours)
            ,bulkPortions
            );
        if (transitions < 1)
            return false; // keep accruing progress

        int sourOutputPortions = (int)(transitions * GetSpoilRate01(api, block, pos, totalHours));
        int compostOutputPortions = transitions - sourOutputPortions;

        // clamp(sour){compost+=overflow}
        int sourOutputRoomPortions = (Settings.Inoculum.MaxQty - InoculumQty) / Settings.InoculumOutPerSourPortion;
        if (sourOutputPortions > sourOutputRoomPortions)
        {
            int sourOverflowPortions = sourOutputPortions - sourOutputRoomPortions;
            sourOutputPortions = sourOutputRoomPortions;
            compostOutputPortions += sourOverflowPortions;
        }

        // clamp(compost){sour+=overflow}
        int compostOutputRoomPortions = (Settings.OutputMaxQty - OutputQty) / Settings.OutputOutPerCompostPortion;
        if (compostOutputPortions > compostOutputRoomPortions)
        {
            int compostOverflowPortions = compostOutputPortions - compostOutputRoomPortions;
            compostOutputPortions = compostOutputRoomPortions;
            sourOutputPortions += compostOverflowPortions;
            compostOutputRoomPortions = 0;
        }

        // bootstrap(compost with sour)
        int inoculumAfterSourQty = InoculumQty + sourOutputPortions * Settings.InoculumOutPerSourPortion;
        int compostPossibleByInoculumPortions = inoculumAfterSourQty / Settings.Inoculum.InPerCompostPortion;
        if (compostOutputPortions > compostPossibleByInoculumPortions)
        {
            int overflowByInoculumLimitsPortions = compostOutputPortions - compostPossibleByInoculumPortions;

            int compostSubsidizedBySourPortions = Math.Min
                (overflowByInoculumLimitsPortions * Settings.InoculumOutPerSourPortion
            /   (Settings.InoculumOutPerSourPortion + Settings.Inoculum.InPerCompostPortion)
                ,compostOutputRoomPortions
                );

            compostOutputPortions = compostPossibleByInoculumPortions + compostSubsidizedBySourPortions;
            sourOutputPortions += overflowByInoculumLimitsPortions - compostSubsidizedBySourPortions;
        }

        // clamp(sour, room)
        int inoculumChangeQty = 
            sourOutputPortions * Settings.InoculumOutPerSourPortion
        -   compostOutputPortions * Settings.Inoculum.InPerCompostPortion;
        int inoculumRoomQty = Settings.Inoculum.MaxQty - InoculumQty;
        if (inoculumChangeQty > inoculumRoomQty)
        {
            int inoculumExcessPortions = (inoculumChangeQty - inoculumRoomQty) / Settings.InoculumOutPerSourPortion;
            sourOutputPortions = Math.Max(sourOutputPortions - inoculumExcessPortions, 0);
        }
        
        int actualTransitions = compostOutputPortions + sourOutputPortions;
        if (actualTransitions < 1)
            return false;
        
        float minBrowns = Math.Max(actualTransitions - nutritionPortions, 0f);
        float maxBrowns = Math.Min(actualTransitions, brownsPortions);

        float brownsRatio;
        if (maxBrowns > minBrowns)
        {
            float noise = 0.2f * (api.World.Rand.NextSingle() - 0.5f) * (maxBrowns - minBrowns);
            float mean = actualTransitions * (brownsPortions / bulkPortions);
            brownsRatio = Math.Clamp(mean + noise, minBrowns, maxBrowns);
        }
        else
            brownsRatio = minBrowns;
        float nutritionRatio = actualTransitions - brownsRatio;
        
        BrownsQty -= (int)Math.Min(brownsRatio * Settings.Browns.InPerCompostPortion, BrownsQty);
        TryRemoveRandomNutrition(api.World.Rand, (int)(nutritionRatio * Settings.Nutrition.InPerCompostPortion));

        InoculumQty = Math.Clamp
           (InoculumQty
        +   sourOutputPortions * Settings.InoculumOutPerSourPortion
        -   compostOutputPortions * Settings.Inoculum.InPerCompostPortion
           ,0,Settings.Inoculum.MaxQty
            );

        OutputQty = Math.Clamp
            (OutputQty + compostOutputPortions * Settings.OutputOutPerCompostPortion
            ,0,Settings.OutputMaxQty
            );

        PrevTimeComposted = totalHours;
        return true;
    }
    
    
    public void ToTreeAttributes(ITreeAttribute tree, string? key = null)
    {
        tree.SetDouble($"{key}.PrevTimeMoistureUpdated", PrevTimeMoistureUpdated);
        tree.SetFloat($"{key}.Moisture01", Moisture01);

        tree.SetDouble($"{key}.PrevTimeComposted", PrevTimeComposted);

        tree.SetInt($"{key}.BrownsQty", BrownsQty);
        tree.SetInt($"{key}.InoculumQty", InoculumQty);
        tree.SetInt($"{key}.OutputQty", OutputQty);

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
    }

    public void FromTreeAttributes(ITreeAttribute tree, string? key = null)
    {
        PrevTimeMoistureUpdated = tree.GetDouble($"{key}.PrevTimeMoistureUpdated", -1);
        Moisture01 = tree.GetFloat($"{key}.Moisture01");

        PrevTimeComposted = tree.GetDouble($"{key}.PrevTimeComposted", -1);

        BrownsQty = tree.GetInt($"{key}.BrownsQty");
        InoculumQty = tree.GetInt($"{key}.InoculumQty");
        OutputQty = tree.GetInt($"{key}.OutputQty");

        NutritionStacks.Clear();
        int nutritionLength = tree.GetInt($"{key}.NutritionStacks.Count");
        for (int i = 0; i < nutritionLength; i++)
            NutritionStacks[(EnumFoodCategory)tree.GetInt($"{key}.NutritionStacks<{i}>")] = tree.GetInt($"{key}.NutritionStacks[{i}]");
    }
}