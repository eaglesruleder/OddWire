using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OddWire.System;

namespace OddWire.GameContent;

public sealed class FertilitySet
{
    private static readonly FertilitySet _singleton = new();
    
    public const string VeryLow = "verylow";
    public const string Low = "low";
    public const string Medium = "medium";
    public const string Compost = "compost";
    public const string High = "high";
    public const int Count = 5;
    
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

    public static float Value(int fertilityIndex) => _singleton.Values.GetValueAtIndex(fertilityIndex);
    public static float Value(Block? block) => _singleton[GetCode(block)];
    public static float Value(string? fertilityCode) => _singleton[fertilityCode];
    private float this[string? fertilityCode] =>
        fertilityCode is not null
    &&  Values.TryGetValue(fertilityCode, out float fertility)
    ?   fertility
    :   0;

    public static string? GetCode(Block? block) => _singleton._getCode(block);
    private string? _getCode(Block? block)
    {
        if (block?.Code is null)
            return null;
        
        string fertilityCode = block.LastCodePart();
        if (block.Code.PathStartsWith("soil"))
            fertilityCode = block.LastCodePart(1);
        
        return
            Contains(fertilityCode)
        ?   fertilityCode
        :   null;
    }
    #endregion

    #region Step
    public static string? StepCode(string? fertilityCode, int delta) => _singleton._stepCode(fertilityCode, delta);
    private string? _stepCode(string? fertilityCode, int delta)
    {
        int nextIndex = Index(fertilityCode) + delta;
        if (nextIndex < 0)
            return null;

        return Order[GameMath.Clamp(nextIndex, 0, Order.Length - 1)];
    }

    public static bool TryGetSteppedBlock(IWorldAccessor world, Block block, int delta, out Block nextBlock) =>
        _singleton._tryGetSteppedBlock(world, block, delta, out nextBlock);
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

        #region nextBlockCode = block.Code.Split('-').replace(nextCode, ^1)
        string[] codeParts = block.Code.Path.Split('-');
        if (codeParts.Length == 0)
            return false;
        
        codeParts[block.Code.PathStartsWith("soil") ? ^2 : ^1] = nextCode;
        AssetLocation nextBlockCode = new(block.Code.Domain, string.Join("-", codeParts));
        #endregion
        
        nextBlock = world.GetBlock(nextBlockCode);
        return
            nextBlock is not null
        &&  nextBlock.Id != 0;
    }
    #endregion
    
    public static float[] ResolveNutrients(Block block, BlockEntity? blockEntity)
    {
        if (blockEntity is BlockEntitySoilNutrition beNutrition)
        {
            float[] clone = new float[beNutrition.Nutrients.Length];
            for (int i = 0; i < beNutrition.Nutrients.Length; i++)
                clone[i] = beNutrition.Nutrients[i];
            return clone;
        }

        float fertility = Value(block);
        return new[] { fertility, fertility, fertility };
    }

    public static Block RevertSupportToSoil(IWorldAccessor world, BlockPos pos, Block block)
    {
        if (block is not (BlockFarmland or BlockPlowland))
            return block;

        BlockEntity? blockEntity = world.BlockAccessor.GetBlockEntity(pos);
        string? revertFertilityCode = GetCode(block);
        
        // Intent: if low NPK, chance to lose fertility 
        float nutrientAvg = ResolveNutrients(block, blockEntity).Avg();
        if (nutrientAvg < 100f
        &&  world.Rand.NextSingle() > nutrientAvg / 100f
           )
            revertFertilityCode = StepCode(revertFertilityCode, -1) ?? revertFertilityCode;

        if (revertFertilityCode is null)
            return block;

        AssetLocation soilCode = new("game", $"soil-{revertFertilityCode}");
        Block soilBlock = world.GetBlock(soilCode);
        if (soilBlock?.Id > 0)
        {
            world.BlockAccessor.ExchangeBlock(soilBlock.BlockId, pos);
            return soilBlock;
        }
        return block;
    }
}
