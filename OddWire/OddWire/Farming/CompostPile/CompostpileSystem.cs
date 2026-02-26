using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public sealed class CompostpileData
{
    public double PrevTimeComposted = -1;
    public double PrevTimeMoistureUpdated = -1;

    public float Moisture01;

    public int BrownsQty;
    public int InoculumQty;
    public int OutputQty;

    public readonly Dictionary<EnumFoodCategory, int> NutritionStacks = new();

    public int NutritionQty
    {
        get
        {
            int sum = 0;
            foreach (var kvp in NutritionStacks) sum += kvp.Value;
            return sum;
        }
    }
}

public static class CompostpileSystem
{
    public static void ResetQuantitiesOnPlaced(Block block, CompostpileData d, CompostpileTuning m)
    {
        int.TryParse(block.LastCodePart().Substring(1), out int stackBonus);
        stackBonus--;
        if (stackBonus < 1) stackBonus = 0;

        d.BrownsQty = m.Browns.InitQty + stackBonus * m.Browns.PlacedBonusQty;

        d.NutritionStacks.Clear();
        d.NutritionStacks[EnumFoodCategory.Unknown] = m.Nutrition.InitQty + stackBonus * m.Nutrition.PlacedBonusQty;

        d.InoculumQty = m.Inoculum.InitQty + stackBonus * m.Inoculum.PlacedBonusQty;
        d.OutputQty = 0;

        if (d.Moisture01 <= 0f && d.PrevTimeMoistureUpdated < 0)
            d.Moisture01 = m.Process.DefaultMoisture01;
    }

    public static bool CanHarvest(CompostpileData d, CompostpileTuning m, out int compostPileQty, out int sourCompostQty, out int compostQty)
    {
        int bulkPortions = Math.Min(d.BrownsQty / m.Browns.InitQty, d.NutritionQty / m.Nutrition.InitQty);
        compostPileQty = Math.Min(bulkPortions, d.InoculumQty / m.Inoculum.InitQty);
        sourCompostQty = Math.Max(d.InoculumQty - bulkPortions * m.Inoculum.InitQty, 0);
        compostQty = d.OutputQty;

        return compostPileQty > 0 || sourCompostQty > 0 || compostQty > 0;
    }

    public static bool TryAdd(ICoreAPI api, Block block, BlockPos pos, CompostpileData d, CompostpileTuning m, ItemSlot slot, out int accepted)
    {
        accepted = 0;
        if (slot?.StackSize < 1) return false;

        if (TryAddNutrition(m, d, slot, out accepted)
            || TryAddBrowns(m, d, slot, out accepted)
            || TryAddInoculum(m, d, slot, out accepted))
        {
            return accepted > 0;
        }

        return false;
    }

    private static bool TryAddNutrition(CompostpileTuning m, CompostpileData d, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        var collectible = slot.Itemstack?.Collectible;
        var nutritionProps = collectible?.NutritionProps;
        if (nutritionProps is null) return false;

        int room = m.Nutrition.MaxQty - d.NutritionQty;
        if (room < 1) return false;

        int ratio = 1;
        if (collectible != null && collectible.MaxStackSize != 64)
            ratio = Math.Max(64 / collectible.MaxStackSize, 1);

        if (slot.StackSize < ratio) return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, m.Nutrition.MaxInput);

        d.NutritionStacks.TryGetValue(nutritionProps.FoodCategory, out var cur);
        d.NutritionStacks[nutritionProps.FoodCategory] = cur + adjustedAccept;

        accepted = adjustedAccept * ratio;
        return true;
    }

    private static bool TryAddBrowns(CompostpileTuning m, CompostpileData d, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        int room = m.Browns.MaxQty - d.BrownsQty;
        if (room < 1) return false;

        if (slot.Itemstack?.Item?.Code.ToString() != "game:drygrass")
            return false;

        accepted = Math.Min(slot.StackSize > room ? room : slot.StackSize, m.Browns.MaxInput);
        d.BrownsQty += accepted;

        return accepted > 0;
    }

    private static bool TryAddInoculum(CompostpileTuning m, CompostpileData d, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        int room = m.Inoculum.MaxQty - d.InoculumQty;
        if (room < 1) return false;

        string code = slot.Itemstack?.Item?.Code.ToString() ?? "";
        int ratio = code switch
        {
            "game:compost" => 1,
            "oddwire:sourcompost" => m.Inoculum.InPerSourAdded,
            "game:rot" => m.Inoculum.InPerRotAdded,
            _ => 0
        };

        if (ratio < 1 || slot.StackSize < ratio) return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, m.Inoculum.MaxInput);

        d.InoculumQty += adjustedAccept;
        accepted = adjustedAccept * ratio;

        return accepted > 0;
    }

    public static void UpdateMoisture(ICoreAPI api, BlockPos pos, CompostpileData d, CompostpileTuning m, double totalHours)
    {
        if (d.PrevTimeMoistureUpdated < 0)
            d.PrevTimeMoistureUpdated = totalHours;

        float dtDays = (float)Math.Min((totalHours - d.PrevTimeMoistureUpdated) / 24.0, 14.0);

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        if (skyExposed)
        {
            var conds = api.World.GetClimateAtHours(pos, totalHours);
            float wetGain = Math.Clamp(conds?.Rainfall ?? 0f, 0f, 1f) * dtDays * m.Process.RainToMoisturePerDay;
            d.Moisture01 = Math.Clamp(d.Moisture01 + wetGain, 0f, 1f);
        }

        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, m.Process.GreenhouseTempBonusC, out bool inGreenhouse);

        float tempDryMultiplier = Math.Clamp(0.5f + envTemp / 40f, 0.2f, 2.0f);
        float shelterMultiplier = (skyExposed ? 1.0f : 0.75f) * (inGreenhouse ? 0.85f : 1.0f);

        float dryLoss = dtDays * m.Process.DryoutPerDayAt20C * tempDryMultiplier * shelterMultiplier;
        d.Moisture01 = Math.Clamp(d.Moisture01 - dryLoss, 0f, 1f);

        d.PrevTimeMoistureUpdated = totalHours;
    }

    public static float GetInoculumFactor01(CompostpileTuning m, int inoculumQty)
        => Math.Clamp((float)inoculumQty / m.Inoculum.MaxQty, 0.1f, 1f);

    public static float GetTemperatureFactor01(float tempC)
    {
        if (tempC < 0) return 0.05f;
        if (tempC < 10) return GameMath.Lerp(0.05f, 0.6f, (tempC - 0f) / 10f);
        if (tempC < 20) return GameMath.Lerp(0.6f, 1.0f, (tempC - 10f) / 10f);
        if (tempC < 55) return 1.0f;
        if (tempC < 70) return GameMath.Lerp(1.0f, 0.35f, (tempC - 55f) / 15f);
        return 0.10f;
    }

    public static float GetMoistureFactor01(CompostpileTuning m, float moisture01)
    {
        if (moisture01 <= 0.05f)
            return 0.05f;

        float factor = moisture01 <= m.Process.OptimalMoisture01
            ? GameMath.Lerp(0.1f, 1.0f, (moisture01 - 0.05f) / (m.Process.OptimalMoisture01 - 0.05f))
            : GameMath.Lerp(1.0f, 0.25f, (moisture01 - m.Process.OptimalMoisture01) / (1f - m.Process.OptimalMoisture01));

        if (moisture01 > 0.9f)
            factor *= 0.6f;

        return Math.Clamp(factor, 0.05f, 1.0f);
    }

    public static float GetNutritionFactor01(Block block, CompostpileData d, CompostpileTuning m)
    {
        if (d.NutritionStacks.Count < 1)
            return 0f;

        JsonObject speedByCat = block.Attributes?["nutritionSpeedByCategory"];

        float weighted = 0f;
        foreach (var kvp in d.NutritionStacks)
        {
            float mult = speedByCat?[kvp.Key.ToString()]?.AsFloat(1f) ?? 1f;
            weighted += mult * kvp.Value;
        }

        return weighted / m.Nutrition.MaxQty;
    }

    public static float GetCompostRatePerHour(ICoreAPI api, Block block, BlockPos pos, CompostpileData d, CompostpileTuning m, double totalHours)
    {
        if (d.InoculumQty < 1 && d.OutputQty < 1)
            return 0f;

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, m.Process.GreenhouseTempBonusC, out _);

        return
            m.Process.BaseCompostRatePerHour
            * GetInoculumFactor01(m, d.InoculumQty + d.OutputQty)
            * GetTemperatureFactor01(envTemp)
            * GetMoistureFactor01(m, d.Moisture01)
            * GetNutritionFactor01(block, d, m);
    }

    public static float GetSpoilRate01(ICoreAPI api, Block block, BlockPos pos, CompostpileData d, CompostpileTuning m, double totalHours)
        => Math.Clamp(GetSpoilRate(api, block, pos, d, m, totalHours), 0f, 1f);

    public static float GetSpoilRate(ICoreAPI api, Block block, BlockPos pos, CompostpileData d, CompostpileTuning m, double totalHours)
    {
        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, m.Process.GreenhouseTempBonusC, out _);

        JsonObject spoilTemps = block.Attributes?["spoilTempByCategory"];
        if (spoilTemps is null || d.NutritionStacks.Count == 0)
            return 0f;

        float tempRisk01 = 0f;
        foreach (var kvp in d.NutritionStacks)
        {
            string keyA = kvp.Key.ToString();
            float thresh = spoilTemps[keyA]?.AsFloat(float.NaN) ?? float.NaN;
            if (float.IsNaN(thresh))
                continue;

            if (envTemp > thresh)
            {
                float risk = Math.Clamp((envTemp - thresh) / 15f, 0f, 1f);
                if (risk > tempRisk01)
                    tempRisk01 = risk;
            }
        }

        float moistRisk01 = 0f;
        if (d.Moisture01 < 0.05f)
            moistRisk01 = Math.Max(moistRisk01, 0.6f * Math.Clamp((0.05f - d.Moisture01) / 0.05f, 0f, 1f));
        else if (d.Moisture01 > 0.85f)
            moistRisk01 = Math.Clamp((d.Moisture01 - 0.85f) / 0.15f, 0f, 1f);

        return 1f - (1f - tempRisk01) * (1f - moistRisk01);
    }

    public static bool ProcessCompost(ICoreAPI api, Block block, BlockPos pos, CompostpileData d, CompostpileTuning m, double totalHours)
    {
        if (d.PrevTimeComposted < 0
            || (d.InoculumQty >= m.Inoculum.MaxQty && d.OutputQty >= m.Output.OutputMaxQty))
        {
            d.PrevTimeComposted = totalHours;
            return false;
        }

        float brownsPortions = (float)d.BrownsQty / m.Browns.InPerCompostPortion;
        float nutritionPortions = (float)d.NutritionQty / m.Nutrition.InPerCompostPortion;
        float bulkPortions = brownsPortions + nutritionPortions;

        if (bulkPortions < 1f)
        {
            d.PrevTimeComposted = totalHours;
            return false;
        }

        int transitions = (int)Math.Min((totalHours - d.PrevTimeComposted) * GetCompostRatePerHour(api, block, pos, d, m, totalHours), bulkPortions);
        if (transitions < 1)
            return false; // keep “accrue progress” behaviour

        int sourOutputPortions = (int)(transitions * GetSpoilRate01(api, block, pos, d, m, totalHours));
        int compostOutputPortions = transitions - sourOutputPortions;

        // Clamp sour to room, overflow into compost
        int sourOutputRoomPortions = (m.Inoculum.MaxQty - d.InoculumQty) / m.Output.InoculumOutPerSourPortion;
        if (sourOutputPortions > sourOutputRoomPortions)
        {
            int sourOverflowPortions = sourOutputPortions - sourOutputRoomPortions;
            sourOutputPortions = sourOutputRoomPortions;
            compostOutputPortions += sourOverflowPortions;
        }

        // Clamp compost to room, overflow into sour
        int compostOutputRoomPortions = (m.Output.OutputMaxQty - d.OutputQty) / m.Output.OutputOutPerCompostPortion;
        if (compostOutputPortions > compostOutputRoomPortions)
        {
            int compostOverflowPortions = compostOutputPortions - compostOutputRoomPortions;
            compostOutputPortions = compostOutputRoomPortions;
            sourOutputPortions += compostOverflowPortions;
            compostOutputRoomPortions = 0;
        }

        // Bootstrap compost with sour transitions
        int inoculumAfterSourQty = d.InoculumQty + sourOutputPortions * m.Output.InoculumOutPerSourPortion;
        int compostPossibleByInoculumPortions = inoculumAfterSourQty / m.Inoculum.InPerCompostPortion;
        if (compostOutputPortions > compostPossibleByInoculumPortions)
        {
            int overflowByInoculumLimitsPortions = compostOutputPortions - compostPossibleByInoculumPortions;

            int compostSubsidizedBySourPortions = Math.Min(
                overflowByInoculumLimitsPortions * m.Output.InoculumOutPerSourPortion
                / (m.Output.InoculumOutPerSourPortion + m.Inoculum.InPerCompostPortion),
                compostOutputRoomPortions
            );

            compostOutputPortions = compostPossibleByInoculumPortions + compostSubsidizedBySourPortions;
            sourOutputPortions += overflowByInoculumLimitsPortions - compostSubsidizedBySourPortions;
        }

        int actualTransitions = compostOutputPortions + sourOutputPortions;
        if (actualTransitions < 1)
            return false; // keep “accrue progress” behaviour

        // Ratio split (same approach; still mildly noisy)
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
        {
            brownsRatio = minBrowns;
        }

        float nutritionRatio = actualTransitions - brownsRatio;

        d.BrownsQty -= (int)Math.Min(brownsRatio * m.Browns.InPerCompostPortion, d.BrownsQty);
        RemoveRandomNutrition(api.World.Rand, d, (int)(nutritionRatio * m.Nutrition.InPerCompostPortion));

        d.InoculumQty = Math.Clamp(
            d.InoculumQty
            + sourOutputPortions * m.Output.InoculumOutPerSourPortion
            - compostOutputPortions * m.Inoculum.InPerCompostPortion,
            0,
            m.Inoculum.MaxQty
        );

        d.OutputQty = Math.Clamp(
            d.OutputQty + compostOutputPortions * m.Output.OutputOutPerCompostPortion,
            0,
            m.Output.OutputMaxQty
        );

        d.PrevTimeComposted = totalHours;
        return true;
    }

    private static void RemoveRandomNutrition(Random rand, CompostpileData d, int amount)
    {
        if (amount <= 0 || d.NutritionStacks.Count == 0)
            return;

        var keys = new List<EnumFoodCategory>(d.NutritionStacks.Keys);
        int nutritionRemaining = d.NutritionQty;

        int remaining = amount;
        while (remaining > 0 && keys.Count > 0 && nutritionRemaining > 0)
        {
            int index = rand.Next(keys.Count);
            var key = keys[index];

            int stackQty = d.NutritionStacks[key];
            if (stackQty <= 0)
            {
                d.NutritionStacks.Remove(key);
                keys.RemoveAt(index);
                continue;
            }

            int removeWeight = (int)Math.Ceiling(rand.NextSingle() * stackQty / nutritionRemaining);
            int maxRemove = Math.Min(removeWeight, remaining);

            // Fix: avoid the old “removeQty can be 0 forever” infinite-loop risk.
            if (maxRemove < 1)
                maxRemove = 1;

            int removeQty = rand.Next(maxRemove) + 1; // 1..maxRemove
            removeQty = Math.Min(removeQty, stackQty);

            d.NutritionStacks[key] -= removeQty;
            if (d.NutritionStacks[key] < 1)
            {
                d.NutritionStacks.Remove(key);
                keys.RemoveAt(index);
            }

            nutritionRemaining -= removeQty;
            remaining -= removeQty;
        }
    }
}