using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace OddWire.GameContent;

public class CompostpileSettings
{
    public class Ingredient
    {
        public string Name { get; internal set; }
        public int InitQty { get; internal set; }
        public int PlacedBonusQty { get; internal set; }
        public int MaxQty { get; internal set; }
        public int MaxInput { get; internal set; }
    
        public int InPerCompostPortion { get; internal set; }
        public int InPerSourPortion { get; internal set; }

        public Dictionary<string, float> AddItemCodeRatios { get; internal set; }

        public static int GetStackNormalizedRatio(CollectibleObject? collectible) =>
            collectible?.MaxStackSize > 0
        &&  collectible .MaxStackSize != 64
        ?   Math.Max(64 / collectible.MaxStackSize, 1)
        :   1;
    }
    
    public Ingredient Browns { get; private set; }
    public Ingredient Nutrition { get; private set; }
    public Ingredient Inoculum { get; private set; }
    
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
        {Browns = new()
            {Name = "browns"
            ,InitQty = 16
            ,PlacedBonusQty = 44
            ,MaxQty = 64 * 3
            ,MaxInput = 16
            ,InPerCompostPortion = 16
            ,InPerSourPortion = 8
            ,AddItemCodeRatios = new Dictionary<string, float>
                {{"game:drygrass", 1f}
                }
            }
        ,Nutrition = new Ingredient
            {Name = "nutrition"
            ,InitQty = 16
            ,PlacedBonusQty = 12
            ,MaxQty = 64
            ,MaxInput = 8
            ,InPerCompostPortion = 8
            ,InPerSourPortion = 4
            }
        ,Inoculum = new Ingredient
            {Name = "inoculum"
            ,InitQty = 2
            ,PlacedBonusQty = 8
            ,MaxQty = 16
            ,MaxInput = 4
            ,InPerCompostPortion = 1
            ,InPerSourPortion = 1
            ,AddItemCodeRatios = new Dictionary<string, float>
                {{"game:compost", 1f}
                ,{"game:rot", 2}
                ,{"oddwire:sourcompost", 4}
                }
            }
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