using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

/// <summary>
/// Tuning + behaviour bundled together.
/// This replaces the old static CompostpileSystem, and makes the "system" an instance API off the inventory.
/// </summary>
public sealed class CompostpileInventory
{
    public CompostpileIngredient Browns { get; }
    public CompostpileIngredient Nutrition { get; }
    public CompostpileIngredient Inoculum { get; }

    public CompostpileProcess Process { get; }
    public CompostpileOutput Output { get; }
    public CompostpileHarvest Harvest { get; }

    public CompostpileInventory(
        CompostpileIngredient browns,
        CompostpileIngredient nutrition,
        CompostpileIngredient inoculum,
        CompostpileProcess process,
        CompostpileOutput output,
        CompostpileHarvest harvest
    )
    {
        Browns = browns;
        Nutrition = nutrition;
        Inoculum = inoculum;

        Process = process;
        Output = output;
        Harvest = harvest;
    }

    public static readonly CompostpileInventory Default = new(
        browns: new CompostpileIngredient(
            name: "browns",
            initQty: 16,
            placedBonusQty: 44,
            maxQty: 64 * 3,
            maxInput: 16,
            inPerCompostPortion: 16,
            // browns-specific
            requiredItemCode: "game:drygrass"
        ),
        nutrition: new CompostpileIngredient(
            name: "nutrition",
            initQty: 16,
            placedBonusQty: 12,
            maxQty: 64,
            maxInput: 8,
            inPerCompostPortion: 8
        ),
        inoculum: new CompostpileIngredient(
            name: "inoculum",
            initQty: 2,
            placedBonusQty: 8,
            maxQty: 16,
            maxInput: 4,
            inPerCompostPortion: 1,
            // inoculum-specific “add ratios” (source items per +1 inoculum)
            inPerSourAdded: 2,
            inPerRotAdded: 4
        ),
        process: new CompostpileProcess(
            baseCompostRatePerHour: 0.33f,
            defaultMoisture01: 0.55f,
            optimalMoisture01: 0.60f,
            rainToMoisturePerDay: 0.40f,
            dryoutPerDayAt20C: 0.25f,
            greenhouseTempBonusC: 5f
        ),
        output: new CompostpileOutput(
            outputMaxQty: 48,
            outputOutPerCompostPortion: 1,
            inoculumOutPerSourPortion: 1
        ),
        harvest: new CompostpileHarvest(
            harvestMaxPerStack: 8
        )
    );

    // -----------------------------
    //  System (formerly CompostpileSystem)
    // -----------------------------

    public void ResetQuantitiesOnPlaced(Block block, CompostpileState state)
    {
        int.TryParse(block.LastCodePart().Substring(1), out int stackBonus);
        stackBonus--;
        if (stackBonus < 1) stackBonus = 0;

        state.BrownsQty = Browns.InitQty + stackBonus * Browns.PlacedBonusQty;

        state.NutritionStacks.Clear();
        state.NutritionStacks[EnumFoodCategory.Unknown] = Nutrition.InitQty + stackBonus * Nutrition.PlacedBonusQty;

        state.InoculumQty = Inoculum.InitQty + stackBonus * Inoculum.PlacedBonusQty;
        state.OutputQty = 0;

        if (state.Moisture01 <= 0f && state.PrevTimeMoistureUpdated < 0)
            state.Moisture01 = Process.DefaultMoisture01;
    }

    public bool CanHarvest(CompostpileState state, out int compostPileQty, out int sourCompostQty, out int compostQty)
    {
        int bulkPortions = Math.Min(state.BrownsQty / Browns.InitQty, state.NutritionQty / Nutrition.InitQty);
        compostPileQty = Math.Min(bulkPortions, state.InoculumQty / Inoculum.InitQty);
        sourCompostQty = Math.Max(state.InoculumQty - bulkPortions * Inoculum.InitQty, 0);
        compostQty = state.OutputQty;

        return compostPileQty > 0 || sourCompostQty > 0 || compostQty > 0;
    }

    public bool TryAdd(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, ItemSlot slot, out int accepted)
    {
        accepted = 0;
        if (slot?.StackSize < 1)
            return false;

        // Nutrition (category stacks)
        if (TryAddNutrition(state, slot, out accepted))
            return accepted > 0;

        // Browns (simple required item)
        if (Browns.TryAddSimpleRequired(slot, ref state.BrownsQty, out accepted))
            return accepted > 0;

        // Inoculum (ratio-based)
        if (TryAddInoculum(state, slot, out accepted))
            return accepted > 0;

        return false;
    }

    private bool TryAddNutrition(CompostpileState state, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        var collectible = slot.Itemstack?.Collectible;
        var nutritionProps = collectible?.NutritionProps;
        if (nutritionProps is null)
            return false;

        int room = Nutrition.MaxQty - state.NutritionQty;
        if (room < 1)
            return false;

        // For reduced max stacks, normalize to "64-stack equivalents" so small-stack items aren't overpowered.
        int ratio = CompostpileIngredient.GetStackNormalizationRatio(collectible);
        if (slot.StackSize < ratio)
            return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, Nutrition.MaxInput);

        state.NutritionStacks.TryGetValue(nutritionProps.FoodCategory, out var cur);
        state.NutritionStacks[nutritionProps.FoodCategory] = cur + adjustedAccept;

        accepted = adjustedAccept * ratio;
        return true;
    }

    private bool TryAddInoculum(CompostpileState state, ItemSlot slot, out int accepted)
    {
        accepted = 0;

        int room = Inoculum.MaxQty - state.InoculumQty;
        if (room < 1)
            return false;

        string code = slot.Itemstack?.Item?.Code.ToString() ?? "";
        int ratio = Inoculum.GetInoculumAddRatio(code);

        if (ratio < 1 || slot.StackSize < ratio)
            return false;

        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, Inoculum.MaxInput);

        state.InoculumQty += adjustedAccept;
        accepted = adjustedAccept * ratio;

        return accepted > 0;
    }

    public void UpdateMoisture(ICoreAPI api, BlockPos pos, CompostpileState state, double totalHours)
    {
        if (state.PrevTimeMoistureUpdated < 0)
            state.PrevTimeMoistureUpdated = totalHours;

        float dtDays = (float)Math.Min((totalHours - state.PrevTimeMoistureUpdated) / 24.0, 14.0);

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        if (skyExposed)
        {
            var conds = api.World.GetClimateAtHours(pos, totalHours);
            float wetGain = Math.Clamp(conds?.Rainfall ?? 0f, 0f, 1f) * dtDays * Process.RainToMoisturePerDay;
            state.Moisture01 = Math.Clamp(state.Moisture01 + wetGain, 0f, 1f);
        }

        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, Process.GreenhouseTempBonusC, out bool inGreenhouse);

        float tempDryMultiplier = Math.Clamp(0.5f + envTemp / 40f, 0.2f, 2.0f);
        float shelterMultiplier = (skyExposed ? 1.0f : 0.75f) * (inGreenhouse ? 0.85f : 1.0f);

        float dryLoss = dtDays * Process.DryoutPerDayAt20C * tempDryMultiplier * shelterMultiplier;
        state.Moisture01 = Math.Clamp(state.Moisture01 - dryLoss, 0f, 1f);

        state.PrevTimeMoistureUpdated = totalHours;
    }

    public float GetInoculumFactor01(int inoculumQty)
        => Math.Clamp((float)inoculumQty / Inoculum.MaxQty, 0.1f, 1f);

    public float GetTemperatureFactor01(float tempC)
    {
        if (tempC < 0) return 0.05f;
        if (tempC < 10) return GameMath.Lerp(0.05f, 0.6f, (tempC - 0f) / 10f);
        if (tempC < 20) return GameMath.Lerp(0.6f, 1.0f, (tempC - 10f) / 10f);
        if (tempC < 55) return 1.0f;
        if (tempC < 70) return GameMath.Lerp(1.0f, 0.35f, (tempC - 55f) / 15f);
        return 0.10f;
    }

    public float GetMoistureFactor01(float moisture01)
    {
        if (moisture01 <= 0.05f)
            return 0.05f;

        float factor = moisture01 <= Process.OptimalMoisture01
            ? GameMath.Lerp(0.1f, 1.0f, (moisture01 - 0.05f) / (Process.OptimalMoisture01 - 0.05f))
            : GameMath.Lerp(1.0f, 0.25f, (moisture01 - Process.OptimalMoisture01) / (1f - Process.OptimalMoisture01));

        if (moisture01 > 0.9f)
            factor *= 0.6f;

        return Math.Clamp(factor, 0.05f, 1.0f);
    }

    public float GetNutritionFactor01(Block block, CompostpileState state)
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

        return weighted / Nutrition.MaxQty;
    }

    public float GetCompostRatePerHour(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, double totalHours)
    {
        if (state.InoculumQty < 1 && state.OutputQty < 1)
            return 0f;

        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, Process.GreenhouseTempBonusC, out _);

        return
            Process.BaseCompostRatePerHour
            * GetInoculumFactor01(state.InoculumQty + state.OutputQty)
            * GetTemperatureFactor01(envTemp)
            * GetMoistureFactor01(state.Moisture01)
            * GetNutritionFactor01(block, state);
    }

    public float GetSpoilRate01(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, double totalHours)
        => Math.Clamp(GetSpoilRate(api, block, pos, state, totalHours), 0f, 1f);

    public float GetSpoilRate(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, double totalHours)
    {
        bool skyExposed = api.World.BlockAccessor.IsSkyExposed(pos);
        float envTemp = api.GetEnvironmentTemperatureC(pos, totalHours, skyExposed, Process.GreenhouseTempBonusC, out _);

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

    public bool ProcessCompost(ICoreAPI api, Block block, BlockPos pos, CompostpileState state, double totalHours)
    {
        if (state.PrevTimeComposted < 0
            || (state.InoculumQty >= Inoculum.MaxQty && state.OutputQty >= Output.OutputMaxQty))
        {
            state.PrevTimeComposted = totalHours;
            return false;
        }

        float brownsPortions = (float)state.BrownsQty / Browns.InPerCompostPortion;
        float nutritionPortions = (float)state.NutritionQty / Nutrition.InPerCompostPortion;
        float bulkPortions = brownsPortions + nutritionPortions;

        if (bulkPortions < 1f)
        {
            state.PrevTimeComposted = totalHours;
            return false;
        }

        int transitions = (int)Math.Min(
            (totalHours - state.PrevTimeComposted) * GetCompostRatePerHour(api, block, pos, state, totalHours),
            bulkPortions
        );
        if (transitions < 1)
            return false; // keep “accrue progress” behaviour

        int sourOutputPortions = (int)(transitions * GetSpoilRate01(api, block, pos, state, totalHours));
        int compostOutputPortions = transitions - sourOutputPortions;

        // Clamp sour to room, overflow into compost
        int sourOutputRoomPortions = (Inoculum.MaxQty - state.InoculumQty) / Output.InoculumOutPerSourPortion;
        if (sourOutputPortions > sourOutputRoomPortions)
        {
            int sourOverflowPortions = sourOutputPortions - sourOutputRoomPortions;
            sourOutputPortions = sourOutputRoomPortions;
            compostOutputPortions += sourOverflowPortions;
        }

        // Clamp compost to room, overflow into sour
        int compostOutputRoomPortions = (Output.OutputMaxQty - state.OutputQty) / Output.OutputOutPerCompostPortion;
        if (compostOutputPortions > compostOutputRoomPortions)
        {
            int compostOverflowPortions = compostOutputPortions - compostOutputRoomPortions;
            compostOutputPortions = compostOutputRoomPortions;
            sourOutputPortions += compostOverflowPortions;
            compostOutputRoomPortions = 0;
        }

        // Bootstrap compost with sour transitions
        int inoculumAfterSourQty = state.InoculumQty + sourOutputPortions * Output.InoculumOutPerSourPortion;
        int compostPossibleByInoculumPortions = inoculumAfterSourQty / Inoculum.InPerCompostPortion;
        if (compostOutputPortions > compostPossibleByInoculumPortions)
        {
            int overflowByInoculumLimitsPortions = compostOutputPortions - compostPossibleByInoculumPortions;

            int compostSubsidizedBySourPortions = Math.Min(
                overflowByInoculumLimitsPortions * Output.InoculumOutPerSourPortion
                / (Output.InoculumOutPerSourPortion + Inoculum.InPerCompostPortion),
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

        state.BrownsQty -= (int)Math.Min(brownsRatio * Browns.InPerCompostPortion, state.BrownsQty);
        RemoveRandomNutrition(api.World.Rand, state, (int)(nutritionRatio * Nutrition.InPerCompostPortion));

        state.InoculumQty = Math.Clamp(
            state.InoculumQty
            + sourOutputPortions * Output.InoculumOutPerSourPortion
            - compostOutputPortions * Inoculum.InPerCompostPortion,
            0,
            Inoculum.MaxQty
        );

        state.OutputQty = Math.Clamp(
            state.OutputQty + compostOutputPortions * Output.OutputOutPerCompostPortion,
            0,
            Output.OutputMaxQty
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

            // avoid “removeQty can be 0 forever” infinite-loop risk
            if (maxRemove < 1) maxRemove = 1;

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

public sealed class CompostpileState
{
    public double PrevTimeMoistureUpdated = -1;
    public float Moisture01;

    public double PrevTimeComposted = -1;

    public int BrownsQty;

    public readonly Dictionary<EnumFoodCategory, int> NutritionStacks = new();

    public int NutritionQty
    {
        get
        {
            int sum = 0;
            foreach (var kvp in NutritionStacks)
                sum += kvp.Value;
            return sum;
        }
    }

    public int InoculumQty;
    public int OutputQty;
}

public sealed class CompostpileIngredient
{
    public string Name { get; }
    public int InitQty { get; }
    public int PlacedBonusQty { get; }
    public int MaxQty { get; }
    public int MaxInput { get; }
    public int InPerCompostPortion { get; }

    // Optional specialisations (kept lightweight)
    public string RequiredItemCode { get; }
    public int InPerSourAdded { get; }
    public int InPerRotAdded { get; }

    public CompostpileIngredient(
        string name,
        int initQty,
        int placedBonusQty,
        int maxQty,
        int maxInput,
        int inPerCompostPortion,
        int inPerSourAdded = 1,
        int inPerRotAdded = 1,
        string requiredItemCode = ""
    )
    {
        Name = name;

        InitQty = initQty;
        PlacedBonusQty = placedBonusQty;
        MaxQty = maxQty;
        MaxInput = maxInput;
        InPerCompostPortion = inPerCompostPortion;

        InPerSourAdded = inPerSourAdded < 1 ? 1 : inPerSourAdded;
        InPerRotAdded = inPerRotAdded < 1 ? 1 : inPerRotAdded;

        RequiredItemCode = requiredItemCode ?? "";
    }

    public static int GetStackNormalizationRatio(CollectibleObject? collectible)
    {
        if (collectible == null)
            return 1;

        // Normalise everything to 64-stack equivalence.
        if (collectible.MaxStackSize != 64 && collectible.MaxStackSize > 0)
            return Math.Max(64 / collectible.MaxStackSize, 1);

        return 1;
    }

    public bool TryAddSimpleRequired(ItemSlot slot, ref int currentQty, out int accepted)
    {
        accepted = 0;

        if (string.IsNullOrEmpty(RequiredItemCode))
            return false;

        int room = MaxQty - currentQty;
        if (room < 1)
            return false;

        if ((slot.Itemstack?.Item?.Code.ToString() ?? "") != RequiredItemCode)
            return false;

        accepted = Math.Min(slot.StackSize > room ? room : slot.StackSize, MaxInput);
        currentQty += accepted;

        return accepted > 0;
    }

    /// <summary>
    /// Inoculum-only helper (kept here so the "rules" live with the ingredient).
    /// Returns item-count-per-+1 inoculum.
    /// </summary>
    public int GetInoculumAddRatio(string itemCode)
        => itemCode switch
        {
            "game:compost" => 1,
            "oddwire:sourcompost" => InPerSourAdded,
            "game:rot" => InPerRotAdded,
            _ => 0
        };
}

public sealed class CompostpileProcess
{
    public float BaseCompostRatePerHour { get; }

    public float DefaultMoisture01 { get; }
    public float OptimalMoisture01 { get; }
    public float RainToMoisturePerDay { get; }
    public float DryoutPerDayAt20C { get; }

    public float GreenhouseTempBonusC { get; }

    public CompostpileProcess(
        float baseCompostRatePerHour,
        float defaultMoisture01,
        float optimalMoisture01,
        float rainToMoisturePerDay,
        float dryoutPerDayAt20C,
        float greenhouseTempBonusC
    )
    {
        BaseCompostRatePerHour = baseCompostRatePerHour;

        DefaultMoisture01 = defaultMoisture01;
        OptimalMoisture01 = optimalMoisture01;
        RainToMoisturePerDay = rainToMoisturePerDay;
        DryoutPerDayAt20C = dryoutPerDayAt20C;

        GreenhouseTempBonusC = greenhouseTempBonusC;
    }
}

public sealed class CompostpileOutput
{
    public int OutputMaxQty { get; }
    public int OutputOutPerCompostPortion { get; }

    public int InoculumOutPerSourPortion { get; }

    public CompostpileOutput(
        int outputMaxQty,
        int outputOutPerCompostPortion,
        int inoculumOutPerSourPortion
    )
    {
        OutputMaxQty = outputMaxQty;
        OutputOutPerCompostPortion = outputOutPerCompostPortion;

        InoculumOutPerSourPortion = inoculumOutPerSourPortion;
    }
}

public sealed class CompostpileHarvest
{
    public int HarvestMaxPerStack { get; }

    public CompostpileHarvest(int harvestMaxPerStack)
    {
        HarvestMaxPerStack = harvestMaxPerStack;
    }
}