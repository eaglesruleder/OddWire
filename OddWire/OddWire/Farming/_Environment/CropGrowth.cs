using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class CropGrowth
{
    public float GrowthRateMul = 1f;

    public BlockPos UpPos;
    public double TotalHoursForNextStage = -1;
    
    public bool RipeCropColdDamaged;
    public bool UnripeCropColdDamaged;
    public bool UnripeHeatDamaged;

    public void Init(BlockPos bePos) => UpPos = bePos.UpCopy();
    public void SetRules(float growthRateMul) => GrowthRateMul = Math.Max(0.01f, growthRateMul);

    #region Queries
    public Block GetCrop(IWorldAccessor world)
    {
        Block block = world.BlockAccessor.GetBlock(UpPos);
        return block?.CropProps != null ? block : null;
    }

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
    }

    public void CheckDamage(ClimateCondition conds, IWorldAccessor world)
    {
        Block crop = GetCrop(world);
        if (crop?.CropProps is null)
            return;

        bool isRipe = GetCropStage(crop) >= crop.CropProps.GrowthStages;

        if (conds.Temperature < crop.CropProps.ColdDamageBelow)
        {
            if (isRipe)
                RipeCropColdDamaged = true;
            else
                UnripeCropColdDamaged = true;
        }

        if (conds.Temperature > crop.CropProps.HeatDamageAbove)
            UnripeHeatDamaged = true;
    }

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
    
    public bool Tick
        (double currentTotalHours
        ,double hourInterval
        ,float moisture01
        ,bool growthPaused
        ,IWorldAccessor world
        ,IFarmlandBlockEntity host
        ,float growthRate
        ,out EnumSoilNutrient consumedNutrient
        ,out float consumedAmount
        )
    {
        consumedNutrient = default;
        consumedAmount = 0f;
        
        Block crop = GetCrop(world);
        if (crop is null)
            return false;
        
        if (growthPaused)
        {
            TotalHoursForNextStage += hourInterval;
            return true;
        }
        
        if (currentTotalHours < TotalHoursForNextStage
        ||  moisture01 < 0.1f
        || !TryGrowCrop(currentTotalHours, world, host, growthRate, out consumedNutrient, out consumedAmount)
            )
            return false;

        Block nextCrop = GetCrop(world) ?? crop;
        TotalHoursForNextStage += GetHoursForNextStage(nextCrop, world, growthRate);

        return true;
    }
    
    public bool TryGrowCrop
        (double currentTotalHours
        ,IWorldAccessor world
        ,IFarmlandBlockEntity host
        ,float growthRate
        ,out EnumSoilNutrient consumedNutrient
        ,out float consumedAmount
        )
    {
        consumedNutrient = EnumSoilNutrient.N;
        consumedAmount   = 0f;

        #region if(!GetCrop(world) || currentStage >= GrowthStages) return false;
        Block block = GetCrop(world);
        if (block is null)
            return false;

        int currentStage = GetCropStage(block);
        if (currentStage >= block.CropProps.GrowthStages)
            return false;
        
        int nextStage = currentStage + 1;
        Block nextBlock = world.GetBlock(block.CodeWithParts("" + nextStage));
        if (nextBlock is null)
            return false;
        #endregion

        #region foreach(CropProps.Behaviors) TryGrowCrop()
        if (block.CropProps.Behaviors is not null)
        {
            EnumHandling handled = EnumHandling.PassThrough;
            bool behaviorResult  = false;

            foreach (CropBehavior behavior in block.CropProps.Behaviors)
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
        
        consumedNutrient = block.CropProps.RequiredNutrient;
        consumedAmount   = block.CropProps.NutrientConsumption / Math.Max(1, block.CropProps.GrowthStages - 1);

        return true;
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
    }

    public void FromTreeAttributes(ITreeAttribute tree)
    {
        TotalHoursForNextStage = tree.GetDouble("totalHoursForNextStage", -1);
        SetRules(tree.GetFloat("growthRateMul", GrowthRateMul));
        
        RipeCropColdDamaged = tree.GetBool("ripeCropColdDamaged");
        UnripeCropColdDamaged = tree.GetBool("unripeCropColdDamaged");
        UnripeHeatDamaged = tree.GetBool("unripeHeatDamaged");
    }
    #endregion
}
