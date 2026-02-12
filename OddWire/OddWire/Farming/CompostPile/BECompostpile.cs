using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace OddWire.GameContent;
public class BlockEntityCompostPile : BlockEntity
{
    private const int MAX_STACK_SIZE = 64;
    
    private static int _nutritionTypes = -1;
    private static int NutritionTypes
    { get {
        if (_nutritionTypes < 0)
        {
            _nutritionTypes = 0;
            
            var nutritionTypeValues = Enum.GetValues(typeof(EnumFoodCategory));
            _nutritionTypes = nutritionTypeValues.Length;
            foreach(var nutritionTypeValue in nutritionTypeValues)
                if((int)nutritionTypeValue < 0)
                    _nutritionTypes--;
        }
        return _nutritionTypes;
    } }
    
    private int[]? _nutritionStacks;
    public int TotalQty
    { get {
        int result = 0;
        for(int i = 0; i < _nutritionStacks?.Length; i++)
            result += _nutritionStacks[i];
        return result;
    } }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        _nutritionStacks = new int[NutritionTypes];

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery3Seconds, 3000);
    }

    private void OnEvery3Seconds(float dt)
    {
        // Placeholder: later convert categories into compost/sour/rot etc.
        // Intentionally empty for now.
    }

    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        if (slot.StackSize < 1)
            return false;

        if (TryAddNutrition(slot, out accepted))
            return accepted > 0;
        
        return false;
    }

    private bool TryAddNutrition(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        
        var nutritionProps =  slot.Itemstack?.Collectible?.NutritionProps;
        if (_nutritionStacks is null
        ||  nutritionProps is null
            )
            return false;

        int nutritionKey = (int)nutritionProps.FoodCategory;
        if (nutritionKey < 0
        ||  nutritionKey >= _nutritionStacks.Length
            )
            return false;
        
        int room = MAX_STACK_SIZE - TotalQty;
        if(room < 0)
            return false;
        
        accepted = slot.StackSize > room ? room : slot.StackSize;
        _nutritionStacks[nutritionKey] += accepted;
        MarkDirty(true);
        return true;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        if(_nutritionStacks is null)
            _nutritionStacks = new int[NutritionTypes];
            
        for (int i = 0; i < NutritionTypes; i++)
            _nutritionStacks[i] = tree.GetInt($"_nutrition[{i}]");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        if(_nutritionStacks is not null)
            for (int i = 0; i < NutritionTypes; i++)
                tree.SetInt($"_nutrition[{i}]", _nutritionStacks[i]);
    }
}
