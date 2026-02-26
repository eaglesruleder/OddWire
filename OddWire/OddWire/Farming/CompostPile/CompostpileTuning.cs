namespace OddWire.GameContent;

public sealed class CompostpileInputTuning
{
    public int BrownsInit { get; }
    public int BrownsPlacedBonus { get; }
    public int BrownsMaxQty { get; }
    public int BrownsMaxInput { get; }
    public int BrownsInPerCompostPortion { get; }

    public int NutritionInit { get; }
    public int NutritionPlacedBonus { get; }
    public int NutritionMaxQty { get; }
    public int NutritionMaxInput { get; }
    public int NutritionInPerCompostPortion { get; }

    public int InoculumInit { get; }
    public int InoculumPlacedBonus { get; }
    public int InoculumMaxQty { get; }
    public int InoculumMaxInput { get; }

    public int InoculumInPerCompostPortion { get; }
    public int InoculumInPerSourAdded { get; }
    public int InoculumInPerRotAdded { get; }

    public CompostpileInputTuning(
        int brownsInit,
        int brownsPlacedBonus,
        int brownsMaxQty,
        int brownsMaxInput,
        int brownsInPerCompostPortion,
        int nutritionInit,
        int nutritionPlacedBonus,
        int nutritionMaxQty,
        int nutritionMaxInput,
        int nutritionInPerCompostPortion,
        int inoculumInit,
        int inoculumPlacedBonus,
        int inoculumMaxQty,
        int inoculumMaxInput,
        int inoculumInPerCompostPortion,
        int inoculumInPerSourAdded,
        int inoculumInPerRotAdded
    )
    {
        BrownsInit = brownsInit;
        BrownsPlacedBonus = brownsPlacedBonus;
        BrownsMaxQty = brownsMaxQty;
        BrownsMaxInput = brownsMaxInput;
        BrownsInPerCompostPortion = brownsInPerCompostPortion;

        NutritionInit = nutritionInit;
        NutritionPlacedBonus = nutritionPlacedBonus;
        NutritionMaxQty = nutritionMaxQty;
        NutritionMaxInput = nutritionMaxInput;
        NutritionInPerCompostPortion = nutritionInPerCompostPortion;

        InoculumInit = inoculumInit;
        InoculumPlacedBonus = inoculumPlacedBonus;
        InoculumMaxQty = inoculumMaxQty;
        InoculumMaxInput = inoculumMaxInput;

        InoculumInPerCompostPortion = inoculumInPerCompostPortion;
        InoculumInPerSourAdded = inoculumInPerSourAdded;
        InoculumInPerRotAdded = inoculumInPerRotAdded;
    }
}

public sealed class CompostpileProcessTuning
{
    public float BaseCompostRatePerHour { get; }

    public float DefaultMoisture01 { get; }
    public float OptimalMoisture01 { get; }
    public float RainToMoisturePerDay { get; }
    public float DryoutPerDayAt20C { get; }

    public float GreenhouseTempBonusC { get; }

    public CompostpileProcessTuning(
        float baseCompostRatePerHour,
        float defaultMoisture01,
        float optimalMoisture01,
        float rainToMoisturePerDay,
        float dryoutPerDayAt20C,
        float greenhouseTempBonusC
    )
    {
        BaseCompostRatePerHour = baseCompostRatePerHour;
        DefaultMoisture01 = defaultMoisture01;
        OptimalMoisture01 = optimalMoisture01;
        RainToMoisturePerDay = rainToMoisturePerDay;
        DryoutPerDayAt20C = dryoutPerDayAt20C;
        GreenhouseTempBonusC = greenhouseTempBonusC;
    }
}

public sealed class CompostpileOutputTuning
{
    public int OutputMaxQty { get; }
    public int OutputOutPerCompostPortion { get; }

    public int InoculumOutPerSourPortion { get; }

    public CompostpileOutputTuning(
        int outputMaxQty,
        int outputOutPerCompostPortion,
        int inoculumOutPerSourPortion
    )
    {
        OutputMaxQty = outputMaxQty;
        OutputOutPerCompostPortion = outputOutPerCompostPortion;
        InoculumOutPerSourPortion = inoculumOutPerSourPortion;
    }
}

public sealed class CompostpileHarvestTuning
{
    public int HarvestMaxPerStack { get; }

    public CompostpileHarvestTuning(int harvestMaxPerStack)
    {
        HarvestMaxPerStack = harvestMaxPerStack;
    }
}

public sealed class CompostpileTuning
{
    public CompostpileInputTuning Input { get; }
    public CompostpileProcessTuning Process { get; }
    public CompostpileOutputTuning Output { get; }
    public CompostpileHarvestTuning Harvest { get; }

    public CompostpileTuning(
        CompostpileInputTuning input,
        CompostpileProcessTuning process,
        CompostpileOutputTuning output,
        CompostpileHarvestTuning harvest
    )
    {
        Input = input;
        Process = process;
        Output = output;
        Harvest = harvest;
    }
}

public static class CompostpileTuningModels
{
    public static readonly CompostpileTuning Default = new (
        input: new CompostpileInputTuning(
            brownsInit: 16,
            brownsPlacedBonus: 44,
            brownsMaxQty: 64 * 3,
            brownsMaxInput: 16,
            brownsInPerCompostPortion: 16,

            nutritionInit: 16,
            nutritionPlacedBonus: 12,
            nutritionMaxQty: 64,
            nutritionMaxInput: 8,
            nutritionInPerCompostPortion: 8,

            inoculumInit: 2,
            inoculumPlacedBonus: 8,
            inoculumMaxQty: 16,
            inoculumMaxInput: 4,

            inoculumInPerCompostPortion: 1,
            inoculumInPerSourAdded: 2,
            inoculumInPerRotAdded: 4
        ),
        process: new CompostpileProcessTuning(
            baseCompostRatePerHour: 0.33f,
            defaultMoisture01: 0.55f,
            optimalMoisture01: 0.60f,
            rainToMoisturePerDay: 0.40f,
            dryoutPerDayAt20C: 0.25f,
            greenhouseTempBonusC: 5f
        ),
        output: new CompostpileOutputTuning(
            outputMaxQty: 48,
            outputOutPerCompostPortion: 1,
            inoculumOutPerSourPortion: 1
        ),
        harvest: new CompostpileHarvestTuning(
            harvestMaxPerStack: 8
        )
    );
}