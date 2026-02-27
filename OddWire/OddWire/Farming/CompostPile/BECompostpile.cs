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
    private static readonly CompostpileInventory Model = CompostpileInventory.Default;

    private readonly CompostpileState state = new CompostpileState();

    public void UpdateShapeStackSize() => SetShapeStackSize(state.BrownsQty + state.NutritionQty + state.InoculumQty + state.OutputQty);
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
        => CompostpileSystem.CanHarvest(state, Model, out compostPileQty, out sourCompostQty, out compostQty);

    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        if (!CompostpileSystem.TryAdd(Api, Block, Pos, state, Model, slot, out accepted) || accepted < 1)
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

        state.BrownsQty = Math.Max(state.BrownsQty - Model.Browns.InitQty * qty, 0);
        RemoveRandomNutrition(Model.Nutrition.InitQty * qty);
        state.InoculumQty = Math.Max(state.InoculumQty - Model.Inoculum.InitQty * qty, 0);

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

        state.InoculumQty = Math.Max(state.InoculumQty - Model.Inoculum.InPerSourAdded * qty, 0);
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

        state.OutputQty = Math.Max(state.OutputQty - qty, 0);
        MarkDirty();
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (state.Moisture01 <= 0f && state.PrevTimeMoistureUpdated < 0)
            state.Moisture01 = Model.Process.DefaultMoisture01;

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery12Seconds, 12000);
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        CompostpileSystem.ResetQuantitiesOnPlaced(Block, state, Model);
        UpdateShapeStackSize();

        state.PrevTimeComposted = Api.World.Calendar.TotalHours;
    }

    private void OnEvery12Seconds(float dt)
    {
        if (Api?.Side != EnumAppSide.Server)
            return;

        double totalHours = Api.World.Calendar.TotalHours;

        CompostpileSystem.UpdateMoisture(Api, Pos, state, Model, totalHours);

        if (CompostpileSystem.ProcessCompost(Api, Block, Pos, state, Model, totalHours))
            MarkDirty(true);
    }

    private void RemoveRandomNutrition(int amount)
    {
        if (amount <= 0 || state.NutritionStacks.Count == 0)
            return;

        // Keep the same “nutrition mix randomness” concept, but do it safely (no chance of infinite loop).
        var keys = state.NutritionStacks.Keys.ToList();
        int nutritionRemaining = state.NutritionQty;

        int remaining = amount;
        while (remaining > 0 && keys.Count > 0 && nutritionRemaining > 0)
        {
            int index = Api.World.Rand.Next(keys.Count);
            var key = keys[index];

            int stackQty = state.NutritionStacks[key];
            if (stackQty <= 0)
            {
                state.NutritionStacks.Remove(key);
                keys.RemoveAt(index);
                continue;
            }

            int removeWeight = (int)Math.Ceiling(Api.World.Rand.NextSingle() * stackQty / nutritionRemaining);
            int maxRemove = Math.Min(removeWeight, remaining);
            if (maxRemove < 1) maxRemove = 1;

            int removeQty = Api.World.Rand.Next(maxRemove) + 1; // 1..maxRemove
            removeQty = Math.Min(removeQty, stackQty);

            state.NutritionStacks[key] -= removeQty;
            if (state.NutritionStacks[key] < 1)
            {
                state.NutritionStacks.Remove(key);
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

        float moisturePct = (float)Math.Round(state.Moisture01 * 100f, 0);
        string moistureColor = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, Math.Max(0, moisturePct))]);
        dsc.AppendLine(Lang.Get("Moisture: <font color=\"#{0}\">{1}%</font>", moistureColor, moisturePct));

        dsc.AppendLine();

        dsc.AppendLine(Lang.Get("Browns: {0}/{1}", state.BrownsQty, Model.Browns.MaxQty));
        dsc.AppendLine(Lang.Get("Nutrition: {0}/{1}", state.NutritionQty, Model.Nutrition.MaxQty));
        dsc.AppendLine(Lang.Get("Inoculum: {0}/{1}", state.InoculumQty, Model.Inoculum.MaxQty));
        dsc.AppendLine(Lang.Get("Compost: {0}/{1}", state.OutputQty, Model.Output.OutputMaxQty));

        if (state.NutritionStacks.Count > 0)
        {
            var parts = state.NutritionStacks
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                .ToArray();

            if (parts.Length > 0)
                dsc.AppendLine(Lang.Get("Nutrition mix: {0}", string.Join(", ", parts)));
        }

        int possibleMax = Math.Min(
            state.BrownsQty / Model.Browns.InPerCompostPortion,
            state.NutritionQty / Model.Nutrition.InPerCompostPortion
        );
        dsc.AppendLine(Lang.Get("Possible output right now: {0}", Math.Max(0, possibleMax)));

        dsc.AppendLine();

        float ratePerHour = Api?.World != null ? CompostpileSystem.GetCompostRatePerHour(Api, Block, Pos, state, Model, totalHours) : 0f;
        if (ratePerHour <= 0)
            ratePerHour = 0.00001f;

        dsc.AppendLine(Lang.Get("Compost time: {0:0.00}hr", 1f / ratePerHour));

        float nutr = CompostpileSystem.GetNutritionFactor01(Block, state, Model);
        dsc.AppendLine(Lang.Get(
            "Factors: Inoculum {0:0}% × Temp {1:0}% × Moisture {2:0}% × Nutrition {3:0}%",
            100f * CompostpileSystem.GetInoculumFactor01(Model, state.InoculumQty + state.OutputQty),
            100f * CompostpileSystem.GetTemperatureFactor01(envTemp),
            100f * CompostpileSystem.GetMoistureFactor01(Model, state.Moisture01),
            100f * nutr
        ));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        state.PrevTimeComposted = tree.GetDouble("_prevTimeComposted");
        state.PrevTimeMoistureUpdated = tree.GetDouble("_prevTimeMoistureUpdated");
        state.Moisture01 = tree.GetFloat("_moisture01", Model.Process.DefaultMoisture01);

        state.BrownsQty = tree.GetInt("_brownsQty");
        state.InoculumQty = tree.GetInt("_inoculumQty");
        state.OutputQty = tree.GetInt("_outputQty");

        state.NutritionStacks.Clear();
        int nutritionLength = tree.GetInt("_nutritionStacks.Count");
        for (int i = 0; i < nutritionLength; i++)
            state.NutritionStacks[(EnumFoodCategory)tree.GetInt($"_nutritionStacks<{i}>")] = tree.GetInt($"_nutritionStacks[{i}]");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetDouble("_prevTimeComposted", state.PrevTimeComposted);
        tree.SetDouble("_prevTimeMoistureUpdated", state.PrevTimeMoistureUpdated);
        tree.SetFloat("_moisture01", state.Moisture01);

        tree.SetInt("_brownsQty", state.BrownsQty);
        tree.SetInt("_inoculumQty", state.InoculumQty);
        tree.SetInt("_outputQty", state.OutputQty);

        tree.SetInt("_nutritionStacks.Count", state.NutritionStacks.Count);
        int i = 0;
        foreach (var stack in state.NutritionStacks)
        {
            tree.SetInt($"_nutritionStacks<{i}>", (int)stack.Key);
            tree.SetInt($"_nutritionStacks[{i}]", stack.Value);
            i++;
        }
    }
}