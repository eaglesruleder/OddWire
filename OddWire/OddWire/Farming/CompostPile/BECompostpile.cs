using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace OddWire.GameContent;
public class BlockEntityCompostPile : BlockEntity
{
    private const int MAX_STACK_SIZE = 64;
    
    private Dictionary<EnumFoodCategory, int>? _nutritionStacks;
    public int TotalQty
    { get {
        if (_nutritionStacks is null)
            return 0;
        
        int result = 0;
        foreach(var kvp in _nutritionStacks)
            result += kvp.Value;
        return result;
    } }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        _nutritionStacks = new();

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
        
        int room = MAX_STACK_SIZE - TotalQty;
        if(room < 1)
            return false;

        int actualStackSize = slot.StackSize;
        int adjustedStackSize = actualStackSize;
        int adjustedStackRatio = 1;
        if (slot.MaxSlotStackSize != 64)
        {
            adjustedStackRatio = 64 / slot.MaxSlotStackSize;
            adjustedStackSize = actualStackSize * adjustedStackRatio;
        }

        while
           (actualStackSize > 0 
        &&  adjustedStackSize > room
            )
        {
            actualStackSize--;
            adjustedStackSize -= adjustedStackRatio;
        }
        
        if(actualStackSize < 1)
            return false;
        
        _nutritionStacks.TryGetValue(nutritionProps.FoodCategory, out var cur);
        _nutritionStacks[nutritionProps.FoodCategory] = cur + adjustedStackSize;
        MarkDirty(true);
        
        accepted = actualStackSize;
        return true;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        if(_nutritionStacks is null)
            _nutritionStacks = new();

        int nutritionLength = tree.GetInt("_nutritionStacks.Count");
        for (int i = 0; i < nutritionLength; i++)
            _nutritionStacks.Add((EnumFoodCategory)tree.GetInt($"_nutritionStacks<{i}>"),tree.GetInt($"_nutritionStacks[{i}]"));
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        if (_nutritionStacks is not null)
        {
            tree.SetInt("_nutritionStacks.Count", _nutritionStacks.Count);
            int i = 0;
            foreach (var stack in _nutritionStacks)
            {
                tree.SetInt($"_nutritionStacks<{i}>", (int)stack.Key);
                tree.SetInt($"_nutritionStacks[{i}]", stack.Value);
                i++;
            }
        }
    }
}
