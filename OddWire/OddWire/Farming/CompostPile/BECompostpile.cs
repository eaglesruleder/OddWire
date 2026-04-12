using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OddWire.Renderers;

namespace OddWire.GameContent;

public class BlockEntityCompostpile : BlockEntity, IHeatSource, IBlockTint
{
    private CompostpileSettings Settings => CompostpileSettings.Default;
    
    private readonly CompostpileInventory _inventory = new();

    private bool _neighbourHeatSource;
    private bool _neighboursDirty;
    public bool NeighboursDirty
    {   get => _neighboursDirty;
        set => _neighboursDirty |= value;
    }

    public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
    {
        if (_inventory.Temperature < 35f)
            return 0f;

        float heatTemp = _inventory.Temperature - 35;
        float sizeEmission = GameMath.Lerp(0.2f, 1.2f, _inventory.GetFullness01());
        return 0.0015f * heatTemp * heatTemp * sizeEmission;
    }

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
        if (block == null
        ||  block.Code == Block.Code
            )
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
        {
            NeighboursDirty = true;
            RegisterGameTickListener(OnEvery12Seconds, 12000);
        }
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);

        _inventory.ResetOnPlaced(Block);
        _inventory.PrevTimeProcessed = Api.World.Calendar.TotalHours;

        NeighboursDirty = true;        
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
        if (Api?.Side != EnumAppSide.Server)
            return;

        if (NeighboursDirty)
        {
            UpdateNeighbourBlocks();
            MarkDirty();
        }

        _inventory.AdjacentBlockHeat = GetNeighbourHeat();

        if (_inventory.Update(this, Api.World.Calendar.TotalHours))
        {
            UpdateShapeStackSize();
            MarkDirty(true);
        }
    }

    private float GetNeighbourHeat()
    {
        if (!_neighbourHeatSource)
            return 0f;
        
        float result = 0f;
        for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
        {
            if(i == BlockFacing.indexDOWN)
                continue;
            
            BlockPos neighbourPos = Pos.AddCopy(BlockFacing.ALLFACES[i]);
            result += Api.World.BlockAccessor
               ?.GetBlock(neighbourPos)
               ?.GetInterface<IHeatSource>(Api.World, neighbourPos)
               ?.GetHeatStrength(Api.World, neighbourPos, Pos.Copy())
            ??   0;
        }
        return result;
    }
    
    public void UpdateNeighbourBlocks()
    {
        int blockedSides = 0;

        if (BlocksAirflow( BlockFacing.UP))
            blockedSides++;
        
        if (BlocksAirflow(BlockFacing.NORTH))
            blockedSides++;
        
        if (BlocksAirflow(BlockFacing.SOUTH))
            blockedSides++;

        if (BlocksAirflow(BlockFacing.EAST))
            blockedSides++;

        if (BlocksAirflow(BlockFacing.WEST))
            blockedSides++;

        _inventory.AdjacentBlockCount = blockedSides;
        _neighboursDirty = false;
    }

    private bool BlocksAirflow(BlockFacing neighbour)
    {
        IBlockAccessor blockAccessor = Api.World.BlockAccessor;
        BlockPos neighbourPos = Pos.AddCopy(neighbour);
        
        Block neighbourBlock = blockAccessor.GetBlock(neighbourPos);
        _neighbourHeatSource |= neighbourBlock?.GetInterface<IHeatSource>(Api.World, neighbourPos) is not null;
        
        if (neighbourBlock is null
        ||  neighbourBlock.Id == 0
           )
            return false;
        
        if (blockAccessor.GetBlockEntity(neighbourPos) is BlockEntityCompostpile)
            return true;
        
        if(neighbourBlock.Replaceable >= 6000)
            return false;
        
        return 
            neighbourBlock.SideSolid[neighbour.Opposite.Index]
        ||  neighbourBlock.CollisionBoxes?.Length > 0;
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
        bool skyExposed = Api.World.BlockAccessor?.IsSkyExposed(Pos) == true;
        bool inGreenhouse = false;
        if (!skyExposed)
        {
            var room = Api.ModLoader.GetModSystem<RoomRegistry>()?.GetRoomForPosition(Pos.UpCopy());
            inGreenhouse = 
                room != null
            &&  room.SkylightCount > room.NonSkylightCount
            &&  room.ExitCount == 0;
        }

        string roomLabel = 
            inGreenhouse ? "Greenhouse"
        :   skyExposed ? "Outside"
        :   "Inside";
        if (Settings.InfoDebug)
            roomLabel += Lang.Get(" ({0:0.#}°C)", Api.GetEnvironmentTemperatureC(Pos, Api.World.Calendar.TotalHours, skyExposed, Settings.GreenhouseHeat, out _));
        
        dsc.AppendLine(Lang.Get("Composting: {0}, {1}", GetCompostingStatus(), roomLabel));
        
        dsc.AppendLine(Lang.Get
            ("Temp {0:0.#}°C | Moist {1:0.#}% | Air {2:0.#}%"
            ,_inventory.Temperature
            ,_inventory.Moisture01 * 100f
            ,_inventory.Aeration01 * 100f
            ));

        if (Settings.InfoDebug)
        {
            dsc.AppendLine(Lang.Get
                ("Neighbours: {0}/5 blocking{1}"
                ,_inventory.AdjacentBlockCount
                ,_inventory.AdjacentBlockHeat > 0 ? $" | Heat: {_inventory.AdjacentBlockHeat:0.0}°C" : ""
                ));
            dsc.AppendLine(Lang.Get
                ("Health {0:0}% | Temp {1:0}% × Moist {2:0}% × Air {3:0}%"
                ,_inventory.GetHealth01() * 100f
                ,_inventory.GetTemperatureHealth01() * 100f
                ,_inventory.GetMoistureHealth01() * 100f
                ,_inventory.GetAerationHealth01() * 100f
                ));
            dsc.AppendLine();
        }
        
        
        float factor = _inventory.GetFactor();
        dsc.AppendLine(Lang.Get
            ("Speed: {0:0}%, {1}"
            ,factor * 100f
            ,GetCompostRateLabel(factor)
            ));

        if (Settings.InfoDebug)
        {
            dsc.AppendLine(Lang.Get
                ("Inoc {0:0}% × Temp {1:0}% × Moist {2:0}% × Nut {3:0}%"
                ,_inventory.GetInoculumFactor01() * 100f
                ,_inventory.GetTemperatureFactor01() * 100f
                ,_inventory.GetMoistureFactor01() * 100f
                ,_inventory.GetNutritionFactor() * 100f
                ));
            dsc.AppendLine();
        }
        
        string missingLabel = GetMissingMaterialLabel();
        if (!string.IsNullOrEmpty(missingLabel))
            dsc.AppendLine(missingLabel);

        if (Settings.InfoDebug)
        {
            string mixLabel = GetNutritionMixLabel();
            if (!string.IsNullOrEmpty(mixLabel))
                dsc.AppendLine(mixLabel);
        }
        
        dsc.AppendLine(Lang.Get
            ("Harvest: Compost {0} | Pile {1}{2}"
            ,_inventory.CompostQty
            ,_inventory.GetHarvestableCompostpileQty()
            ,_inventory.Aeration01 < Settings.HypoxicThreshold + Settings.HypoxicTolerance ? " | Turn soon" : ""
            ));
    }

    private string GetCompostingStatus()
    {
        List<string> states = new();

        AppendInoculumStatus(states);
        AppendTemperatureStatus(states);
        AppendMoistureStatus(states);
        AppendAerationStatus(states);

        if (states.Count < 1)
            return "Active";

        return string.Join(", ", states);
    }

    private void AppendTemperatureStatus(List<string> states)
    {
        if (_inventory.Temperature > Settings.OverheatThreshold)
            states.Add("Overheating");
        else if (_inventory.GetTemperatureFactor01() < Settings.InfoFactorWarningThreshhold)
        {
            if(_inventory.Temperature >= 55f)
                states.Add("Hot");
            else
            if (_inventory.Temperature < 20f)
                states.Add("Cold");
        }
    }

    private void AppendMoistureStatus(List<string> states)
    {
        // Intended: _aeration01 reduces, not invalidates Drowning
        if (_inventory.Moisture01 > Settings.DrowningThreshold)
            states.Add("Drowning");
        else if (_inventory.GetMoistureFactor01() < Settings.InfoFactorWarningThreshhold)
        {
            if (_inventory.Moisture01 < Settings.Moisture01Optimal)
                states.Add("Dry");
        }
    }

    private void AppendAerationStatus(List<string> states)
    {
        if (_inventory.Aeration01 < Settings.HypoxicThreshold)
            states.Add("Hypoxic");
    }

    private void AppendInoculumStatus(List<string> states)
    {
        if (_inventory.GetInoculumFactor01() <= 1f - Settings.InfoFactorWarningThreshhold)
            states.Add("Unseeded");
    }

    private string GetCompostRateLabel(float factor)
    {
        float rateSpeed = Settings.BaseCompostRatePerHour * factor;
        if (rateSpeed <= 0f)
            return "stalled";

        float hoursPerCompost = 1f / rateSpeed;
        float hoursPerDay = Api?.World?.Calendar?.HoursPerDay ?? 24f;

        if (hoursPerCompost < hoursPerDay)
            return $"{hoursPerCompost:0.#}hr(s) / compost";

        float daysPerCompost = hoursPerCompost / hoursPerDay;
        return $"{daysPerCompost:0.#} day(s) / compost";
    }

    private string GetMissingMaterialLabel()
    {
        float brownsPortions = (float)_inventory.BrownsQty / Settings.Browns.ConsumePerTransition;
        float nutritionPortions = (float)_inventory.NutritionQty / Settings.Nutrition.ConsumePerTransition;
        float inoculumPortions = (float)_inventory.InoculumQty / Settings.Inoculum.ConsumePerTransition;

        float maxThresholdPortions =
            Math.Max(Math.Max(brownsPortions, nutritionPortions), inoculumPortions)
        -   Settings.InfoNeedsPortionsThreshhold;

        float minPortions = Math.Min(Math.Min(brownsPortions, nutritionPortions), inoculumPortions);
        if (minPortions > maxThresholdPortions
        && !Settings.InfoDebug
           )
            return "";

        string result = Settings.InfoDebug ? "" : "Needs: ";
        
        if (brownsPortions < maxThresholdPortions
        ||  Settings.InfoDebug
           )
            result += Lang.Get
            ("{0} {1}/{2}{3} | "
            ,"Browns"
            ,_inventory.BrownsQty
            ,Settings.Browns.MaxQty
            ,Settings.InfoDebug ? $" ({(float)_inventory.BrownsQty/Settings.Browns.ConsumePerTransition:0.0})" : ""
            );
        
        if (nutritionPortions < maxThresholdPortions
        ||  Settings.InfoDebug
           )
            result += Lang.Get
            ("{0} {1}/{2}{3} | "
            ,"Nutrition"
            ,_inventory.NutritionQty
            ,Settings.Nutrition.MaxQty
            ,Settings.InfoDebug ? $" ({(float)_inventory.NutritionQty/Settings.Nutrition.ConsumePerTransition:0.0})" : ""
            );
        
        if (inoculumPortions < maxThresholdPortions
        ||  Settings.InfoDebug
           )
            result += Lang.Get
            ("{0} {1}/{2}{3} | "
            ,"Inoculum"
            ,_inventory.InoculumQty
            ,Settings.Inoculum.MaxQty
            ,Settings.InfoDebug ? $" ({(float)_inventory.InoculumQty/Settings.Inoculum.ConsumePerTransition:0.0})" : ""
            );

        return result.Substring(0, result.Length - 3);
    }

    private string GetNutritionMixLabel()
    {
        if (_inventory.NutritionStacks.Count < 1)
            return "";

        StringBuilder mix = new();
        foreach (var nutritionStack in _inventory.NutritionStacks)
        {
            if (nutritionStack.Value < 1)
                continue;
            
            mix.Append(nutritionStack.Key);
            mix.Append(": ");
            mix.Append(nutritionStack.Value);
            mix.Append(", ");
        }

        if (mix.Length < 1)
            return "";

        return Lang.Get("Nut Mix: {0}", mix.ToString().Substring(0, mix.Length - 2));
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