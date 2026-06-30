using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

// Portable crop layer mirroring BlockEntityFarmland's crop logic, so a non-Farmland host
// (e.g. BlockEntityPlowland) catches the same growth, light, damage, death, and persistence
// lifecycles. The host wires three base virtuals it cannot reach: RainHeightOffset,
// RecoverFertility, onRollback (see BlockEntityPlowland).
public sealed class CropGrowth
{
    #region Tuning
    private const float MoistureGrowthMin      = 0.1f;   // below this, growth stalls (matches vanilla)
    private const float DamageDeathThresholdH  = 48f;    // accumulated stress-hours before the crop dies
    private const float DamageDecayDivisor     = 10f;    // safe-condition decay: accum -= hours / divisor
    #endregion

    #region StoredState
    public float GrowthRateMul = 1f;
    public bool AllowCropDeath = true;

    public BlockPos UpPos;
    public double TotalHoursForNextStage = -1;

    public bool RipeCropColdDamaged;
    public bool UnripeCropColdDamaged;
    public bool UnripeHeatDamaged;

    // Accumulated stress-hours — not persisted, resets to 0 on reload (matches vanilla damageAccum)
    private float _coldDamageAccumH;
    private float _heatDamageAccumH;

    // Per-crop behaviour scratch state (fruiting crops etc.); persisted so it survives save/load
    private TreeAttribute _cropAttrs = new();
    public ITreeAttribute CropAttributes => _cropAttrs;
    #endregion

    public void Init(BlockPos bePos) => UpPos = bePos.UpCopy();
    public void SetRules(float growthRateMul) => GrowthRateMul = Math.Max(0.01f, growthRateMul);

    #region Queries
    public Block GetCrop(IWorldAccessor world)
    {
        if (UpPos is null) // guards base-class virtuals (RainHeightOffset/RecoverFertility) evaluated before Init
            return null;

        Block block = world.BlockAccessor.GetBlock(UpPos);
        return block?.CropProps != null ? block : null;
    }

    public bool HasCrop(IWorldAccessor world) => GetCrop(world) != null;

    public int GetCropStage(Block block)
    {
        int.TryParse(block.LastCodePart(), out int stage);
        return stage;
    }

    public bool HasRipeCrop(IWorldAccessor world)
    {
        Block block = GetCrop(world);
        return
            block is not null
        &&  GetCropStage(block) >= block.CropProps.GrowthStages;
    }

    public bool CanPlant(IWorldAccessor world)
    {
        Block block = world.BlockAccessor.GetBlock(UpPos);
        return
            block is null
        ||  block.BlockMaterial == EnumBlockMaterial.Air;
    }
    #endregion

    public bool TryPlant
        (Block block
        ,ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,IWorldAccessor world
        ,float growthRate
        )
    {
        if (!CanPlant(world)
        ||  block.CropProps is null
            )
            return false;

        world.BlockAccessor.SetBlock(block.BlockId, UpPos);
        TotalHoursForNextStage = world.Calendar.TotalHours + GetHoursForNextStage(block, world, growthRate);

        if (block.CropProps.Behaviors is not null)
            foreach (CropBehavior behavior in block.CropProps.Behaviors)
                behavior.OnPlanted(world.Api, slot, byEntity, blockSel);

        return true;
    }

    public void OnCropBlockBroken()
    {
        TotalHoursForNextStage = -1;
        RipeCropColdDamaged = false;
        UnripeCropColdDamaged = false;
        UnripeHeatDamaged = false;
        _coldDamageAccumH = 0;
        _heatDamageAccumH = 0;
    }

    public void OnRollback(double hoursRolledBack)
    {
        // Intent: only an active schedule is rolled back; -1 is the "no crop" sentinel
        if (TotalHoursForNextStage >= 0)
            TotalHoursForNextStage -= hoursRolledBack;
    }

    #region Tick
    public bool Tick
        (double currentTotalHours
        ,double hourInterval
        ,double previousHourInterval
        ,double lightGrowthSpeedFactor
        ,float moisture01
        ,bool growthPaused
        ,ClimateCondition conds
        ,IWorldAccessor world
        ,IFarmlandBlockEntity host
        ,float growthRate
        ,out EnumSoilNutrient consumedNutrient
        ,out float consumedAmount
        )
    {
        consumedNutrient = default;
        consumedAmount = 0f;

        UpdateCropDamage(conds, hourInterval, world);

        Block crop = GetCrop(world); // re-read: UpdateCropDamage may have killed it
        if (crop is null)
            return false;

        // Intent: native light model — add back the un-grown fraction of this interval every tick,
        //         rather than baking lightGrowthSpeedFactor into growthRate at stage start
        TotalHoursForNextStage += hourInterval * (1 - lightGrowthSpeedFactor);

        if (growthPaused)
        {
            TotalHoursForNextStage += previousHourInterval;
            return true;
        }

        if (moisture01 < MoistureGrowthMin
        ||  currentTotalHours < TotalHoursForNextStage
            )
            return false;

        #region if(currentStage >= GrowthStages || !nextBlock) return false
        int currentStage = GetCropStage(crop);
        if (currentStage >= crop.CropProps.GrowthStages)
            return false;

        int nextStage  = currentStage + 1;
        Block nextBlock = world.GetBlock(crop.CodeWithParts(nextStage.ToString()));
        if (nextBlock is null)
            return false;
        #endregion

        #region foreach(CropProps.Behaviors) TryGrowCrop()
        if (crop.CropProps.Behaviors is not null)
        {
            EnumHandling handled = EnumHandling.PassThrough;
            bool behaviorResult = false;

            foreach (CropBehavior behavior in crop.CropProps.Behaviors)
            {
                behaviorResult = behavior.TryGrowCrop(world.Api, host, currentTotalHours, nextStage, ref handled);
                if (handled == EnumHandling.PreventSubsequent)
                    return behaviorResult;
            }
            if (handled == EnumHandling.PreventDefault)
                return behaviorResult;
        }
        #endregion

        if (world.BlockAccessor.GetBlockEntity(UpPos) is null)
            world.BlockAccessor.SetBlock(nextBlock.BlockId, UpPos);
        else
            world.BlockAccessor.ExchangeBlock(nextBlock.BlockId, UpPos);

        consumedNutrient = crop.CropProps.RequiredNutrient;
        consumedAmount = crop.CropProps.NutrientConsumption / Math.Max(1, crop.CropProps.GrowthStages - 1);

        Block nextCrop = GetCrop(world) ?? crop; // re-read: UpPos is now nextStage after ExchangeBlock
        TotalHoursForNextStage += GetHoursForNextStage(nextCrop, world, growthRate);

        return true;
    }

    private void UpdateCropDamage(ClimateCondition conds, double hourInterval, IWorldAccessor world)
    {
        Block crop = GetCrop(world);
        if (crop?.CropProps is null)
        {
            RipeCropColdDamaged = false;
            UnripeCropColdDamaged = false;
            UnripeHeatDamaged = false;
            _coldDamageAccumH = 0;
            _heatDamageAccumH = 0;
            return;
        }

        bool isRipe = GetCropStage(crop) >= crop.CropProps.GrowthStages;

        #region cold: ripe→flag only; unripe→flag + accumulate; else decay
        if (conds.Temperature < crop.CropProps.ColdDamageBelow)
        {
            if (isRipe)
                RipeCropColdDamaged = true;
            else
            {
                UnripeCropColdDamaged = true;
                _coldDamageAccumH += (float)hourInterval;
            }
        }
        else
            _coldDamageAccumH = Math.Max(0, _coldDamageAccumH - (float)hourInterval / DamageDecayDivisor);
        #endregion

        #region hot: flag + accumulate; else decay
        if (conds.Temperature > crop.CropProps.HeatDamageAbove)
        {
            UnripeHeatDamaged = true;
            _heatDamageAccumH += (float)hourInterval;
        }
        else
            _heatDamageAccumH = Math.Max(0, _heatDamageAccumH - (float)hourInterval / DamageDecayDivisor);
        #endregion

        if (!AllowCropDeath)
        {
            _coldDamageAccumH = 0;
            _heatDamageAccumH = 0;
            return;
        }

        #region if(accum > threshold) KillCrop(reason)
        if (_coldDamageAccumH > DamageDeathThresholdH)
            KillCrop(world, crop, EnumCropStressType.TooCold);
        else if (_heatDamageAccumH > DamageDeathThresholdH)
            KillCrop(world, crop, EnumCropStressType.TooHot);
        #endregion
    }

    private void KillCrop(IWorldAccessor world, Block cropBlock, EnumCropStressType reason)
    {
        Block deadCropBlock = world.GetBlock(new AssetLocation("deadcrop"));
        if (deadCropBlock is null
        ||  deadCropBlock.Id == 0
            )
            return;

        world.BlockAccessor.SetBlock(deadCropBlock.Id, UpPos);
        if (world.BlockAccessor.GetBlockEntity(UpPos) is BlockEntityDeadCrop beDead)
        {
            beDead.Inventory[0].Itemstack = new ItemStack(cropBlock);
            beDead.deathReason = reason;
        }
    }
    #endregion

    public ItemStack[] GetDrops(ItemStack[] drops, IWorldAccessor world, JsonObject blockAttributes)
    {
        BlockEntityDeadCrop beDeadCrop = world.BlockAccessor.GetBlockEntity(UpPos) as BlockEntityDeadCrop;
        bool isDead = beDeadCrop != null;

        if(!isDead
        && !RipeCropColdDamaged
        && !UnripeCropColdDamaged
        && !UnripeHeatDamaged
            )
            return drops;

        if (!world.Config.GetString("harshWinters").ToBool(true))
            return drops;

        var cropProps = GetCrop(world)?.CropProps;
        if (cropProps is null)
            return drops;

        float mul = 1f;
        if (isDead)
            mul = beDeadCrop.deathReason == EnumCropStressType.Eaten ? 0
        :   Math.Max(cropProps.ColdDamageRipeMul, cropProps.DamageGrowthStuntMul);
        else
        if (RipeCropColdDamaged)
            mul = cropProps.ColdDamageRipeMul;
        else
        if (UnripeHeatDamaged || UnripeCropColdDamaged)
            mul = cropProps.DamageGrowthStuntMul;

        string[] debuffUnaffected = blockAttributes?["debuffUnaffectedDrops"].AsArray<string>();

        List<ItemStack> stacks = new();
        for (int i = 0; i < drops.Length; i++)
        {
            ItemStack stack = drops[i];
            if (WildcardUtil.Match(debuffUnaffected, stack.Collectible.Code.ToShortString()))
            {
                stacks.Add(stack);
                continue;
            }

            float dropQty = stack.StackSize * mul;
            float frac = dropQty - (int)dropQty;
            stack.StackSize = (int)dropQty + (world.Rand.NextDouble() > frac ? 1 : 0);

            if (stack.StackSize > 0)
                stacks.Add(stack);
        }

        return stacks.ToArray();
    }

    private double GetHoursForNextStage(Block cropBlock, IWorldAccessor world, float growthRate)
    {
        if (cropBlock?.CropProps is null)
            return 99999999;

        float totalDays = cropBlock.CropProps.TotalGrowthDays;
        if (totalDays > 0)
            totalDays = totalDays * world.Calendar.DaysPerMonth / 12f;
        else
            totalDays = cropBlock.CropProps.TotalGrowthMonths * world.Calendar.DaysPerMonth;

        float stageHours =
            world.Calendar.HoursPerDay * totalDays / Math.Max(1, cropBlock.CropProps.GrowthStages - 1)
        *  (1f / Math.Max(0.01f, growthRate))
        *  (float)(0.9 + 0.2 * world.Rand.NextDouble());

        return stageHours / GrowthRateMul;
    }

    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetDouble("totalHoursForNextStage", TotalHoursForNextStage);
        tree.SetFloat ("growthRateMul", GrowthRateMul);

        tree.SetBool("ripeCropColdDamaged", RipeCropColdDamaged);
        tree.SetBool("unripeCropColdDamaged", UnripeCropColdDamaged);
        tree.SetBool("unripeHeatDamaged", UnripeHeatDamaged);

        tree["cropAttrs"] = _cropAttrs;
    }

    public void FromTreeAttributes(ITreeAttribute tree)
    {
        TotalHoursForNextStage = tree.GetDouble("totalHoursForNextStage", -1);
        SetRules(tree.GetFloat("growthRateMul", GrowthRateMul));

        RipeCropColdDamaged = tree.GetBool("ripeCropColdDamaged");
        UnripeCropColdDamaged = tree.GetBool("unripeCropColdDamaged");
        UnripeHeatDamaged = tree.GetBool("unripeHeatDamaged");

        _cropAttrs = tree["cropAttrs"] as TreeAttribute ?? new TreeAttribute();
    }
    #endregion
}
