using System;
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

    public string RequiredItemCode { get; }
    public int InPerSourAdded { get; }
    public int InPerRotAdded { get; }

    public CompostpileIngredient
        (string name
        ,int initQty
        ,int placedBonusQty
        ,int maxQty
        ,int maxInput
        ,int inPerCompostPortion
        ,int inPerSourAdded = 1
        ,int inPerRotAdded = 1
        ,string requiredItemCode = ""
        )
    {
        Name = name;
        InitQty = initQty;
        PlacedBonusQty = placedBonusQty;
        MaxQty = maxQty;
        MaxInput = maxInput;
        InPerCompostPortion = inPerCompostPortion;

        InPerSourAdded = Math.Max(1, inPerSourAdded);
        InPerRotAdded = Math.Max(1, inPerRotAdded);
        RequiredItemCode = requiredItemCode ?? "";
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

    public bool TryAddSimpleRequired(ItemSlot slot, ref int currentQty, out int accepted)
    {
        accepted = 0;
        if (string.IsNullOrEmpty(RequiredItemCode)) return false;

        int room = MaxQty - currentQty;
        if (room < 1)
            return false;

        if ((slot.Itemstack?.Item?.Code.ToString() ?? "") != RequiredItemCode)
            return false;

        accepted = Math.Min(slot.StackSize > room ? room : slot.StackSize, MaxInput);
        currentQty += accepted;
        return accepted > 0;
    }

    public int GetInoculumAddRatio(string itemCode) => itemCode switch
        {"game:compost" => 1
        ,"oddwire:sourcompost" => InPerSourAdded
        ,"game:rot" => InPerRotAdded
        ,_ => 0
        };
}