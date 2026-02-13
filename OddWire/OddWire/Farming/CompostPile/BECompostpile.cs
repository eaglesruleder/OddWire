using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
    
    private const float DEFAULT_MOISTURE = 0.55f;
    private const float OPTIMAL_MOISTURE = 0.60f;
    private const float RAIN_TO_MOISTURE_PER_DAY = 0.40f; 
    private const float DRY_OUT_PER_DAY_AT_20C = 0.25f; 
    private const float GREENHOUSE_TEMP_BONUS = 5f;
    
    private double _prevTimeComposted = -1;
    private double _prevTimeMoistureUpdated = -1;
    
    private float _moisture01 = DEFAULT_MOISTURE;
    
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
    
    private static float GetTemperatureFactor(float tempC)
    {
        if (tempC <  0) return 0.05f;
        if (tempC < 10) return GameMath.Lerp(0.05f, 0.6f, (tempC - 0f) / 10f);
        if (tempC < 20) return GameMath.Lerp(0.6f, 1.0f, (tempC - 10f) / 10f);
        if (tempC < 55) return 1.0f;
        if (tempC < 70) return GameMath.Lerp(1.0f, 0.35f, (tempC - 55f) / 15f);
        return 0.10f;
    }

    private static float GetMoistureFactor(float moisture01)
    {
        if (moisture01 <= 0.05f)
            return 0.05f;

        float factor = moisture01 <= OPTIMAL_MOISTURE
        ?   GameMath.Lerp(0.1f, 1.0f, (moisture01 - 0.05f) / (OPTIMAL_MOISTURE - 0.05f))
        :   GameMath.Lerp(1.0f, 0.25f, (moisture01 - OPTIMAL_MOISTURE) / (1f - OPTIMAL_MOISTURE));

        if (moisture01 > 0.9f)
            factor *= 0.6f;

        return Math.Clamp(factor, 0.05f, 1.0f);
    }
    
    private float GetEnvTemperature(double totalHours, bool skyExposed, out bool inGreenhouse)
    {
        inGreenhouse = false;
        
        ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHours / Api.World.Calendar.HoursPerDay);
        float temp = conds?.Temperature ?? 0;
        
        if (!skyExposed)
        {
            var room = Api.ModLoader.GetModSystem<RoomRegistry>()?.GetRoomForPosition(Pos.UpCopy());
            if (room != null
                &&  room.SkylightCount > room.NonSkylightCount
                &&  room.ExitCount == 0
               )
            {
                inGreenhouse = true;
                temp += GREENHOUSE_TEMP_BONUS;
            }
        }

        return temp;
    }
    
    
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
        if (Api?.Side != EnumAppSide.Server)
            return;

        double totalHours = Api.World.Calendar.TotalHours;
        UpdateMoisture(totalHours);
        ProcessCompost(totalHours);
    }

    private void UpdateMoisture(double totalHours)
    {
        if (_prevTimeMoistureUpdated < 0)
            _prevTimeMoistureUpdated = totalHours;

        double dtHours = totalHours - _prevTimeMoistureUpdated;
        if (dtHours <= 0.1)
            return;

        // Don't simulate absurdly large jumps, cap at 2 weeks
        dtHours = Math.Min(dtHours, 24 * 14);

        ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHours / Api.World.Calendar.HoursPerDay);
        float rainfall = Math.Clamp(conds?.Rainfall ?? 0, 0f, 1f);
        
        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y;
        if (skyExposed)
        {
            float wetGain = rainfall * (float)(dtHours / 24) * RAIN_TO_MOISTURE_PER_DAY;
            _moisture01 = Math.Clamp(_moisture01 + wetGain, 0f, 1f);
        }

        float envTemp = GetEnvTemperature(totalHours, skyExposed, out bool inGreenhouse);
        float tempDryMultiplier = Math.Clamp(0.5f + envTemp / 40f, 0.2f, 2.0f);
        float shelterMultiplier = (skyExposed ? 1.0f : 0.75f) * (inGreenhouse ? 0.85f : 1.0f);

        float dryLoss = (float)(dtHours / 24.0) * DRY_OUT_PER_DAY_AT_20C * tempDryMultiplier * shelterMultiplier;
        _moisture01 = Math.Clamp(_moisture01 - dryLoss, 0f, 1f);

        _prevTimeMoistureUpdated = totalHours;
    }
    
    private float GetCompostRate(double totalHours)
    {
        if (_inoculumQty < 1)
            return 0f;

        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y;

        float envTemp = GetEnvTemperature(totalHours, skyExposed, out _);
        float tempFactor = GetTemperatureFactor(envTemp);
        float moistureFactor = GetMoistureFactor(_moisture01);
        float inoculumFactor = Math.Clamp((float)_inoculumQty / MAX_INPUT_INOCULUM, 0.1f, 1f);

        return BASE_COMPOST_CHANCE * inoculumFactor * tempFactor * moistureFactor;
    }

    private void ProcessCompost(double totalHours)
    {
        if (_nutritionStacks is null
        ||  _nutritionStacks.Count == 0
            )
            return;

        int possibleMax = Math.Min
            (_brownsQty / BROWNS_PER_COMPOST
            ,NutritionQty / NUTRITION_PER_COMPOST
            );

        if (possibleMax < 1)
            return;
        
        int transitions = (int)((totalHours - _prevTimeComposted) * GetCompostRate(totalHours));
        if (transitions < 1)
            return;

        transitions = Math.Min(transitions, possibleMax);

        _prevTimeComposted = totalHours;
        
        _brownsQty -= transitions * BROWNS_PER_COMPOST;
        RemoveRandomNutrition(transitions * NUTRITION_PER_COMPOST);
        _inoculumQty += transitions;

        MarkDirty(true);
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
        _prevTimeMoistureUpdated = tree.GetDouble("_prevTimeMoistureUpdated");
        _moisture01 = tree.GetFloat("_moisture01", DEFAULT_MOISTURE);

        
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
        tree.SetDouble("_prevTimeMoistureUpdated", _prevTimeMoistureUpdated);
        tree.SetFloat("_moisture01", _moisture01);

        
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
