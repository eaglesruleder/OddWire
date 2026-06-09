using System.Collections.Generic;

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
    }

    public const int ShapeSizeLevels = 5;
    
    #region IngredientTuning
    public Ingredient Browns { get; private set; } = new()
    {
        Name = "browns",
        InitialQty = 16,
        SizeBonusQty = (64 * 3 - 16) / (ShapeSizeLevels - 1),
        MaxQty = 64 * 3,
        MaxInputPerAdd = 16,
        Aeration01PerInput = 0.5f / (64 * 3),
        AddItemCodeRatios = new Dictionary<string, float>
        {
            { "game:drygrass", 1f }
        },
        HarvestItemPath = "game:drygrass",
        HarvestQty = 64,
        HarvestStackQty = 64,
        ConsumePerTransition = 12
    };

    public Ingredient Nutrition { get; private set; } = new()
    {
        Name = "nutrition",
        InitialQty = 0,
        SizeBonusQty = (64 - 0) / (ShapeSizeLevels - 1),
        MaxQty = 64,
        MaxInputPerAdd = 8,
        Aeration01PerInput = 0.5f / 64,
        ConsumePerTransition = 3
    };

    public Ingredient Inoculum { get; private set; } = new()
    {
        Name = "inoculum",
        InitialQty = 4,
        SizeBonusQty = (64 - 4) / (ShapeSizeLevels - 1),
        MaxQty = 64,
        MaxInputPerAdd = 4,
        Aeration01PerInput = 0.5f / 64,
        AddItemCodeRatios = new Dictionary<string, float>
        {
            { "game:rot", 1f },
            { "game:compost", 0.25f }
        },
        HarvestItemPath = "game:rot",
        HarvestQty = 32,
        HarvestStackQty = 32,
        ConsumePerTransition = 1
    };

    public float Aeration01PerCompostpileInput { get; internal set; } = 0.01f;
    #endregion

    #region CompostRate
    public float BaseCompostRatePerHour { get; private set; } = 0.05f;
    public float InoculumMinFactor { get; private set; } = 0.25f;
    public float NutritionBaseFactor { get; private set; } = 1.25f;
    public Dictionary<string, float>? NutritionBonusFactor { get; private set; } = new()
    {
        { "Dairy", 1.3f },
        { "Fruit", 1.6f },
        { "Grain", 1.8f },
        { "Vegetable", 2.2f },
        { "Protein", 2.6f }
    };
    #endregion

    #region Moisture
    public float Moisture01WaterRate { get; private set; } = 0.33f;
    public float Moisture01Initial { get; private set; } = 0.85f;
    public float Moisture01Optimal { get; private set; } = 0.60f;
    public float Moisture01GainPerRainyDay { get; private set; } = 0.30f;
    public float MoistureAmbientRetentionDays { get; private set; } = 16f;
    public float DrowningThreshold { get; private set; } = 0.85f;
    public float DrowningTolerance { get; private set; } = 0.15f;
    #endregion

    #region Aeration
    public float AerationRetentionDays { get; private set; } = 10f;
    public float AerationBlockedPenalty { get; private set; } = 0.2f;
    public float HypoxicThreshold { get; private set; } = 0.35f;
    public float HypoxicTolerance { get; private set; } = 0.20f;
    public Dictionary<string, float>? NutritionAerationConsumption { get; private set; } = new()
    {
        { "Dairy", 2.7f },
        { "Fruit", 2.1f },
        { "Grain", 1.7f },
        { "Vegetable", 1.3f },
        { "Protein", 2.3f }
    };
    #endregion

    #region Temperature
    public float HeatingRatePerHour { get; private set; } = 42f;
    public float CoolingRatePerHour { get; private set; } = 0.16f;
    public float GreenhouseHeat { get; private set; } = 3f;
    public float NeighbourHeatRate { get; private set; } = 1f;
    public Dictionary<string, float>? NutritionHeat { get; private set; } = new()
    {
        { "Dairy", 2.0f },
        { "Fruit", 1.2f },
        { "Grain", 1.6f },
        { "Vegetable", 1.8f },
        { "Protein", 2.7f }
    };
    public float OverheatThreshold { get; private set; } = 65f;
    public float OverheatTolerance { get; private set; } = 10f;
    #endregion

    #region Stress
    public float StressGainDays { get; private set; } = 1.5f;
    public float StressRecoveryDays { get; private set; } = 3f;

    public int CompostOutPerSuccess { get; private set; } = 1;
    public int InoculumOutPerFail { get; private set; } = 1;
    #endregion

    #region Harvest
    public string HarvestCompostPath { get; private set; } = "game:compost";
    public int HarvestCompostQty { get; internal set; } = 2;
    public int HarvestCompostStackQty { get; internal set; } = 2;
    public string HarvestCompostpilePath { get; private set; } = "oddwire:compostpile-#1";
    public int HarvestCompostpileQty { get; internal set; } = 2;
    public int HarvestCompostpileStackQty { get; internal set; } = 2;

    public int TotalMaxQty => Browns.MaxQty + Nutrition.MaxQty + Inoculum.MaxQty;
    #endregion

    #region BlockInfo
    public bool InfoDebug = true;
    public float InfoFactorWarningThreshhold = 0.8f;
    public float InfoNeedsPortionsThreshhold = 2f;
    #endregion
}
