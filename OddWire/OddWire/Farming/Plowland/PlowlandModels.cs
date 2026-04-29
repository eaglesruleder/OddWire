namespace OddWire.GameContent;

public sealed class PlowlandSettings
{
    public string VariantState = "state";
    public string VariantFertility = "fertility";

    public string StateDry = "dry";
    public string StateMoist = "moist";
    public string DefaultFertility = FertilitySet.Low;

    public float MoistVisibleThreshold = 0.10f;
    public float DefaultRetentionDays = 4f;
    public float FertilityRecoveryPerTick = 0.25f;
    public float FertilizerReleasePerTick = 0.25f;
    public float MaxFertilizedNutrient = 150f;
    public int WaterSearchRadius = 4;
}
