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
    public float MinRetentionDays = 0.25f;
    public float WaterPerSecond = 0.5f;
    public int WaterSearchRadius = 4;

    public float RecoveryPerTick = 0.25f;
    public float ReleasePerTick = 0.25f;
    public float Max = 150f;

    public float GrowthRateMul = 1f;
}
