using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class FertilitySet
{
    private static readonly FertilitySet _singleton = new();
    
    public const string VeryLow = "verylow";
    public const string Low = "low";
    public const string Medium = "medium";
    public const string Compost = "compost";
    public const string High = "high";

    public readonly string Default;
    public readonly OrderedDictionary<string, float> Values;
    public readonly string[] Order;

    private FertilitySet()
    {
        Default = Low;
        Values = BlockEntityFarmland.Fertilities;
        Order =
            [VeryLow
            ,Low
            ,Medium
            ,Compost
            ,High
            ];
    }

    #region Queries
    public static bool Contains(string? fertilityCode) => _singleton._contains(fertilityCode);
    private bool _contains(string? fertilityCode) =>
        fertilityCode is not null
    &&  Values.ContainsKey(fertilityCode);
    
    public static int Index(Block? block) => _singleton._index(GetCode(block));
    public static int Index(string? code) => _singleton._index(code);
    private int _index(string? code)
    {
        if (code is null)
            return -1;
        
        for (int i = 0; i < Order.Length; i++)
            if (Order[i] == code)
                return i;
        return -1;
    }
    
    
    public static float Value(Block? block) => _singleton[GetCode(block)];
    public static float Value(string? fertilityCode) => _singleton[fertilityCode];
    private float this[string? fertilityCode] =>
        fertilityCode is not null
    &&  Values.TryGetValue(fertilityCode, out float fertility)
    ?   fertility : Values[Default];

    public static string? GetCode(Block? block) => _singleton[block];
    private string? this[Block? block]
    { get {
        string? fertilityCode = block?.LastCodePart();
        return
            Contains(fertilityCode)
        ?   fertilityCode
        :   Default;
    } }
    #endregion

    #region Mutations
    public static string StepCode(string? fertilityCode, int delta) => _singleton._stepCode(fertilityCode, delta);
    private string _stepCode(string? fertilityCode, int delta)
    {
        string currentCode = Contains(fertilityCode) ? fertilityCode! : Default;

        int index = 0;
        for (int i = 0; i < Order.Length; i++)
            if (Order[i] == currentCode)
                { index = i; break; }
        
        return Order[GameMath.Clamp(index + delta, 0, Order.Length - 1)];
    }

    public static float[] MakeUniformNutrients(string? fertilityCode) => _singleton._makeUniformNutrients(fertilityCode);
    private float[] _makeUniformNutrients(string? fertilityCode)
    {
        float fertility = this[fertilityCode];
        return new[] { fertility, fertility, fertility };
    }
    #endregion
}
