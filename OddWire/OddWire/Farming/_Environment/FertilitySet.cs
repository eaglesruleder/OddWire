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
    
    public readonly OrderedDictionary<string, float> Values;
    public readonly string[] Order;

    private FertilitySet()
    {
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
    ?   fertility
    :   0f;

    public static string? GetCode(Block? block) => _singleton._getCode(block);
    private string? _getCode(Block? block)
    {
        string? fertilityCode = block?.LastCodePart();
        return
            Contains(fertilityCode)
        ?   fertilityCode
        :   null;
    }
    #endregion

    #region Mutations
    public static string? StepCode(string? fertilityCode, int delta) => _singleton._stepCode(fertilityCode, delta);
    private string? _stepCode(string? fertilityCode, int delta)
    {
        int nextIndex = Index(fertilityCode) + delta;
        if (nextIndex < 0)
            return null;

        return Order[GameMath.Clamp(nextIndex, 0, Order.Length - 1)];
    }

    public static bool TryGetSteppedBlock
        (IWorldAccessor world
        ,Block block
        ,int delta
        ,out Block nextBlock
        ) => _singleton._tryGetSteppedBlock(world, block, delta, out nextBlock);

    private bool _tryGetSteppedBlock
        (IWorldAccessor world
        ,Block block
        ,int delta
        ,out Block nextBlock
        )
    {
        nextBlock = null;

        string? currentCode = GetCode(block);
        string? nextCode = StepCode(currentCode, delta);
        if (nextCode is null
        ||  nextCode == currentCode
        ||  block.Code is null
            )
            return false;

        string[] codeParts = block.Code.Path.Split('-');
        if (codeParts.Length == 0)
            return false;

        codeParts[^1] = nextCode;
        AssetLocation nextBlockCode = new(block.Code.Domain, string.Join("-", codeParts));
        nextBlock = world.GetBlock(nextBlockCode);
        return nextBlock is not null
            && nextBlock.Id != 0;
    }
    #endregion
}
