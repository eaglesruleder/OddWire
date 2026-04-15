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

    #region IngredientTuning
    public Ingredient Browns { get; private set; } = new()
    {
        Name = "browns",
        InitialQty = 16,
        SizeBonusQty = 44,
        MaxQty = 64 * 3,
        MaxInputPerAdd = 16,
        Aeration01PerInput = 1f / (64 * 3),
        AddItemCodeRatios = new Dictionary<string, float>
        {
            { "game:drygrass", 1f }
        },
        HarvestItemPath = "game:drygrass",
        HarvestQty = 64,
        HarvestStackQty = 64,
        ConsumePerTransition = 16
    };

    public Ingredient Nutrition { get; private set; } = new()
    {
        Name = "nutrition",
        InitialQty = 16,
        SizeBonusQty = 12,
        MaxQty = 64,
        MaxInputPerAdd = 8,
        Aeration01PerInput = 1f / 64,
        ConsumePerTransition = 8
    };

    public Ingredient Inoculum { get; private set; } = new()
    {
        Name = "inoculum",
        InitialQty = 2,
        SizeBonusQty = 8,
        MaxQty = 64,
        MaxInputPerAdd = 4,
        Aeration01PerInput = 1f / 16,
        AddItemCodeRatios = new Dictionary<string, float>
        {
            { "game:rot", 1f },
            { "game:compost", 4f }
        },
        HarvestItemPath = "game:rot",
        HarvestQty = 32,
        HarvestStackQty = 32,
        ConsumePerTransition = 1
    };

    public float Aeration01PerCompostpileInput { get; internal set; } = 1.0f;
    #endregion

    #region CompostRateTuning
    public float BaseCompostRatePerHour { get; private set; } = 0.05f;
    public Dictionary<string, float>? NutritionSpeed { get; private set; } = new()
    {
        { "Fruit", 1.5f },
        { "Vegetable", 2.0f },
        { "Dairy", 2.2f },
        { "Grain", 2.3f },
        { "Protein", 2.7f }
    };
    #endregion

    #region MoistureTuning
    public float Moisture01WaterRate { get; private set; } = 0.33f;
    public float Moisture01Initial { get; private set; } = 0.85f;
    public float Moisture01Optimal { get; private set; } = 0.60f;
    public float Moisture01GainPerRainyDay { get; private set; } = 0.30f;
    public float MoistureAmbientRetentionDays { get; private set; } = 16f;
    public float DrowningThreshold { get; private set; } = 0.85f;
    public float DrowningTolerance { get; private set; } = 0.15f;
    #endregion

    #region AerationTuning
    public float AerationRetentionDays { get; private set; } = 10f;
    public float AerationBlockedPenalty { get; private set; } = 0.2f;
    public float HypoxicThreshold { get; private set; } = 0.35f;
    public float HypoxicTolerance { get; private set; } = 0.20f;
    #endregion

    #region TemperatureTuning
    public float HeatingRatePerHour { get; private set; } = 42f;
    public float CoolingRatePerHour { get; private set; } = 0.16f;
    public float GreenhouseHeat { get; private set; } = 3f;
    public float NeighbourHeatRate { get; private set; } = 1f;
    public Dictionary<string, float>? NutritionHeat { get; private set; } = new()
    {
        { "Fruit", 1.5f },
        { "Vegetable", 2.0f },
        { "Dairy", 2.2f },
        { "Grain", 2.3f },
        { "Protein", 2.7f }
    };
    public float OverheatThreshold { get; private set; } = 65f;
    public float OverheatTolerance { get; private set; } = 10f;
    #endregion

    #region StressAndOutputTuning
    public float StressGainDays { get; private set; } = 1.5f;
    public float StressRecoveryDays { get; private set; } = 3f;

    public int CompostOutPerSuccess { get; private set; } = 1;
    public int InoculumOutPerFail { get; private set; } = 1;
    #endregion

    #region HarvestTuning
    public string HarvestCompostPath { get; private set; } = "game:compost";
    public int HarvestCompostQty { get; internal set; } = 8;
    public int HarvestCompostStackQty { get; internal set; } = 8;
    public string HarvestCompostpilePath { get; private set; } = "oddwire:compostpile-#1";
    public int HarvestCompostpileQty { get; internal set; } = 2;
    public int HarvestCompostpileStackQty { get; internal set; } = 2;

    public int TotalMaxQty => Browns.MaxQty + Nutrition.MaxQty + Inoculum.MaxQty;
    #endregion

    #region BlockInfoDisplayTuning
    public bool InfoDebug = false;
    public float InfoFactorWarningThreshhold = 0.8f;
    public float InfoNeedsPortionsThreshhold = 2f;
    #endregion
}
