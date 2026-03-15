using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace OddWire.GameContent;

public sealed class CompostpileSettings
{
    public CompostpileIngredientSettings Browns { get; private set; }
    public CompostpileIngredientSettings Nutrition { get; private set; }
    public CompostpileIngredientSettings Inoculum { get; private set; }
    
    public float BaseCompostRatePerHour { get; private set; }
    public float DefaultMoisture01 { get; private set; }
    public float OptimalMoisture01 { get; private set; }
    public float RainToMoisturePerDay { get; private set; }
    public float DryoutPerDayAt20C { get; private set; }
    public float GreenhouseTempBonusC { get; private set; }
    
    public float NutritionTolerance { get; private set; }
    public float MoistureTolerance { get; private set; }
    public float AerationDecayPerDay { get; private set; }
    public float PassiveHeating { get; private set; }
    public float PassiveCooling { get; private set; }
    
    public float OverheatTemperature { get; private set; }
    public float OverheatTolerance { get; private set; }
    
    public float DrowningMoisture { get; private set; }
    public float DrowningTolerance { get; private set; }
    
    public float HypoxicTolerance { get; private set; }

    public Dictionary<string, float>? NutritionSpeed { get; private set; }
    public Dictionary<string, float>? NutritionHeat { get; private set; }
    
    public int OutputMaxQty { get; private set; }
    public int OutputOutPerCompostPortion { get; private set; }
    public int InoculumOutPerSourPortion { get; private set; }
    
    public int HarvestMaxPerStack { get; private set; }
    
    public static CompostpileSettings Default = new()
        {Browns = new CompostpileIngredientSettings
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
        ,Nutrition = new CompostpileIngredientSettings
            (name: "nutrition"
                ,initQty: 16
                ,placedBonusQty: 12
                ,maxQty: 64
                ,maxInput: 8
                ,inPerCompostPortion: 8
                ,inPerSourPortion: 4
            )
        ,Inoculum = new CompostpileIngredientSettings
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
        ,BaseCompostRatePerHour = 0.33f
        ,DefaultMoisture01 = 0.55f
        ,OptimalMoisture01 = 0.60f
        ,RainToMoisturePerDay = 0.40f
        ,DryoutPerDayAt20C = 0.25f
        ,GreenhouseTempBonusC = 5f
            
        ,NutritionTolerance = 0.35f
        ,MoistureTolerance = 0.35f
        ,AerationDecayPerDay = 0.96f
        ,PassiveHeating = 45
        ,PassiveCooling = 0.18f
    
        ,OverheatTemperature = 65
        ,OverheatTolerance = 12
    
        ,DrowningMoisture = 0.8f
        ,DrowningTolerance = 0.2f
    
        ,HypoxicTolerance = 0.15f
    
        ,NutritionSpeed = new Dictionary<string, float>
            {{"Fruit", 1.5f}
            ,{"Vegetable", 2.0f}
            ,{"Dairy", 2.2f}
            ,{"Grain", 2.3f}
            ,{"Protein", 2.7f}
            }
        ,NutritionHeat = new Dictionary<string, float>()
            {{"Fruit", 1.5f}
            ,{"Vegetable", 2.0f}
            ,{"Dairy", 2.2f}
            ,{"Grain", 2.3f}
            ,{"Protein", 2.7f}
            }
    
        ,OutputMaxQty = 48
        ,OutputOutPerCompostPortion = 1
        ,InoculumOutPerSourPortion = 1
            
        ,HarvestMaxPerStack = 8
        };
}

public sealed class CompostpileIngredientSettings
{
    public string Name { get; }
    public int InitQty { get; }
    public int PlacedBonusQty { get; }
    public int MaxQty { get; }
    public int MaxInput { get; }
    
    public int InPerCompostPortion { get; }
    public int InPerSourPortion { get; }

    public Dictionary<string, float> AddItemCodeRatios { get; }

    public CompostpileIngredientSettings
        (string name
        ,int initQty, int placedBonusQty
        ,int maxQty, int maxInput
        ,int inPerCompostPortion, int inPerSourPortion
        ,Dictionary<string, float>? addItemCodeRatios = null
        )
    {
        Name = name;
        InitQty = initQty; PlacedBonusQty = placedBonusQty;
        MaxQty = maxQty; MaxInput = maxInput;
        InPerCompostPortion = inPerCompostPortion; InPerSourPortion = inPerSourPortion;
        AddItemCodeRatios = addItemCodeRatios ?? new Dictionary<string, float>();
    }

    public static int GetStackNormalizedRatio(CollectibleObject? collectible) =>
        collectible?.MaxStackSize > 0
    &&  collectible .MaxStackSize != 64
    ?   Math.Max(64 / collectible.MaxStackSize, 1)
    :   1;

    public bool TryAddRef(ItemSlot slot, out int accepted, ref int currentQty)
    {
        accepted = 0;
        if (AddItemCodeRatios is null
        ||  AddItemCodeRatios.Count == 0
           )
            return false;

        int room = MaxQty - currentQty;
        if (room < 1)
            return false;

        string code =
            slot.Itemstack?.Item?.Code.ToString()
        ??  slot.Itemstack?.Block?.Code.ToString()
        ??  "";
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