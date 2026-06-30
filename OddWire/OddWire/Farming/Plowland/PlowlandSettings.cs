namespace OddWire.GameContent;

public sealed class PlowlandSettings
{
    public string StateDry   = "dry";
    public string StateMoist = "moist";
    public string DefaultFertility = FertilitySet.Low;

    // Used in UpdateFarmlandBlock — matches vanilla's IsVisiblyMoist threshold
    public float MoistVisibleThreshold = 0.10f;

    // Support block retention — scaled by support fertility, drives totalHoursWaterRetention
    public float DefaultRetentionDays = 4f;
    public float MinRetentionDays     = 0.25f;

    // Used in ItemPlow.DoPlow to cap result nutrients
    public float Max = 150f;

    // Slow-release nutrient ceiling — matches vanilla BlockEntitySoilNutrition cap
    public float SlowReleaseMax = 150f;

    // Avg-nutrient pivot in DoPlow: below it the richer block risks losing a tier, above it the poorer block can gain one
    public float FertilityNeutral = 100f;

    // ItemPlow held-interact tuning
    public float PlowSecondsRequired   = 0.6f;
    public float PlowWalkSpeedPenalty  = -0.4f;
}
