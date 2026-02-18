using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;
public class BlockEntityCompostPile : BlockEntity
{
    private const int BROWNS_INIT = 16;
    private const int BROWNS_PLACED_BONUS = 44;
    private const int BROWNS_MAXQTY = 64 * 3;
    private const int BROWNS_MAXINPUT = 16;
    private const int BROWNS_PER_COMPOST = 16;
    
    private const int NUTRITION_INIT = 16;
    private const int NUTRITION_PLACED_BONUS = 12;
    private const int NUTRITION_MAXQTY = 64;
    private const int NUTRITION_MAXINPUT = 8;
    private const int NUTRITION_PER_COMPOST = 8;
    
    private const int INOCULUM_INIT = 2;
    private const int INOCULUM_PLACED_BONUS = 8;
    private const int INOCULUM_MAXQTY = 16;
    private const int INOCULUM_MAXINPUT = 4;
    private const int INOCULUM_PER_COMPOST = 1;
    
    private const int SOUR_PER_INOCULUM = 2;
    private const int ROT_PER_INOCULUM = 4;
    
    private const int OUTPUT_MAXQTY = 48;
    
    private const float BASE_COMPOST_RATE = 0.33f;
    private const float DEFAULT_MOISTURE = 0.55f;
    private const float OPTIMAL_MOISTURE = 0.60f;
    private const float RAIN_TO_MOISTURE_PER_DAY = 0.40f; 
    private const float DRY_OUT_PER_DAY_AT_20C = 0.25f; 
    private const float GREENHOUSE_TEMP_BONUS = 5f;

    private const int HARVEST_MAX_PER_STACK = 8;
    
    
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
    private int _outputQty;

    
    private static float GetInoculumFactor(int inoculumQty) =>
        Math.Clamp((float)inoculumQty / INOCULUM_MAXQTY, 0.1f, 1f);
    
    private static float GetTemperatureFactor(float tempC)
    {
        if (tempC <  0) return 0.05f;
        if (tempC < 10) return GameMath.Lerp(0.05f, 0.6f, (tempC - 0f) / 10f);
        if (tempC < 20) return GameMath.Lerp(0.6f, 1.0f, (tempC - 10f) / 10f);
        if (tempC < 55) return 1.0f;
        if (tempC < 70) return GameMath.Lerp(1.0f, 0.35f, (tempC - 55f) / 15f);
        return 0.10f;
    }

    private float GetNutritionFactor()
    {
        if (_nutritionStacks is null
        ||  _nutritionStacks?.Count < 1
            )
            return 0;

        JsonObject? speedByCat = Block.Attributes?["nutritionSpeedByCategory"];

        float weighted = 0f;
        foreach (var kvp in _nutritionStacks)
            weighted += (speedByCat?[kvp.Key.ToString()]?.AsFloat(1f) ?? 1f) * kvp.Value;
        return weighted / NUTRITION_MAXQTY;
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

    public void UpdateShapeStackSize() => SetShapeStackSize(_brownsQty + NutritionQty + _inoculumQty + _outputQty);
    public void SetShapeStackSize(int stackSize)
    {
        if (Api.Side != EnumAppSide.Server)
            return;

        int variantSize = Math.Clamp((int)Math.Ceiling((float)stackSize / 64), 1, 5);
        AssetLocation loc = Block.CodeWithVariant("size", $"#{variantSize:0}");
        Block block = Api.World.GetBlock(loc);
        if (block == null)
            return;

        Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
        Block = block;
    }
    
    
    public bool CanHarvest(out int compostPileQty, out int sourCompostQty, out int compostQty)
    {
        int bulkPortions = Math.Min(_brownsQty / BROWNS_INIT, NutritionQty / NUTRITION_INIT);
        compostPileQty = Math.Min(bulkPortions, _inoculumQty / INOCULUM_INIT);
        sourCompostQty = Math.Max(_inoculumQty - bulkPortions * INOCULUM_INIT, 0);
        compostQty = _outputQty;
        return
            compostPileQty > 0
        ||  sourCompostQty > 0
        ||  compostQty > 0;
    }
    
    public void HarvestCompostPile(int qty, float dropQuantityMultiplier)
    {
        Block spawnBlock = Api.World.GetBlock(new AssetLocation("oddwire:compostpile-#1"));
        
        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, HARVEST_MAX_PER_STACK))+1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }
        
        _brownsQty = Math.Max(_brownsQty - BROWNS_INIT * qty, 0);
        RemoveRandomNutrition(NUTRITION_INIT * qty);
        _inoculumQty = Math.Max(_inoculumQty - INOCULUM_INIT * qty, 0);
        MarkDirty();
    }
    
    public void HarvestSourCompost(int qty, float dropQuantityMultiplier)
    {
        Item spawnBlock = Api.World.GetItem(new AssetLocation("oddwire:sourcompost"));
        
        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, HARVEST_MAX_PER_STACK))+1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }
        
        _inoculumQty = Math.Max(_inoculumQty - SOUR_PER_INOCULUM * qty, 0);
        MarkDirty();
    }

    public void HarvestCompost(int qty, float dropQuantityMultiplier)
    {
        Item spawnItem = Api.World.GetItem(new AssetLocation("game:compost"));
        
        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, HARVEST_MAX_PER_STACK))+1;
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }
        
        _outputQty = Math.Max(_outputQty - qty, 0);
        MarkDirty();
    }
    
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        _nutritionStacks ??= new();

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery3Seconds, 3000);
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        ResetQuantities();
        UpdateShapeStackSize();
        
        _prevTimeComposted = Api.World.Calendar.TotalHours;
    }

    private void ResetQuantities()
    {
        int.TryParse(Block.LastCodePart().Substring(1), out int stackBonus);
        stackBonus--;
        if(stackBonus < 1)
            stackBonus = 0;
        
        _brownsQty = BROWNS_INIT + stackBonus * BROWNS_PLACED_BONUS;
        _nutritionStacks ??= new Dictionary<EnumFoodCategory, int>();
        _nutritionStacks.Clear();
        _nutritionStacks[EnumFoodCategory.Unknown] = NUTRITION_INIT + stackBonus * NUTRITION_PLACED_BONUS;
        _inoculumQty = INOCULUM_INIT + stackBonus * INOCULUM_PLACED_BONUS;
        _outputQty = 0;
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
            UpdateShapeStackSize();
            MarkDirty(true);
            return accepted > 0;
        }
        
        return false;
    }

    private bool TryAddNutrition(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        var stackCollectible = slot.Itemstack?.Collectible;
        var nutritionProps = stackCollectible?.NutritionProps;
        if (_nutritionStacks is null
        ||  nutritionProps is null
            )
            return false;
        
        int room = NUTRITION_MAXQTY - NutritionQty;
        if(room < 1)
            return false;
        
        int ratio = 1;
        if (stackCollectible != null
        &&  stackCollectible.MaxStackSize != 64
            )
            ratio = Math.Max(64 / stackCollectible.MaxStackSize, 1);
        
        if (slot.StackSize < ratio)
            return false;
                
        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, NUTRITION_MAXINPUT);
        
        _nutritionStacks.TryGetValue(nutritionProps.FoodCategory, out var cur);
        _nutritionStacks[nutritionProps.FoodCategory] = cur + adjustedAccept;
        
        accepted = adjustedAccept * ratio;
        return true;
    }

    private bool TryAddBrowns(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        int room = BROWNS_MAXQTY - _brownsQty;
        if (room < 1
        ||  slot.Itemstack?.Item?.Code.ToString() != "game:drygrass"
            )
            return false;
        
        accepted = Math.Min(slot.StackSize > room ? room : slot.StackSize, BROWNS_MAXINPUT);

        _brownsQty += accepted;
        
        return true;
    }

    private bool TryAddInoculum(ItemSlot slot, out int accepted)
    {
        accepted = 0;
        int room = INOCULUM_MAXQTY - _inoculumQty;
        if(room < 1)
            return false;

        string? code = slot.Itemstack?.Item?.Code.ToString();
        int ratio = code switch
            {"game:compost" => 1
            ,"oddwire:sourcompost" => SOUR_PER_INOCULUM
            ,"game:rot" => ROT_PER_INOCULUM
            ,_ => 0
            };
            
        if (ratio < 1
        ||  slot.StackSize < ratio
            )
            return false;
                
        int adjustedStackSize = slot.StackSize / ratio;
        int adjustedAccept = Math.Min(adjustedStackSize > room ? room : adjustedStackSize, INOCULUM_MAXINPUT);

        _inoculumQty += adjustedAccept;
        accepted = adjustedAccept * ratio;
        return true;
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

        float dtDays = (float)Math.Min((totalHours - _prevTimeMoistureUpdated)/24, 14);
        
        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y;
        if (skyExposed)
        {
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHours / Api.World.Calendar.HoursPerDay);
            float wetGain = Math.Clamp(conds?.Rainfall ?? 0, 0f, 1f) * dtDays * RAIN_TO_MOISTURE_PER_DAY;
            _moisture01 = Math.Clamp(_moisture01 + wetGain, 0f, 1f);
        }

        float envTemp = GetEnvTemperature(totalHours, skyExposed, out bool inGreenhouse);
        float tempDryMultiplier = Math.Clamp(0.5f + envTemp / 40f, 0.2f, 2.0f);
        float shelterMultiplier = (skyExposed ? 1.0f : 0.75f) * (inGreenhouse ? 0.85f : 1.0f);

        float dryLoss = dtDays * DRY_OUT_PER_DAY_AT_20C * tempDryMultiplier * shelterMultiplier;
        _moisture01 = Math.Clamp(_moisture01 - dryLoss, 0f, 1f);

        _prevTimeMoistureUpdated = totalHours;
    }
    
    private float GetCompostRate(double totalHours)
    {
        if (_inoculumQty < 1
        &&  _outputQty < 1
            )
            return 0f;

        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y;
        float envTemp = GetEnvTemperature(totalHours, skyExposed, out _);
        return
            BASE_COMPOST_RATE
        *   GetInoculumFactor(_inoculumQty + _outputQty)
        *   GetTemperatureFactor(envTemp)
        *   GetMoistureFactor(_moisture01)
        *   GetNutritionFactor();
    }

    private void ProcessCompost(double totalHours)
    {
        double timePassed = totalHours - _prevTimeComposted;
        if (_nutritionStacks is null
        ||  _nutritionStacks.Count == 0
        ||  timePassed < 1
            )
            return;
        
        _prevTimeComposted = totalHours;
        
        int room = OUTPUT_MAXQTY - _outputQty;
        int available = Math.Min(Math.Min
            (_brownsQty / BROWNS_PER_COMPOST
            ,NutritionQty / NUTRITION_PER_COMPOST
           ),_inoculumQty / INOCULUM_PER_COMPOST
            );
        if (available < 1
        ||  room < 1
            )
            return;

        int transitions = (int)Math.Min(timePassed * GetCompostRate(totalHours), Math.Min(room,available));
        if (transitions < 1)
            return;
        
        _brownsQty -= transitions * BROWNS_PER_COMPOST;
        RemoveRandomNutrition(transitions * NUTRITION_PER_COMPOST);
        
        // Handle failchance here
        _inoculumQty -= transitions * INOCULUM_PER_COMPOST;
        _outputQty += transitions;
        
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
        int nutritionRemaining = NutritionQty;
        
        int remaining = amount;
        while (remaining > 0)
        {
            int index = Api.World.Rand.Next(keys.Count);
            var key = keys[index];

            int removeWeight = (int)Math.Ceiling(Api.World.Rand.NextSingle() * _nutritionStacks[key] / nutritionRemaining);
            int removeQty = Api.World.Rand.Next(Math.Min(removeWeight, remaining)+1);
            
            _nutritionStacks[key] -= removeQty;
            if (_nutritionStacks[key] < 1)
            {
                _nutritionStacks.Remove(key);
                keys.RemoveAt(index);

                if (keys.Count < 1)
                    break;
            }
            nutritionRemaining -= removeQty;
            remaining -= removeQty;
        }
    }
    
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        double totalHours = Api?.World?.Calendar?.TotalHours ?? 0;
        bool skyExposed = Api?.World?.BlockAccessor != null && Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y;

        float envTemp = 0;
        bool inGreenhouse = false;
        if (Api?.World is not null)
            envTemp = GetEnvTemperature(totalHours, skyExposed, out inGreenhouse);

        dsc.AppendLine(Lang.Get("Temperature: {0:0.#}°C", envTemp));
        if (inGreenhouse)
            dsc.AppendLine(Lang.Get("greenhousetempbonus"));

        float moisturePct = (float)Math.Round(_moisture01 * 100f, 0);
        string moistureColor = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, Math.Max(0, moisturePct))]);
        dsc.AppendLine(Lang.Get("Moisture: <font color=\"#{0}\">{1}%</font>", moistureColor, moisturePct));

        dsc.AppendLine();

        dsc.AppendLine(Lang.Get("Browns: {0}/{1}", _brownsQty, BROWNS_MAXQTY));
        dsc.AppendLine(Lang.Get("Nutrition: {0}/{1}", NutritionQty, NUTRITION_MAXQTY));
        dsc.AppendLine(Lang.Get("Inoculum: {0}/{1}", _inoculumQty, INOCULUM_MAXQTY));
        dsc.AppendLine(Lang.Get("Compost: {0}/{1}", _outputQty, OUTPUT_MAXQTY));

        // Consider removing
        if (_nutritionStacks?.Count > 0)
        {
            var parts = _nutritionStacks
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                .ToArray();

            if (parts.Length > 0)
                dsc.AppendLine(Lang.Get("Nutrition mix: {0}", string.Join(", ", parts)));
        }

        int possibleMax = Math.Min(
            _brownsQty / BROWNS_PER_COMPOST,
            NutritionQty / NUTRITION_PER_COMPOST
            );
        dsc.AppendLine(Lang.Get("Possible output right now: {0}", Math.Max(0, possibleMax)));

        dsc.AppendLine();

        float ratePerHour = Api?.World != null ? GetCompostRate(totalHours) : 0f;
        if (ratePerHour <= 0)
            ratePerHour = 0.00001f;
        
        dsc.AppendLine(Lang.Get("Compost time: {0:0.00}hr", 1f/ratePerHour));
        dsc.AppendLine(Lang.Get("Factors: Inoculum {0:0}% × Temp {1:0}% × Moisture {2:0}% × Nutrition {3:0}%"
            ,100f*GetInoculumFactor(_inoculumQty + _outputQty), 100f*GetTemperatureFactor(envTemp), 100f*GetMoistureFactor(_moisture01), 100f*GetNutritionFactor())
            );
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        _prevTimeComposted = tree.GetDouble("_prevTimeComposted");
        _prevTimeMoistureUpdated = tree.GetDouble("_prevTimeMoistureUpdated");
        _moisture01 = tree.GetFloat("_moisture01", DEFAULT_MOISTURE);
        
        _brownsQty = tree.GetInt("_brownsQty");
        _inoculumQty = tree.GetInt("_inoculumQty");
        _outputQty = tree.GetInt("_outputQty");
        
        _nutritionStacks ??= new();
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
        tree.SetInt("_outputQty", _outputQty);
        
        tree.SetInt("_nutritionStacks.Count", _nutritionStacks?.Count ?? 0);
        if (_nutritionStacks is not null)
        {
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
