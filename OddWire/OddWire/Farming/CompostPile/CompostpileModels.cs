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
        public float Aeration01PerInput { get; internal set; }

        public Dictionary<string, float> AddItemCodeRatios { get; internal set; }
        public string HarvestItemPath { get; internal set; }
        public int HarvestQty { get; internal set; }
        public int HarvestStackQty { get; internal set; }
    
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
    
    public float Aeration01PerCompostpileInput { get; internal set; }
    
    public float BaseCompostRatePerHour { get; private set; }
    public Dictionary<string, float>? NutritionSpeed { get; private set; }
    
    public float Moisture01Initial { get; private set; }
    public float Moisture01Optimal { get; private set; }
    public float Moisture01GainPerRainyDay { get; private set; }
    public float MoistureAmbientRetentionDays { get; private set; }
    public float DrowningThreshold { get; private set; }
    public float DrowningTolerance { get; private set; }
    
    public float AerationRetentionDays { get; private set; }
    public float HypoxicThreshold { get; private set; }
    public float HypoxicTolerance { get; private set; }
    
    public float HeatingRatePerHour { get; private set; }
    public float CoolingRatePerHour { get; private set; }
    public float GreenhouseHeat { get; private set; }
    public Dictionary<string, float>? NutritionHeat { get; private set; }
    public float OverheatThreshold { get; private set; }
    public float OverheatTolerance { get; private set; }
    
    public float StressGainDays { get; private set; }
    public float StressRecoveryDays { get; private set; }
    
    public int CompostOutPerSuccess { get; private set; }
    public int InoculumOutPerFail { get; private set; }
    
    
    
    public string HarvestCompostPath { get; private set; }
    public int HarvestCompostQty { get; internal set; }
    public int HarvestCompostStackQty { get; internal set; }
    public string HarvestCompostpilePath { get; private set; }
    public int HarvestCompostpileQty { get; internal set; }
    public int HarvestCompostpileStackQty { get; internal set; }
    
    public int TotalMaxQty => Browns.MaxQty + Nutrition.MaxQty + Inoculum.MaxQty;
    
    public static readonly CompostpileSettings Default = new()
        {Browns = new()
            {Name = "browns"
            ,InitialQty = 16
            ,SizeBonusQty = 44
            ,MaxQty = 64 * 3
            ,MaxInputPerAdd = 16
            ,Aeration01PerInput = 1f/(64*3)
            ,AddItemCodeRatios = new Dictionary<string, float>
                {{"game:drygrass", 1f}
                }
            ,HarvestItemPath = "game:drygrass"
            ,HarvestQty = 64
            ,HarvestStackQty = 64
            ,ConsumePerTransition = 16
            }
            
        ,Nutrition = new Ingredient
            {Name = "nutrition"
            ,InitialQty = 16
            ,SizeBonusQty = 12
            ,MaxQty = 64
            ,MaxInputPerAdd = 8
            ,Aeration01PerInput = 1f/64
            ,ConsumePerTransition = 8
            }
        ,Inoculum = new Ingredient
            {Name = "inoculum"
            ,InitialQty = 2
            ,SizeBonusQty = 8
            ,MaxQty = 64
            ,MaxInputPerAdd = 4
            ,Aeration01PerInput = 1f/16
            ,AddItemCodeRatios = new Dictionary<string, float>
                {{"game:rot", 1}
                ,{"game:compost", 4f}
                }
            ,HarvestItemPath = "game:rot"
            ,HarvestQty = 32
            ,HarvestStackQty = 32
            ,ConsumePerTransition = 1
            }
        ,Aeration01PerCompostpileInput = 1.0f
        
        ,BaseCompostRatePerHour = 0.05f
        ,NutritionSpeed = new Dictionary<string, float>
            {{"Fruit", 1.5f}
            ,{"Vegetable", 2.0f}
            ,{"Dairy", 2.2f}
            ,{"Grain", 2.3f}
            ,{"Protein", 2.7f}
            }
        
        ,Moisture01Initial = 0.85f
        ,Moisture01Optimal = 0.60f
        ,Moisture01GainPerRainyDay = 0.30f
        ,MoistureAmbientRetentionDays = 16f
        ,DrowningThreshold = 0.85f
        ,DrowningTolerance = 0.15f
        
        ,AerationRetentionDays = 10f
        ,HypoxicThreshold = 0.35f
        ,HypoxicTolerance = 0.20f
        
        ,HeatingRatePerHour = 42
        ,CoolingRatePerHour = 0.16f
        ,GreenhouseHeat = 3f
        ,NutritionHeat = new Dictionary<string, float>()
            {{"Fruit", 1.5f}
            ,{"Vegetable", 2.0f}
            ,{"Dairy", 2.2f}
            ,{"Grain", 2.3f}
            ,{"Protein", 2.7f}
            }
        ,OverheatThreshold = 65
        ,OverheatTolerance = 10
        
        ,StressGainDays = 1.5f
        ,StressRecoveryDays = 3f
        
        ,CompostOutPerSuccess = 1
        ,InoculumOutPerFail = 1
            
        ,HarvestCompostPath = "game:compost"
        ,HarvestCompostQty = 8
        ,HarvestCompostStackQty = 8
        
        ,HarvestCompostpilePath = "oddwire:Compostpile-#1"
        ,HarvestCompostpileQty = 2
        ,HarvestCompostpileStackQty = 2
        };
}