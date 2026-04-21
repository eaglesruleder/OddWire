using System.Collections.Generic;

namespace OddWire.GameContent;

public sealed class PlowlandSettings
{
    public const string VariantState = "state";
    public const string VariantFertility = "fertility";

    public const string StateDry = "dry";
    public const string StateMoist = "moist";
    public const string DefaultFertility = FertilitySet.Low;

    public float MoistVisibleThreshold = 0.10f;
    public float DefaultRetentionDays = 4f;
    public float FertilityRecoveryPerTick = 0.25f;
    public float FertilizerReleasePerTick = 0.25f;
    public float MaxFertilizedNutrient = 150f;
    public int WaterSearchRadius = 4;
}

public readonly record struct PlowlandSupportModel(
    bool IsValid,
    string? SupportCode,
    float RetentionDays,
    float WaterQuality01,
    string FertilityCode
);
