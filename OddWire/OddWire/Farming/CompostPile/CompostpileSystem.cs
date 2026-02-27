using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public sealed class CompostpileState
{
    public double PrevTimeMoistureUpdated = -1;
    public float Moisture01;

    public double PrevTimeComposted = -1;
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
    public int OutputQty;
}

public static class CompostpileSystem
{
    public static void ResetQuantitiesOnPlaced(Block block, CompostpileState state, CompostpileInventory inventory)
    {
        int.TryParse(block.LastCodePart().Substring(1), out int stackBonus);
        stackBonus--;
        if (stackBonus < 1)
            stackBonus = 0;

        state.BrownsQty = inventory.Browns.InitQty + stackBonus * inventory.Browns.PlacedBonusQty;

        state.NutritionStacks.Clear();
        state.NutritionStacks[EnumFoodCategory.Unknown] = inventory.Nutrition.InitQty + stackBonus * inventory.Nutrition.PlacedBonusQty;

        state.InoculumQty = inventory.Inoculum.InitQty + stackBonus * inventory.Inoculum.PlacedBonusQty;
        state.OutputQty = 0;

        if (state.Moisture01 <= 0f && state.PrevTimeMoistureUpdated < 0)
            state.Moisture01 = inventory.Process.DefaultMoisture01;
    }

    public static bool CanHarvest(CompostpileState state, CompostpileInventory inventory, out int compostPileQty, out int sourCompostQty, out int compostQty)
    {
        int bulkPortions = Math.Min(state.BrownsQty / inventory.Browns.InitQty, state.NutritionQty / inventory.Nutrition.InitQty);
        compostPileQty = Math.Min(bulkPortions, state.InoculumQty / inventory.Inoculum.InitQty);
        sourCompostQty = Math.Max(state.InoculumQty - bulkPortions * inventory.Inoculum.InitQty, 0);
        compostQty = state.OutputQty;

        return compostPileQty > 0 || sourCompostQty > 0 || compostQty > 0;
    }

    public static bool TryAdd(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, CompostpileInventory inventory, ItemSlot slot, out int accepted)
    {
        accepted = 0;
        if (slot?.StackSize < 1)
            return false;

        if (TryAddNutrition(inventory, state, slot, out accepted)
        ||  TryAddBrowns(inventory, state, slot, out accepted)
        ||  TryAddInoculum(inventory, state, slot, out accepted)
            )
            return accepted > 0;

        return false;
    }

    private static bool TryAddNutrition(CompostpileInventory inventory, CompostpileState state, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        var collectible = slot.Itemstack?.Collectible;
        var nutritionProps = collectible?.NutritionProps;
        if (nutritionProps is null)
            return false;

        int room = inventory.Nutrition.MaxQty - state.NutritionQty;
        if (room < 1)
            return false;

        int ratio = 1;
        if (collectible != null && collectible.MaxStackSize != 64)
            ratio = Math.Max(64 / collectible.MaxStackSize, 1);

        if (slot.StackSize < ratio)
            return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, inventory.Nutrition.MaxInput);

        state.NutritionStacks.TryGetValue(nutritionProps.FoodCategory, out var cur);
        state.NutritionStacks[nutritionProps.FoodCategory] = cur + adjustedAccept;

        accepted = adjustedAccept * ratio;
        return true;
    }

    private static bool TryAddBrowns(CompostpileInventory inventory, CompostpileState state, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        int room = inventory.Browns.MaxQty - state.BrownsQty;
        if (room < 1) return false;

        if (slot.Itemstack?.Item?.Code.ToString() != "game:drygrass")
            return false;

        accepted = Math.Min(slot.StackSize > room ? room : slot.StackSize, inventory.Browns.MaxInput);
        state.BrownsQty += accepted;

        return accepted > 0;
    }

    private static bool TryAddInoculum(CompostpileInventory inventory, CompostpileState state, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        int room = inventory.Inoculum.MaxQty - state.InoculumQty;
        if (room < 1)
            return false;

        string code = slot.Itemstack?.Item?.Code.ToString() ?? "";
        int ratio = code switch
            {"game:compost" => 1
            ,"oddwire:sourcompost" => inventory.Inoculum.InPerSourAdded
            ,"game:rot" => inventory.Inoculum.InPerRotAdded
            ,_ => 0
            };

        if (ratio < 1 || slot.StackSize < ratio)
            return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, inventory.Inoculum.MaxInput);

        state.InoculumQty += adjustedAccept;
        accepted = adjustedAccept * ratio;

        return accepted > 0;
    }

    public static void UpdateMoisture(ICoreAPI api, BlockPos pos, CompostpileState state, CompostpileInventory inventory, double totalHours)
    {
        if (state.PrevTimeMoistureUpdated < 0)
            state.PrevTimeMoistureUpdated = totalHours;

        float dtDays = (float)Math.Min((totalHours - state.PrevTimeMoistureUpdated) / 24.0, 14.0);

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        if (skyExposed)
        {
            var conds = api.World.GetClimateAtHours(pos, totalHours);
            float wetGain = Math.Clamp(conds?.Rainfall ?? 0f, 0f, 1f) * dtDays * inventory.Process.RainToMoisturePerDay;
            state.Moisture01 = Math.Clamp(state.Moisture01 + wetGain, 0f, 1f);
        }

        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, inventory.Process.GreenhouseTempBonusC, out bool inGreenhouse);

        float tempDryMultiplier = Math.Clamp(0.5f + envTemp / 40f, 0.2f, 2.0f);
        float shelterMultiplier = (skyExposed ? 1.0f : 0.75f) * (inGreenhouse ? 0.85f : 1.0f);

        float dryLoss = dtDays * inventory.Process.DryoutPerDayAt20C * tempDryMultiplier * shelterMultiplier;
        state.Moisture01 = Math.Clamp(state.Moisture01 - dryLoss, 0f, 1f);

        state.PrevTimeMoistureUpdated = totalHours;
    }

    public static float GetInoculumFactor01(CompostpileInventory inventory, int inoculumQty)
        => Math.Clamp((float)inoculumQty / inventory.Inoculum.MaxQty, 0.1f, 1f);

    public static float GetTemperatureFactor01(float tempC)
    {
        if (tempC <  0) return 0.05f;
        if (tempC < 10) return GameMath.Lerp(0.05f, 0.6f, (tempC - 0f) / 10f);
        if (tempC < 20) return GameMath.Lerp(0.6f, 1.0f, (tempC - 10f) / 10f);
        if (tempC < 55) return 1.0f;
        if (tempC < 70) return GameMath.Lerp(1.0f, 0.35f, (tempC - 55f) / 15f);
        return 0.10f;
    }

    public static float GetMoistureFactor01(CompostpileInventory inventory, float moisture01)
    {
        if (moisture01 <= 0.05f)
            return 0.05f;

        float factor = moisture01 <= inventory.Process.OptimalMoisture01
            ? GameMath.Lerp(0.1f, 1.0f, (moisture01 - 0.05f) / (inventory.Process.OptimalMoisture01 - 0.05f))
            : GameMath.Lerp(1.0f, 0.25f, (moisture01 - inventory.Process.OptimalMoisture01) / (1f - inventory.Process.OptimalMoisture01));

        if (moisture01 > 0.9f)
            factor *= 0.6f;

        return Math.Clamp(factor, 0.05f, 1.0f);
    }

    public static float GetNutritionFactor01(Block block, CompostpileState state, CompostpileInventory inventory)
    {
        if (state.NutritionStacks.Count < 1)
            return 0f;

        JsonObject? speedByCat = block.Attributes?["nutritionSpeedByCategory"];

        float weighted = 0f;
        foreach (var kvp in state.NutritionStacks)
        {
            float mult = speedByCat?[kvp.Key.ToString()]?.AsFloat(1f) ?? 1f;
            weighted += mult * kvp.Value;
        }

        return weighted / inventory.Nutrition.MaxQty;
    }

    public static float GetCompostRatePerHour(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, CompostpileInventory inventory, double totalHours)
    {
        if (state.InoculumQty < 1 && state.OutputQty < 1)
            return 0f;

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, inventory.Process.GreenhouseTempBonusC, out _);

        return
            inventory.Process.BaseCompostRatePerHour
            * GetInoculumFactor01(inventory, state.InoculumQty + state.OutputQty)
            * GetTemperatureFactor01(envTemp)
            * GetMoistureFactor01(inventory, state.Moisture01)
            * GetNutritionFactor01(block, state, inventory);
    }

    public static float GetSpoilRate01(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, CompostpileInventory inventory, double totalHours)
        => Math.Clamp(GetSpoilRate(api, block, pos, state, inventory, totalHours), 0f, 1f);

    public static float GetSpoilRate(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, CompostpileInventory inventory, double totalHours)
    {
        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, inventory.Process.GreenhouseTempBonusC, out _);

        JsonObject? spoilTemps = block.Attributes?["spoilTempByCategory"];
        if (spoilTemps is null || state.NutritionStacks.Count == 0)
            return 0f;

        float tempRisk01 = 0f;
        foreach (var kvp in state.NutritionStacks)
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
        if (state.Moisture01 < 0.05f)
            moistRisk01 = Math.Max(moistRisk01, 0.6f * Math.Clamp((0.05f - state.Moisture01) / 0.05f, 0f, 1f));
        else if (state.Moisture01 > 0.85f)
            moistRisk01 = Math.Clamp((state.Moisture01 - 0.85f) / 0.15f, 0f, 1f);

        return 1f - (1f - tempRisk01) * (1f - moistRisk01);
    }

    public static bool ProcessCompost(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, CompostpileInventory inventory, double totalHours)
    {
        if (state.PrevTimeComposted < 0
            || (state.InoculumQty >= inventory.Inoculum.MaxQty && state.OutputQty >= inventory.Output.OutputMaxQty))
        {
            state.PrevTimeComposted = totalHours;
            return false;
        }

        float brownsPortions = (float)state.BrownsQty / inventory.Browns.InPerCompostPortion;
        float nutritionPortions = (float)state.NutritionQty / inventory.Nutrition.InPerCompostPortion;
        float bulkPortions = brownsPortions + nutritionPortions;

        if (bulkPortions < 1f)
        {
            state.PrevTimeComposted = totalHours;
            return false;
        }

        int transitions = (int)Math.Min((totalHours - state.PrevTimeComposted) * GetCompostRatePerHour(api, block, pos, state, inventory, totalHours), bulkPortions);
        if (transitions < 1)
            return false; // keep “accrue progress” behaviour

        int sourOutputPortions = (int)(transitions * GetSpoilRate01(api, block, pos, state, inventory, totalHours));
        int compostOutputPortions = transitions - sourOutputPortions;

        // Clamp sour to room, overflow into compost
        int sourOutputRoomPortions = (inventory.Inoculum.MaxQty - state.InoculumQty) / inventory.Output.InoculumOutPerSourPortion;
        if (sourOutputPortions > sourOutputRoomPortions)
        {
            int sourOverflowPortions = sourOutputPortions - sourOutputRoomPortions;
            sourOutputPortions = sourOutputRoomPortions;
            compostOutputPortions += sourOverflowPortions;
        }

        // Clamp compost to room, overflow into sour
        int compostOutputRoomPortions = (inventory.Output.OutputMaxQty - state.OutputQty) / inventory.Output.OutputOutPerCompostPortion;
        if (compostOutputPortions > compostOutputRoomPortions)
        {
            int compostOverflowPortions = compostOutputPortions - compostOutputRoomPortions;
            compostOutputPortions = compostOutputRoomPortions;
            sourOutputPortions += compostOverflowPortions;
            compostOutputRoomPortions = 0;
        }

        // Bootstrap compost with sour transitions
        int inoculumAfterSourQty = state.InoculumQty + sourOutputPortions * inventory.Output.InoculumOutPerSourPortion;
        int compostPossibleByInoculumPortions = inoculumAfterSourQty / inventory.Inoculum.InPerCompostPortion;
        if (compostOutputPortions > compostPossibleByInoculumPortions)
        {
            int overflowByInoculumLimitsPortions = compostOutputPortions - compostPossibleByInoculumPortions;

            int compostSubsidizedBySourPortions = Math.Min(
                overflowByInoculumLimitsPortions * inventory.Output.InoculumOutPerSourPortion
                / (inventory.Output.InoculumOutPerSourPortion + inventory.Inoculum.InPerCompostPortion),
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

        state.BrownsQty -= (int)Math.Min(brownsRatio * inventory.Browns.InPerCompostPortion, state.BrownsQty);
        RemoveRandomNutrition(api.World.Rand, state, (int)(nutritionRatio * inventory.Nutrition.InPerCompostPortion));

        state.InoculumQty = Math.Clamp(
            state.InoculumQty
            + sourOutputPortions * inventory.Output.InoculumOutPerSourPortion
            - compostOutputPortions * inventory.Inoculum.InPerCompostPortion,
            0,
            inventory.Inoculum.MaxQty
        );

        state.OutputQty = Math.Clamp(
            state.OutputQty + compostOutputPortions * inventory.Output.OutputOutPerCompostPortion,
            0,
            inventory.Output.OutputMaxQty
        );

        state.PrevTimeComposted = totalHours;
        return true;
    }

    private static void RemoveRandomNutrition(Random rand, CompostpileState state, int amount)
    {
        if (amount <= 0 || state.NutritionStacks.Count == 0)
            return;

        var keys = new List<EnumFoodCategory>(state.NutritionStacks.Keys);
        int nutritionRemaining = state.NutritionQty;

        int remaining = amount;
        while (remaining > 0 && keys.Count > 0 && nutritionRemaining > 0)
        {
            int index = rand.Next(keys.Count);
            var key = keys[index];

            int stackQty = state.NutritionStacks[key];
            if (stackQty <= 0)
            {
                state.NutritionStacks.Remove(key);
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

            state.NutritionStacks[key] -= removeQty;
            if (state.NutritionStacks[key] < 1)
            {
                state.NutritionStacks.Remove(key);
                keys.RemoveAt(index);
            }

            nutritionRemaining -= removeQty;
            remaining -= removeQty;
        }
    }
}