using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace OddWire.GameContent;
public class BlockEntityCompostPile : BlockEntity
{
    private const int MAX_INPUT_BROWNS = 64 * 3;
    private const int MAX_INPUT_NUTRITION = 64;
    private const int MAX_INPUT_INOCULUM = 16;
    
    private const int SOUR_PER_INOCULUM = 2;
    private const int ROT_PER_INOCULUM = 4;
    
    private const int BROWNS_PER_COMPOST = 16;
    private const int NUTRITION_PER_COMPOST = 8;
    private const float BASE_COMPOST_CHANCE = 0.33f;
    
    private double _prevTimeComposted;
    
    private int _brownsQty;
    private int _inoculumQty;
    private Dictionary<EnumFoodCategory, int>? _nutritionStacks;
    public int NutritionQty
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

        _prevTimeComposted = Api.World.Calendar.TotalHours;
        
        _nutritionStacks = new();

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery3Seconds, 3000);
    }

    private void OnEvery3Seconds(float dt)
    {
        if (Api?.Side != EnumAppSide.Server
        ||  _nutritionStacks is null
        ||  _nutritionStacks.Count == 0
            )
            return;

        int possibleMax = Math.Min
            (_brownsQty / BROWNS_PER_COMPOST
            ,NutritionQty / NUTRITION_PER_COMPOST
            );

        if (possibleMax < 1)
            return;
        
        double totalHours = Api.World.Calendar.TotalHours;
        
        int transitions = (int)((totalHours - _prevTimeComposted) * GetCompostRate());
        if (transitions < 1)
            return;

        _prevTimeComposted = totalHours;
        
        _brownsQty -= transitions * BROWNS_PER_COMPOST;
        RemoveRandomNutrition(transitions * NUTRITION_PER_COMPOST);
        _inoculumQty += transitions;

        MarkDirty(true);
    }
    
    private float GetCompostRate()
    {
        if (_inoculumQty < 1)
            return 0f;
        
        float inoculumFactor = Math.Clamp((float)_inoculumQty / MAX_INPUT_INOCULUM, 0.1f, 1f);
        return BASE_COMPOST_CHANCE * inoculumFactor;
    }

    private void RemoveRandomNutrition(int amount)
    {
        if (amount <= 0
        || _nutritionStacks is null
        || _nutritionStacks.Count == 0
            )
            return;

        var keys = new List<EnumFoodCategory>(_nutritionStacks.Keys);
        var rand = Api.World.Rand;

        for (int i = 0; i < amount; i++)
        {
            int index = rand.Next(keys.Count);
            var key = keys[index];

            _nutritionStacks[key]--;
            if (_nutritionStacks[key] < 1)
            {
                _nutritionStacks.Remove(key);
                keys.RemoveAt(index);

                if (keys.Count < 1)
                    break;
            }
        }
    }


    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        if (slot.StackSize < 1)
            return false;

        if (TryAddNutrition(slot, out accepted)
        ||  TryAddBrowns(slot, out accepted)
        ||  TryAddInoculum(slot, out accepted)
            )
        {
            MarkDirty(true);
            return accepted > 0;
        }
        
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
        
        int room = MAX_INPUT_NUTRITION - NutritionQty;
        if(room < 1)
            return false;
        
        int ratio = 1;
        if (slot.MaxSlotStackSize != 64)
            ratio = Math.Max(64 / slot.MaxSlotStackSize, 1);
        
        if (slot.StackSize < ratio)
            return false;
                
        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = adjustedStackSize > room 
        ?   room
        :   adjustedStackSize;
        
        _nutritionStacks.TryGetValue(nutritionProps.FoodCategory, out var cur);
        _nutritionStacks[nutritionProps.FoodCategory] = cur + adjustedAccept;
        
        accepted = adjustedAccept * ratio;
        return true;
    }

    private bool TryAddBrowns(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        int room = MAX_INPUT_BROWNS - _brownsQty;
        if (room < 1
        ||  slot.Itemstack?.Item?.Code != "drygrass"
            )
            return false;
        
        accepted = slot.StackSize > room 
        ?   room
        :   slot.StackSize;

        _brownsQty += accepted;
        
        return true;
    }

    private bool TryAddInoculum(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        int room = MAX_INPUT_INOCULUM - _inoculumQty;
        if(room < 1)
            return false;

        string code = slot.Itemstack?.Item?.Code;
        if (code is null)
            return false;
        
        if(code == "compost")
        {
            accepted = slot.StackSize > room 
            ?   room
            :   slot.StackSize;

            _inoculumQty += accepted;
            return true;
        }

        if (code == "sourcompost"
        ||  code == "rot"
           )
        {
            int ratio = code == "sourcompost" ? SOUR_PER_INOCULUM : ROT_PER_INOCULUM;
            
            if (slot.StackSize < ratio)
                return false;
                
            int adjustedStackSize = slot.StackSize / ratio;
            int adjustedAccept = adjustedStackSize > room 
            ?   room
            :   adjustedStackSize;

            _inoculumQty += adjustedAccept;
            accepted = adjustedAccept * ratio;
            return true;
        }

        return false;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        _prevTimeComposted = tree.GetDouble("_prevTimeComposted");
        
        _brownsQty = tree.GetInt("_brownsQty");
        _inoculumQty = tree.GetInt("_inoculumQty");
        
        if(_nutritionStacks is null)
            _nutritionStacks = new();
        else
            _nutritionStacks.Clear();

        int nutritionLength = tree.GetInt("_nutritionStacks.Count");
        for (int i = 0; i < nutritionLength; i++)
            _nutritionStacks[(EnumFoodCategory)tree.GetInt($"_nutritionStacks<{i}>")] = tree.GetInt($"_nutritionStacks[{i}]");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetDouble("_prevTimeComposted", _prevTimeComposted);
        
        tree.SetInt("_brownsQty", _brownsQty);
        tree.SetInt("_inoculumQty", _inoculumQty);
        
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
