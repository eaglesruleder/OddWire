using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public class BlockEntityCompostPile : BlockEntity
{
    // “static final model = …” (C# equivalent)
    private static readonly CompostpileTuning Model = CompostpileTuningModels.Default;

    private readonly CompostpileData _d = new CompostpileData();

    public int NutritionQty => _d.NutritionQty;

    public void UpdateShapeStackSize() => SetShapeStackSize(_d.BrownsQty + _d.NutritionQty + _d.InoculumQty + _d.OutputQty);

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
        => CompostpileSystem.CanHarvest(_d, Model, out compostPileQty, out sourCompostQty, out compostQty);

    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        if (!CompostpileSystem.TryAdd(Api, Block, Pos, _d, Model, slot, out accepted) || accepted < 1)
            return false;

        UpdateShapeStackSize();
        MarkDirty(true);
        return true;
    }

    public void HarvestCompostPile(int qty, float dropQuantityMultiplier)
    {
        Block spawnBlock = Api.World.GetBlock(new AssetLocation("oddwire:compostpile-#1"));

        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, Model.Harvest.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        _d.BrownsQty = Math.Max(_d.BrownsQty - Model.Browns.InitQty * qty, 0);
        RemoveRandomNutrition(Model.Nutrition.InitQty * qty);
        _d.InoculumQty = Math.Max(_d.InoculumQty - Model.Inoculum.InitQty * qty, 0);

        MarkDirty();
    }

    public void HarvestSourCompost(int qty, float dropQuantityMultiplier)
    {
        Item spawnBlock = Api.World.GetItem(new AssetLocation("oddwire:sourcompost"));

        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, Model.Harvest.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        _d.InoculumQty = Math.Max(_d.InoculumQty - Model.Inoculum.InPerSourAdded * qty, 0);
        MarkDirty();
    }

    public void HarvestCompost(int qty, float dropQuantityMultiplier)
    {
        Item spawnItem = Api.World.GetItem(new AssetLocation("game:compost"));

        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, Model.Harvest.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        _d.OutputQty = Math.Max(_d.OutputQty - qty, 0);
        MarkDirty();
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (_d.Moisture01 <= 0f && _d.PrevTimeMoistureUpdated < 0)
            _d.Moisture01 = Model.Process.DefaultMoisture01;

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery12Seconds, 12000);
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        CompostpileSystem.ResetQuantitiesOnPlaced(Block, _d, Model);
        UpdateShapeStackSize();

        _d.PrevTimeComposted = Api.World.Calendar.TotalHours;
    }

    private void OnEvery12Seconds(float dt)
    {
        if (Api?.Side != EnumAppSide.Server)
            return;

        double totalHours = Api.World.Calendar.TotalHours;

        CompostpileSystem.UpdateMoisture(Api, Pos, _d, Model, totalHours);

        if (CompostpileSystem.ProcessCompost(Api, Block, Pos, _d, Model, totalHours))
            MarkDirty(true);
    }

    private void RemoveRandomNutrition(int amount)
    {
        if (amount <= 0 || _d.NutritionStacks.Count == 0)
            return;

        // Keep the same “nutrition mix randomness” concept, but do it safely (no chance of infinite loop).
        var keys = _d.NutritionStacks.Keys.ToList();
        int nutritionRemaining = _d.NutritionQty;

        int remaining = amount;
        while (remaining > 0 && keys.Count > 0 && nutritionRemaining > 0)
        {
            int index = Api.World.Rand.Next(keys.Count);
            var key = keys[index];

            int stackQty = _d.NutritionStacks[key];
            if (stackQty <= 0)
            {
                _d.NutritionStacks.Remove(key);
                keys.RemoveAt(index);
                continue;
            }

            int removeWeight = (int)Math.Ceiling(Api.World.Rand.NextSingle() * stackQty / nutritionRemaining);
            int maxRemove = Math.Min(removeWeight, remaining);
            if (maxRemove < 1) maxRemove = 1;

            int removeQty = Api.World.Rand.Next(maxRemove) + 1; // 1..maxRemove
            removeQty = Math.Min(removeQty, stackQty);

            _d.NutritionStacks[key] -= removeQty;
            if (_d.NutritionStacks[key] < 1)
            {
                _d.NutritionStacks.Remove(key);
                keys.RemoveAt(index);
            }

            nutritionRemaining -= removeQty;
            remaining -= removeQty;
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        double totalHours = Api?.World?.Calendar?.TotalHours ?? 0;

        bool skyExposed = Api?.World?.BlockAccessor != null && Api.World.BlockAccessor.IsSkyExposed(Pos);

        float envTemp = 0;
        bool inGreenhouse = false;
        if (Api?.World is not null)
            envTemp = Api.GetEnvironmentTemperatureC(Pos, totalHours, skyExposed, Model.Process.GreenhouseTempBonusC, out inGreenhouse);

        dsc.AppendLine(Lang.Get("Temperature: {0:0.#}°C", envTemp));
        if (inGreenhouse)
            dsc.AppendLine(Lang.Get("greenhousetempbonus"));

        float moisturePct = (float)Math.Round(_d.Moisture01 * 100f, 0);
        string moistureColor = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, Math.Max(0, moisturePct))]);
        dsc.AppendLine(Lang.Get("Moisture: <font color=\"#{0}\">{1}%</font>", moistureColor, moisturePct));

        dsc.AppendLine();

        dsc.AppendLine(Lang.Get("Browns: {0}/{1}", _d.BrownsQty, Model.Browns.MaxQty));
        dsc.AppendLine(Lang.Get("Nutrition: {0}/{1}", _d.NutritionQty, Model.Nutrition.MaxQty));
        dsc.AppendLine(Lang.Get("Inoculum: {0}/{1}", _d.InoculumQty, Model.Inoculum.MaxQty));
        dsc.AppendLine(Lang.Get("Compost: {0}/{1}", _d.OutputQty, Model.Output.OutputMaxQty));

        if (_d.NutritionStacks.Count > 0)
        {
            var parts = _d.NutritionStacks
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                .ToArray();

            if (parts.Length > 0)
                dsc.AppendLine(Lang.Get("Nutrition mix: {0}", string.Join(", ", parts)));
        }

        int possibleMax = Math.Min(
            _d.BrownsQty / Model.Browns.InPerCompostPortion,
            _d.NutritionQty / Model.Nutrition.InPerCompostPortion
        );
        dsc.AppendLine(Lang.Get("Possible output right now: {0}", Math.Max(0, possibleMax)));

        dsc.AppendLine();

        float ratePerHour = Api?.World != null ? CompostpileSystem.GetCompostRatePerHour(Api, Block, Pos, _d, Model, totalHours) : 0f;
        if (ratePerHour <= 0)
            ratePerHour = 0.00001f;

        dsc.AppendLine(Lang.Get("Compost time: {0:0.00}hr", 1f / ratePerHour));

        float nutr = CompostpileSystem.GetNutritionFactor01(Block, _d, Model);
        dsc.AppendLine(Lang.Get(
            "Factors: Inoculum {0:0}% × Temp {1:0}% × Moisture {2:0}% × Nutrition {3:0}%",
            100f * CompostpileSystem.GetInoculumFactor01(Model, _d.InoculumQty + _d.OutputQty),
            100f * CompostpileSystem.GetTemperatureFactor01(envTemp),
            100f * CompostpileSystem.GetMoistureFactor01(Model, _d.Moisture01),
            100f * nutr
        ));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        _d.PrevTimeComposted = tree.GetDouble("_prevTimeComposted");
        _d.PrevTimeMoistureUpdated = tree.GetDouble("_prevTimeMoistureUpdated");
        _d.Moisture01 = tree.GetFloat("_moisture01", Model.Process.DefaultMoisture01);

        _d.BrownsQty = tree.GetInt("_brownsQty");
        _d.InoculumQty = tree.GetInt("_inoculumQty");
        _d.OutputQty = tree.GetInt("_outputQty");

        _d.NutritionStacks.Clear();
        int nutritionLength = tree.GetInt("_nutritionStacks.Count");
        for (int i = 0; i < nutritionLength; i++)
            _d.NutritionStacks[(EnumFoodCategory)tree.GetInt($"_nutritionStacks<{i}>")] = tree.GetInt($"_nutritionStacks[{i}]");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetDouble("_prevTimeComposted", _d.PrevTimeComposted);
        tree.SetDouble("_prevTimeMoistureUpdated", _d.PrevTimeMoistureUpdated);
        tree.SetFloat("_moisture01", _d.Moisture01);

        tree.SetInt("_brownsQty", _d.BrownsQty);
        tree.SetInt("_inoculumQty", _d.InoculumQty);
        tree.SetInt("_outputQty", _d.OutputQty);

        tree.SetInt("_nutritionStacks.Count", _d.NutritionStacks.Count);
        int i = 0;
        foreach (var stack in _d.NutritionStacks)
        {
            tree.SetInt($"_nutritionStacks<{i}>", (int)stack.Key);
            tree.SetInt($"_nutritionStacks[{i}]", stack.Value);
            i++;
        }
    }
}