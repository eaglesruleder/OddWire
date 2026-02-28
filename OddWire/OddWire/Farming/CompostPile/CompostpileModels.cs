using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace OddWire.GameContent;

public sealed class CompostpileProcess
{
    public float BaseCompostRatePerHour { get; }
    public float DefaultMoisture01 { get; }
    public float OptimalMoisture01 { get; }
    public float RainToMoisturePerDay { get; }
    public float DryoutPerDayAt20C { get; }
    public float GreenhouseTempBonusC { get; }

    public CompostpileProcess
        (float baseCompostRatePerHour
        ,float defaultMoisture01
        ,float optimalMoisture01
        ,float rainToMoisturePerDay
        ,float dryoutPerDayAt20C
        ,float greenhouseTempBonusC
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

    public CompostpileOutput
        (int outputMaxQty
        ,int outputOutPerCompostPortion
        ,int inoculumOutPerSourPortion
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
    public CompostpileHarvest(int harvestMaxPerStack) =>
        HarvestMaxPerStack = harvestMaxPerStack;
}

public sealed class CompostpileIngredient
{
    public string Name { get; }
    public int InitQty { get; }
    public int PlacedBonusQty { get; }
    public int MaxQty { get; }
    public int MaxInput { get; }
    
    public int InPerCompostPortion { get; }
    public int InPerSourPortion { get; }

    public Dictionary<string, float> AddItemCodeRatios { get; }

    public CompostpileIngredient
        (string name
        ,int initQty
        ,int placedBonusQty
        ,int maxQty
        ,int maxInput
        ,int inPerCompostPortion
        ,int inPerSourPortion
        ,Dictionary<string, float>? requiredItemCodes = null
        )
    {
        Name = name;
        InitQty = initQty;
        PlacedBonusQty = placedBonusQty;
        MaxQty = maxQty;
        MaxInput = maxInput;
        InPerCompostPortion = inPerCompostPortion;
        InPerSourPortion = inPerSourPortion;
        AddItemCodeRatios = requiredItemCodes ?? new Dictionary<string, float>();
    }

    public static int GetStackNormalizationRatio(CollectibleObject? collectible)
    {
        if (collectible == null)
            return 1;
        
        if (collectible.MaxStackSize != 64
        &&  collectible.MaxStackSize > 0
            )
            return Math.Max(64 / collectible.MaxStackSize, 1);
        
        return 1;
    }

    public bool TryAddRef(ItemSlot slot, out int accepted, ref int currentQty)
    {
        accepted = 0;
        if (AddItemCodeRatios is null
            || AddItemCodeRatios.Count == 0
           )
            return false;

        int room = MaxQty - currentQty;
        if (room < 1)
            return false;

        string code = slot.Itemstack?.Item?.Code.ToString() ?? "";
        if(!AddItemCodeRatios.TryGetValue(code, out float ratio)
        ||  ratio <= 0f
        ||  slot.StackSize < ratio
            )
            return false;

        int adjustedStackSize = (int)(slot.StackSize / ratio);
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, MaxInput);

        currentQty += adjustedAccept;
        accepted = (int)(adjustedAccept * ratio);
        return accepted > 0;
    }
}