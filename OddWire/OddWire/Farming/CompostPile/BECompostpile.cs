using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace OddWire.GameContent;

public class BlockEntityCompostpile : BlockEntity
{
    private CompostpileSettings Settings = CompostpileSettings.Default;
    
    private readonly CompostpileInventory _inventory = new();

    public void UpdateShapeStackSize() => SetShapeStackSize(_inventory.BrownsQty + _inventory.NutritionQty + _inventory.InoculumQty + _inventory.OutputQty);
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

    public bool CanHarvest(out int CompostpileQty, out int sourCompostQty, out int compostQty)
        => _inventory.CanHarvest(out CompostpileQty, out sourCompostQty, out compostQty);

    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        if (!_inventory.TryAdd(Api, slot, out accepted) || accepted < 1)
            return false;

        UpdateShapeStackSize();
        MarkDirty(true);
        return true;
    }

    public void HarvestCompostpile(int qty, float dropQuantityMultiplier)
    {
        Block spawnBlock = Api.World.GetBlock(new AssetLocation("oddwire:Compostpile-#1"));

        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        _inventory.BrownsQty = Math.Max(_inventory.BrownsQty - Settings.Browns.InitQty * qty, 0);
        _inventory.TryRemoveRandomNutrition(Api.World.Rand, Settings.Nutrition.InitQty * qty);
        _inventory.InoculumQty = Math.Max(_inventory.InoculumQty - Settings.Inoculum.InitQty * qty, 0);

        MarkDirty();
    }

    public void HarvestSourCompost(int qty, float dropQuantityMultiplier)
    {
        Item spawnBlock = Api.World.GetItem(new AssetLocation("oddwire:sourcompost"));

        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnBlock, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        _inventory.InoculumQty = Math.Max(_inventory.InoculumQty - Settings.Inoculum.InPerSourPortion * qty, 0);
        MarkDirty();
    }

    public void HarvestCompost(int qty, float dropQuantityMultiplier)
    {
        Item spawnItem = Api.World.GetItem(new AssetLocation("game:compost"));

        int remaining = (int)(qty * dropQuantityMultiplier);
        while (remaining > 0)
        {
            int spawnNow = Api.World.Rand.Next(Math.Min(remaining, Settings.HarvestMaxPerStack)) + 1;
            ItemStack stack = new ItemStack(spawnItem, spawnNow);
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(Api.World.Rand.NextDouble(), 0.5, Api.World.Rand.NextDouble()));
            remaining -= spawnNow;
        }

        _inventory.OutputQty = Math.Max(_inventory.OutputQty - qty, 0);
        MarkDirty();
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (_inventory.Moisture01 <= 0f
        &&  _inventory.PrevTimeMoistureUpdated < 0
            )
            _inventory.Moisture01 = Settings.DefaultMoisture01;

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery12Seconds, 12000);
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        _inventory.ResetOnPlaced(Block);
        _inventory.PrevTimeComposted = Api.World.Calendar.TotalHours;
        
        UpdateShapeStackSize();
    }

    private void OnEvery12Seconds(float dt)
    {
        if (Api?.Side == EnumAppSide.Server
        &&  _inventory.Update(this, Api.World.Calendar.TotalHours)
            )
            MarkDirty(true);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        double totalHours = Api?.World?.Calendar?.TotalHours ?? 0;

        bool skyExposed = Api?.World?.BlockAccessor != null && Api.World.BlockAccessor.IsSkyExposed(Pos);

        float envTemp = 0;
        bool inGreenhouse = false;
        if (Api?.World is not null)
            envTemp = Api.GetEnvironmentTemperatureC(Pos, totalHours, skyExposed, Settings.GreenhouseTempBonusC, out inGreenhouse);

        dsc.AppendLine(Lang.Get("Temperature: {0:0.#}°C", envTemp));
        if (inGreenhouse)
            dsc.AppendLine(Lang.Get("greenhousetempbonus"));

        float moisturePct = (float)Math.Round(_inventory.Moisture01 * 100f, 0);
        string moistureColor = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, Math.Max(0, moisturePct))]);
        dsc.AppendLine(Lang.Get("Moisture: <font color=\"#{0}\">{1}%</font>", moistureColor, moisturePct));

        dsc.AppendLine();

        dsc.AppendLine(Lang.Get("Browns: {0}/{1}", _inventory.BrownsQty, Settings.Browns.MaxQty));
        dsc.AppendLine(Lang.Get("Nutrition: {0}/{1}", _inventory.NutritionQty, Settings.Nutrition.MaxQty));
        dsc.AppendLine(Lang.Get("Inoculum: {0}/{1}", _inventory.InoculumQty, Settings.Inoculum.MaxQty));
        dsc.AppendLine(Lang.Get("Compost: {0}/{1}", _inventory.OutputQty, Settings.OutputMaxQty));

        if (_inventory.NutritionStacks.Count > 0)
        {
            var parts = _inventory.NutritionStacks
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                .ToArray();

            if (parts.Length > 0)
                dsc.AppendLine(Lang.Get("Nutrition mix: {0}", string.Join(", ", parts)));
        }

        int possibleMax = (int)(
            (float)_inventory.BrownsQty / Settings.Browns.InPerCompostPortion
        +   (float)_inventory.NutritionQty / Settings.Nutrition.InPerCompostPortion
            );
        dsc.AppendLine(Lang.Get("Possible output right now: {0}", Math.Max(0, possibleMax)));

        dsc.AppendLine();

        float ratePerHour = Api?.World != null ? _inventory.GetCompostRatePerHour() : 0f;
        if (ratePerHour <= 0)
            ratePerHour = 0.00001f;

        dsc.AppendLine(Lang.Get("Compost time: {0:0.00}hr", 1f / ratePerHour));

        float nutr = _inventory.GetNutritionFactor();
        dsc.AppendLine(Lang.Get(
            "Factors: Inoculum {0:0}% × Temp {1:0}% × Moisture {2:0}% × Nutrition {3:0}%",
            100f * _inventory.GetInoculumFactor01(),
            100f * _inventory.GetTemperatureFactor01(),
            100f * _inventory.GetMoistureFactor01(),
            100f * nutr
        ));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        _inventory.FromTreeAttributes(tree, "_inventory");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        _inventory.ToTreeAttributes(tree, "_inventory");
    }
}