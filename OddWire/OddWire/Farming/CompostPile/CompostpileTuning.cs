namespace OddWire.GameContent;

public sealed class CompostpileIngredientTuning
{
    public string Name { get; }
    public int InitQty { get; }
    public int PlacedBonusQty { get; }
    public int MaxQty { get; }
    public int MaxInput { get; }
    public int InPerCompostPortion { get; }
    
    public int InPerSourAdded { get; }
    public int InPerRotAdded { get; }

    public CompostpileIngredientTuning(
        string name,
        int initQty,
        int placedBonusQty,
        int maxQty,
        int maxInput,
        int inPerCompostPortion,
        int inPerSourAdded = 1,
        int inPerRotAdded = 1
    )
    {
        Name = name;

        InitQty = initQty;
        PlacedBonusQty = placedBonusQty;
        MaxQty = maxQty;
        MaxInput = maxInput;
        InPerCompostPortion = inPerCompostPortion;

        InPerSourAdded = inPerSourAdded < 1 ? 1 : inPerSourAdded;
        InPerRotAdded  = inPerRotAdded  < 1 ? 1 : inPerRotAdded;
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
    public CompostpileIngredientTuning Browns { get; }
    public CompostpileIngredientTuning Nutrition { get; }
    public CompostpileIngredientTuning Inoculum { get; }

    public CompostpileProcessTuning Process { get; }
    public CompostpileOutputTuning Output { get; }
    public CompostpileHarvestTuning Harvest { get; }

    public CompostpileTuning(
        CompostpileIngredientTuning browns,
        CompostpileIngredientTuning nutrition,
        CompostpileIngredientTuning inoculum,
        CompostpileProcessTuning process,
        CompostpileOutputTuning output,
        CompostpileHarvestTuning harvest
    )
    {
        Browns = browns;
        Nutrition = nutrition;
        Inoculum = inoculum;

        Process = process;
        Output = output;
        Harvest = harvest;
    }
}

public static class CompostpileTuningModels
{
    public static readonly CompostpileTuning Default = new(
        browns: new CompostpileIngredientTuning(
            name: "browns",
            initQty: 16,
            placedBonusQty: 44,
            maxQty: 64 * 3,
            maxInput: 16,
            inPerCompostPortion: 16
        ),
        nutrition: new CompostpileIngredientTuning(
            name: "nutrition",
            initQty: 16,
            placedBonusQty: 12,
            maxQty: 64,
            maxInput: 8,
            inPerCompostPortion: 8
        ),
        inoculum: new CompostpileIngredientTuning(
            name: "inoculum",
            initQty: 2,
            placedBonusQty: 8,
            maxQty: 16,
            maxInput: 4,
            inPerCompostPortion: 1,

            // inoculum-specific “add ratios” (source items per +1 inoculum)
            inPerSourAdded: 2,
            inPerRotAdded: 4
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