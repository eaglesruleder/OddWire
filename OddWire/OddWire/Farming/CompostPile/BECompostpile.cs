using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OddWire.Renderers;

namespace OddWire.GameContent;

public class BlockEntityCompostpile : BlockEntity, IBlockTint
{
    private CompostpileSettings Settings => CompostpileSettings.Default;
    
    private readonly CompostpileInventory _inventory = new();

    private BlockTintRenderer _tintRenderer;
    private MultiTextureMeshRef _tintMeshRef;

    private readonly BlockTint _blockTint = new()
        {NormalShaded = true
        ,RenderRange = 128
        };

    public BlockTint BlockTint
    { get {
        _blockTint.MeshRef = _tintMeshRef;
        _blockTint.Rgba = _inventory.GetVisualTintRgba();
        _blockTint.Enabled = _tintMeshRef?.Disposed != true;
        return _blockTint;
    } }

    private void RegenTintMesh()
    {
        if (Api is not ICoreClientAPI capi)
            return;

        _tintMeshRef?.Dispose();
        _tintMeshRef = null;

        if (Block == null)
            return;

        capi.Tesselator.TesselateBlock(Block, out MeshData mesh);
        _tintMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
    }

    private void DisposeTintRenderer()
    {
        _tintRenderer?.Dispose();
        _tintRenderer = null;

        _tintMeshRef?.Dispose();
        _tintMeshRef = null;
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator) => true;

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
    
    
    public bool TryAdd(ItemSlot slot, out int accepted)
    {
        accepted = 0;

        if (!_inventory.TryAdd(this, slot, out accepted) || accepted < 1)
            return false;

        UpdateShapeStackSize();
        MarkDirty(true);
        return true;
    }

    
    //  Intent: CanHarvest treats Nutrition as lossy
    public bool CanHarvest() => _inventory.CanHarvest();
    
    //  Objective: Harvest all Compost and Compostpile, then remaining Browns & Inoculum, ignore nutrition
    public void Harvest(float dropQuantityMultiplier)
    {
        bool dirty = false;
        
        dirty |= _inventory.HarvestCompost(this, dropQuantityMultiplier);
        dirty |= _inventory.HarvestCompostpile(this, dropQuantityMultiplier);

        if (!dirty)
        {
            dirty |= _inventory.HarvestBrowns(this, dropQuantityMultiplier);
            dirty |= _inventory.HarvestInoculum(this, dropQuantityMultiplier);
        }
        
        if(dirty)
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

        if (api.Side == EnumAppSide.Client
        &&  api is ICoreClientAPI capi
            )
        {
            RegenTintMesh();
            _tintRenderer ??= new BlockTintRenderer(capi, this);
        }

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

    public override void OnExchanged(Block block)
    {
        base.OnExchanged(block);

        if (Api?.Side == EnumAppSide.Client)
            RegenTintMesh();
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        DisposeTintRenderer();
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        DisposeTintRenderer();
    }

    private void OnEvery12Seconds(float dt)
    {
        if (Api?.Side == EnumAppSide.Server
        && _inventory.Update(this, Api.World.Calendar.TotalHours)
           )
        {
            UpdateShapeStackSize();
            MarkDirty();
        }
    }

    public void Water(float dt)
    {
        if (Api?.Side == EnumAppSide.Server
        &&  _inventory.RestoreMoisture01(this, dt/2)
            )
            MarkDirty();
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        double totalHours = Api?.World?.Calendar?.TotalHours ?? 0;

        bool skyExposed = Api?.World?.BlockAccessor != null && Api.World.BlockAccessor.IsSkyExposed(Pos);
        float envTemp = Api.GetEnvironmentTemperatureC(Pos, totalHours, skyExposed, Settings.GreenhouseHeat, out bool inGreenhouse);
        
        dsc.AppendLine(Lang.Get("Temp: {0:0.#}°C ({1:0.#}°C {2})", _inventory.Temperature, envTemp, inGreenhouse ? "in greenhouse" : "outside"));
        dsc.AppendLine(Lang.Get("Moisture: {0:0.#}% | Aeration: {1:0.#}%", _inventory.Moisture01 * 100f, _inventory.Aeration01 * 100f));

        
        string rateString = "Stalled";
        float rateSpeed = Settings.BaseCompostRatePerHour * _inventory.GetFactor();
        if (rateSpeed > 0)
        {
            float hoursPerCompost = 1f / rateSpeed;
            if (hoursPerCompost < Api.World.Calendar.HoursPerDay)
                rateString = $"{hoursPerCompost:0.00} hours";
            else
                rateString = $"{(hoursPerCompost / Api.World.Calendar.HoursPerDay):0.00} days";
        }

        dsc.AppendLine();
        dsc.AppendLine(Lang.Get("Rate: {0}", rateString));
        dsc.AppendLine(Lang.Get(
            "Factor {0:0}% ({1:#.00}I×{2:#.00}T×{3:#.00}M×{4:#.00}N)",
            _inventory.GetFactor() * 100,
            _inventory.GetInoculumFactor01(),
            _inventory.GetTemperatureFactor01(),
            _inventory.GetMoistureFactor01(),
            _inventory.GetNutritionFactor()
        ));
        
        dsc.AppendLine(Lang.Get(
            "Health {0:0}% ({1:#.00}A×{2:#.00}T×{3:#.00}M)",
            _inventory.GetHealth01() * 100,
            _inventory.GetAerationHealth01(),
            _inventory.GetTemperatureHealth01(),
            _inventory.GetMoistureHealth01()
        ));
        
        
        float brownsPortions = (float)_inventory.BrownsQty / Settings.Browns.ConsumePerTransition;
        float nutritionPortions = (float)_inventory.NutritionQty / Settings.Nutrition.ConsumePerTransition;
        float bulkPortions = brownsPortions + nutritionPortions;
        
        float inoculumPortions = (float)_inventory.InoculumQty / Settings.Inoculum.ConsumePerTransition;
        
        dsc.AppendLine();
        dsc.AppendLine(Lang.Get("Bulk portions: {0:0.0}", bulkPortions));
        dsc.AppendLine(Lang.Get
            ("Browns: {0}/{1} ({2:0.0}) | Nutrition: {3}/{4} ({5:0.0}) | Inoc: {6}+{7}/{8} ({9:0.0})"
            ,_inventory.BrownsQty, Settings.Browns.MaxQty, brownsPortions
            ,_inventory.NutritionQty, Settings.Nutrition.MaxQty, nutritionPortions
            ,_inventory.InoculumQty, _inventory.CompostQty, Settings.Inoculum.MaxQty, inoculumPortions
            ));
        if (_inventory.NutritionStacks.Count > 0)
        {
            var parts = _inventory.NutritionStacks
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}")
                .ToArray();

            if (parts.Length > 0)
                dsc.AppendLine(Lang.Get("-&gt; Mix: {0}", string.Join(", ", parts)));
        }

        
        dsc.AppendLine();
        dsc.AppendLine(Lang.Get(
            "Harvestable: Compost {0}, Pile {1}",
            _inventory.CompostQty,
            _inventory.GetHarvestableCompostpileQty()
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