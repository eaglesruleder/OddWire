using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace OddWire.GameContent;

public class CompostpileSettings
{
    public class Ingredient
    {
        public string Name { get; internal set; }
        
        public int InitialQty { get; internal set; }
        public int SizeBonusQty { get; internal set; }
        public int MaxQty { get; internal set; }
        public int MaxInputPerAdd { get; internal set; }

        public Dictionary<string, float> ItemCodeAddRatios { get; internal set; }
    
        public int ConsumePerTransition { get; internal set; }

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
    public Dictionary<string, float>? NutritionSpeed { get; private set; }
    public float NutritionSensitivity { get; private set; }
    
    public float Moisture01Initial { get; private set; }
    public float Moisture01Optimal { get; private set; }
    public float Moisture01Sensitivity { get; private set; }
    public float Moisture01GainPerRainyDay { get; private set; }
    public float Moisture01LossPerDay { get; private set; }
    public float DrowningThreshold { get; private set; }
    public float DrowningTolerance { get; private set; }
    
    public float AerationLossPerDay { get; private set; }
    public float HypoxicThreshold { get; private set; }
    public float HypoxicTolerance { get; private set; }
    
    public float HeatingRatePerHour { get; private set; }
    public float CoolingRatePerHour { get; private set; }
    public float GreenhouseHeat { get; private set; }
    public Dictionary<string, float>? NutritionHeat { get; private set; }
    public float OverheatThreshold { get; private set; }
    public float OverheatTolerance { get; private set; }
    
    public int CompostMaxQty { get; private set; }
    public int CompostOutPerSuccess { get; private set; }
    public int InoculumOutPerFail { get; private set; }
    
    public int HarvestMaxPerStack { get; private set; }
    public int InoculumPerSourDropped { get; internal set; }
    
    public static CompostpileSettings Default = new()
        {Browns = new()
            {Name = "browns"
            ,InitialQty = 16
            ,SizeBonusQty = 44
            ,MaxQty = 64 * 3
            ,MaxInputPerAdd = 16
            ,ItemCodeAddRatios = new Dictionary<string, float>
                {{"game:drygrass", 1f}
                }
            ,ConsumePerTransition = 16
            }
        ,Nutrition = new Ingredient
            {Name = "nutrition"
            ,InitialQty = 16
            ,SizeBonusQty = 12
            ,MaxQty = 64
            ,MaxInputPerAdd = 8
            ,ConsumePerTransition = 8
            }
        ,Inoculum = new Ingredient
            {Name = "inoculum"
            ,InitialQty = 2
            ,SizeBonusQty = 8
            ,MaxQty = 16
            ,MaxInputPerAdd = 4
            ,ItemCodeAddRatios = new Dictionary<string, float>
                {{"game:compost", 1f}
                ,{"game:rot", 2}
                ,{"oddwire:sourcompost", 4}
                }
            ,ConsumePerTransition = 1
            }
        
        ,BaseCompostRatePerHour = 0.33f
        ,NutritionSpeed = new Dictionary<string, float>
            {{"Fruit", 1.5f}
            ,{"Vegetable", 2.0f}
            ,{"Dairy", 2.2f}
            ,{"Grain", 2.3f}
            ,{"Protein", 2.7f}
            }
        ,NutritionSensitivity = 0.35f
        
        ,Moisture01Initial = 0.55f
        ,Moisture01Optimal = 0.60f
        ,Moisture01Sensitivity = 0.35f
        ,Moisture01GainPerRainyDay = 0.40f
        ,Moisture01LossPerDay = 0.25f
        ,DrowningThreshold = 0.8f
        ,DrowningTolerance = 0.2f
        
        ,AerationLossPerDay = 0.96f
        ,HypoxicThreshold = 0.15f
        ,HypoxicTolerance = 0.15f
        
        ,HeatingRatePerHour = 45
        ,CoolingRatePerHour = 0.18f
        ,GreenhouseHeat = 5f
        ,NutritionHeat = new Dictionary<string, float>()
            {{"Fruit", 1.5f}
            ,{"Vegetable", 2.0f}
            ,{"Dairy", 2.2f}
            ,{"Grain", 2.3f}
            ,{"Protein", 2.7f}
            }
        ,OverheatThreshold = 65
        ,OverheatTolerance = 12
    
        ,CompostMaxQty = 48
        ,CompostOutPerSuccess = 1
        ,InoculumOutPerFail = 1
            
        ,HarvestMaxPerStack = 8
        ,InoculumPerSourDropped = 1
        };
}