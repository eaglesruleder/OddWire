namespace OddWire.GameContent;

public sealed class CompostpileInventory
{
    public CompostpileIngredient Browns { get; }
    public CompostpileIngredient Nutrition { get; }
    public CompostpileIngredient Inoculum { get; }

    public CompostpileProcess Process { get; }
    public CompostpileOutput Output { get; }
    public CompostpileHarvest Harvest { get; }

    public CompostpileInventory(
        CompostpileIngredient browns,
        CompostpileIngredient nutrition,
        CompostpileIngredient inoculum,
        CompostpileProcess process,
        CompostpileOutput output,
        CompostpileHarvest harvest
    )
    {
        Browns = browns;
        Nutrition = nutrition;
        Inoculum = inoculum;

        Process = process;
        Output = output;
        Harvest = harvest;
    }
    
    public static readonly CompostpileInventory Default = new(
        browns: new CompostpileIngredient(
            name: "browns",
            initQty: 16,
            placedBonusQty: 44,
            maxQty: 64 * 3,
            maxInput: 16,
            inPerCompostPortion: 16
        ),
        nutrition: new CompostpileIngredient(
            name: "nutrition",
            initQty: 16,
            placedBonusQty: 12,
            maxQty: 64,
            maxInput: 8,
            inPerCompostPortion: 8
        ),
        inoculum: new CompostpileIngredient(
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
        process: new CompostpileProcess(
            baseCompostRatePerHour: 0.33f,
            defaultMoisture01: 0.55f,
            optimalMoisture01: 0.60f,
            rainToMoisturePerDay: 0.40f,
            dryoutPerDayAt20C: 0.25f,
            greenhouseTempBonusC: 5f
        ),
        output: new CompostpileOutput(
            outputMaxQty: 48,
            outputOutPerCompostPortion: 1,
            inoculumOutPerSourPortion: 1
        ),
        harvest: new CompostpileHarvest(
            harvestMaxPerStack: 8
        )
    );
}

public sealed class CompostpileIngredient
{
    public string Name { get; }
    public int InitQty { get; }
    public int PlacedBonusQty { get; }
    public int MaxQty { get; }
    public int MaxInput { get; }
    public int InPerCompostPortion { get; }
    
    public int InPerSourAdded { get; }
    public int InPerRotAdded { get; }

    public CompostpileIngredient(
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

public sealed class CompostpileProcess
{
    public float BaseCompostRatePerHour { get; }

    public float DefaultMoisture01 { get; }
    public float OptimalMoisture01 { get; }
    public float RainToMoisturePerDay { get; }
    public float DryoutPerDayAt20C { get; }

    public float GreenhouseTempBonusC { get; }

    public CompostpileProcess(
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

public sealed class CompostpileOutput
{
    public int OutputMaxQty { get; }
    public int OutputOutPerCompostPortion { get; }

    public int InoculumOutPerSourPortion { get; }

    public CompostpileOutput(
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

public sealed class CompostpileHarvest
{
    public int HarvestMaxPerStack { get; }

    public CompostpileHarvest(int harvestMaxPerStack)
    {
        HarvestMaxPerStack = harvestMaxPerStack;
    }
}