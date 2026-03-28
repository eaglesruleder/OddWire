using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public class BlockEntityCompostpile : BlockEntity
{
    private CompostpileSettings Settings => CompostpileSettings.Default;
    
    private readonly CompostpileInventory _inventory = new();

    public void UpdateShapeStackSize() => SetShapeStackSize(_inventory.BrownsQty + _inventory.NutritionQty + _inventory.InoculumQty + _inventory.CompostQty);
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

    public bool CanHarvest() => _inventory.CanHarvest();
    public bool IsEmpty() => _inventory.TotalQty < 1;

    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        if (!_inventory.TryAdd(this, slot, out accepted) || accepted < 1)
            return false;

        UpdateShapeStackSize();
        MarkDirty(true);
        return true;
    }

    public void Harvest(float dropQuantityMultiplier)
    {
        if (_inventory.HarvestCompostpile(this, dropQuantityMultiplier)
        |   _inventory.HarvestSourCompost(this, dropQuantityMultiplier)
        |   _inventory.HarvestCompost(this, dropQuantityMultiplier)
            )
        {
            UpdateShapeStackSize();
            MarkDirty(true);
        }
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (_inventory.Moisture01 <= 0f
        &&  _inventory.PrevTimeMoistureUpdated < 0
            )
            _inventory.Moisture01 = Settings.Moisture01Initial;

        if (api.Side == EnumAppSide.Server)
            RegisterGameTickListener(OnEvery12Seconds, 12000);
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        _inventory.ResetOnPlaced(Block);
        _inventory.PrevTimeProcessed = Api.World.Calendar.TotalHours;
        
        UpdateShapeStackSize();
    }

    private void OnEvery12Seconds(float dt)
    {
        if (Api?.Side == EnumAppSide.Server
        &&  _inventory.Update(this, Api.World.Calendar.TotalHours)
            )
            MarkDirty(true);
    }

    public void Water(float dt)
    {
        if (Api?.Side == EnumAppSide.Server
        &&  _inventory.RestoreMoisture01(this, dt/2)
            )
            MarkDirty(true);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        double totalHours = Api?.World?.Calendar?.TotalHours ?? 0;

        bool skyExposed = Api?.World?.BlockAccessor != null && Api.World.BlockAccessor.IsSkyExposed(Pos);
        float envTemp = Api.GetEnvironmentTemperatureC(Pos, totalHours, skyExposed, Settings.GreenhouseHeat, out bool inGreenhouse);
        
        
        dsc.AppendLine(Lang.Get("Ambient temp: {0:0.#}°C{1}", envTemp, inGreenhouse ? ", (InGreenhouse)" : ""));
        dsc.AppendLine(Lang.Get("Pile temp: {0:0.#}°C", _inventory.Temperature));
        dsc.AppendLine(Lang.Get("Moisture: {0:0.#}%", _inventory.Moisture01 * 100f));
        dsc.AppendLine(Lang.Get("Aeration: {0:0.#}%", _inventory.Aeration01 * 100f));
        

        float brownsPortions = (float)_inventory.BrownsQty / Settings.Browns.ConsumePerTransition;
        float nutritionPortions = (float)_inventory.NutritionQty / Settings.Nutrition.ConsumePerTransition;
        float bulkPortions = brownsPortions + nutritionPortions;
        
        dsc.AppendLine();
        dsc.AppendLine(Lang.Get("Bulk portions: {0:0.0}", bulkPortions));
        dsc.AppendLine(Lang.Get("- Browns: {0}/{1} ({2:0.0})", _inventory.BrownsQty, Settings.Browns.MaxQty, brownsPortions));
        dsc.AppendLine(Lang.Get("- Nutrition: {0}/{1} ({2:0.0})", _inventory.NutritionQty, Settings.Nutrition.MaxQty, nutritionPortions));
        if (_inventory.NutritionStacks.Count > 0)
        {
            var parts = _inventory.NutritionStacks
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                .ToArray();

            if (parts.Length > 0)
                dsc.AppendLine(Lang.Get("-> Mix: {0}", string.Join(", ", parts)));
        }
        dsc.AppendLine(Lang.Get("Inoc: {0} & Comp: {1}/{2}", _inventory.InoculumQty, _inventory.CompostQty, Settings.Inoculum.MaxQty));

        
        dsc.AppendLine();
        dsc.AppendLine(Lang.Get(
            "Harvestable: Pile {0}, Sour {1}, Compost {2}",
            _inventory.GetHarvestableCompostpileQty(),
            _inventory.GetHarvestableSourCompostQty(),
            _inventory.GetHarvestableCompostQty()
        ));

        
        float totalFactor = _inventory.GetFactor();
        float ratePerHour = Settings.BaseCompostRatePerHour * totalFactor;
        
        dsc.AppendLine();
        dsc.AppendLine(Lang.Get("Rate: {0:0.000}/hr", ratePerHour));
        dsc.AppendLine(Lang.Get(
            "- Hours / transition: {0}",
            ratePerHour > 0f ? $"{1f / ratePerHour:0.00}hr" : "stalled"
        ));
        
        dsc.AppendLine(Lang.Get(
            "- Factors: Starter {0:0}% × Temp {1:0}% × Moisture {2:0}% × Nutrition {3:0}% = {4:0}%",
            _inventory.GetInoculumFactor01() * 100,
            _inventory.GetTemperatureFactor01() * 100,
            _inventory.GetMoistureFactor01() * 100,
            _inventory.GetNutritionFactor() * 100,
            totalFactor * 100
        ));
        
        dsc.AppendLine(Lang.Get(
            "Health: Aeration {0:0}% × Temp {1:0}% × Moisture {2:0}% = {3:0}%",
            _inventory.GetAerationHealth01() * 100,
            _inventory.GetTemperatureHealth01() * 100,
            _inventory.GetMoistureHealth01() * 100,
            _inventory.GetHealth01() * 100
        ));
        dsc.AppendLine(Lang.Get("- Stress: {0:0}%", _inventory.Stress01));
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