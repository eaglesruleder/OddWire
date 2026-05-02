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
}
