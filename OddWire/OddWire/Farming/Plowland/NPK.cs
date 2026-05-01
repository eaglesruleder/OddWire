using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public sealed class NPK
{
    public const int Original = 0;
    public const int Current = 1;
    public const int OverTime = 2;

    private const int LayerCount = 3;
    public static readonly char[] Keys = ['N', 'P', 'K']; // add 'C','O' here later
    private readonly float[,] _v = new float[Keys.Length, LayerCount];

    public float Max = 150f;
    public float RecoveryPerTick = 0.25f;
    public float ReleasePerTick = 0.25f;
    public double PrevTimeUpdated = -1;

    public float this[char key] { get => this[key, Current]; set => this[key, Current] = value; }
    public float this[char key, int layer]
    {   get => _v[GetKeyIndex(key), GetLayerIndex(layer)];
        set => _v[GetKeyIndex(key), GetLayerIndex(layer)] = value;
    }

    public void SetRules
        (float max
        ,float recoveryPerTick
        ,float releasePerTick
        )
    {
        Max = Math.Max(0f, max);
        RecoveryPerTick = Math.Max(0f, recoveryPerTick);
        ReleasePerTick = Math.Max(0f, releasePerTick);
    }

    private static int GetKeyIndex(char key)
    {
        char lookupKey = char.ToUpperInvariant(key);
        for (int i = 0; i < Keys.Length; i++)
            if (Keys[i] == lookupKey)
                return i;
        throw new ArgumentOutOfRangeException(nameof(key), key, $"Nutrient key must be one of: {string.Join(", ", Keys)}.");
    }

    private static int GetLayerIndex(int layer)
    {
        if (layer < 0
        ||  layer >= LayerCount
           )
            throw new ArgumentOutOfRangeException(nameof(layer), layer, "Nutrient layer must be 0 original, 1 current, or 2 overtime.");
        return layer;
    }

    public void Initialise(float originalValue, NPK? nutrients = null)
    {
        foreach (char key in Keys)
        {
            this[key, Original] = GameMath.Clamp(originalValue, 0f, Max);
            this[key, Current] = nutrients is null
            ?   this[key, Original]
            :   GameMath.Clamp(nutrients[key], 0f, Max);
            this[key, OverTime] = 0f;
        }
        PrevTimeUpdated = -1;
    }

    public void AddOverTime(char key, float value) =>
        this[key, OverTime] += Math.Min(Math.Max(0f, Max - this[key, OverTime]), Math.Max(0f, value));
    
    public bool Tick(double totalHours)
    {
        if (PrevTimeUpdated < 0)
        {
            PrevTimeUpdated = totalHours;
            return false;
        }

        double hoursPassed = totalHours - PrevTimeUpdated;
        if (hoursPassed <= 0)
            return false;
        PrevTimeUpdated = totalHours;

        bool changed = false;
        foreach (char key in Keys)
        {
            float prevCurrent = this[key, Current];
            float prevOverTime = this[key, OverTime];

            if (this[key, Current] < this[key, Original])
                this[key, Current] = Math.Min(this[key, Original], this[key, Current] + RecoveryPerTick * (float)hoursPassed / 3f);

            if (this[key, OverTime] > 0)
            {
                float release = Math.Min(ReleasePerTick * (float)hoursPassed / 3f, this[key, OverTime]);
                this[key, Current] = Math.Min(Max, this[key, Current] + release);
                this[key, OverTime] = Math.Max(0f, this[key, OverTime] - release);
            }

            changed |=
               !this[key, Current].Approx(prevCurrent)
            || !this[key, OverTime].Approx(prevOverTime);
        }
        return changed;
    }

    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetDouble("prevTimeNutrientsUpdated", PrevTimeUpdated);
        tree.SetFloat("max", Max);
        tree.SetFloat("recoveryPerTick", RecoveryPerTick);
        tree.SetFloat("releasePerTick", ReleasePerTick);
        for(int i = 0; i < Keys.Length; i++)
            for(int j = 0; j < LayerCount; j++)
                tree.SetFloat($"{Keys[i]}{j}", this[Keys[i], j]);
    }

    public void FromTreeAttributes(ITreeAttribute tree)
    {
        PrevTimeUpdated = tree.GetDouble("prevTimeNutrientsUpdated", -1);
        SetRules
            (tree.GetFloat("max", tree.GetFloat("maxFertilizedNutrient", Max))
            ,tree.GetFloat("recoveryPerTick", tree.GetFloat("fertilityRecoveryPerTick", RecoveryPerTick))
            ,tree.GetFloat("releasePerTick", tree.GetFloat("fertilizerReleasePerTick", ReleasePerTick))
            );
        for(int i = 0; i < Keys.Length; i++)
            for(int j = 0; j < LayerCount; j++)
                this[Keys[i], j] = tree.GetFloat($"{Keys[i]}{j}");
    }
    #endregion
}
