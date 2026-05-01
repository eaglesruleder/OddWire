using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public sealed class CropGrowth
{
    public float GrowthRateMul = 1f;
    
    public BlockPos UpPos;
    public double TotalHoursForNextStage = -1;
    
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

    #region Actions
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

        foreach (CropBehavior behavior in block.CropProps.Behaviors)
            behavior.OnPlanted(world.Api, slot, byEntity, blockSel);

        return true;
    }

    public bool TryGrowCrop
        (double currentTotalHours
        ,IWorldAccessor world
        ,IFarmlandBlockEntity host       // BE implements this; container passes through for CropBehavior compat
        ,float growthRate
        ,out EnumSoilNutrient consumedNutrient
        ,out float consumedAmount
        )
    {
        consumedNutrient = EnumSoilNutrient.N;
        consumedAmount   = 0f;

        #region Require crop with room to grow
        Block block = GetCrop(world);
        if (block is null)
            return false;

        int currentStage = GetCropStage(block);
        if (currentStage >= block.CropProps.GrowthStages)
            return false;
        #endregion

        #region Resolve next stage block
        int nextStage = currentStage + 1;
        Block nextBlock = world.GetBlock(block.CodeWithParts("" + nextStage));
        if (nextBlock is null)
            return false;
        #endregion

        #region Fire CropBehaviors
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

        #region Advance block stage
        if (world.BlockAccessor.GetBlockEntity(UpPos) is null)
            world.BlockAccessor.SetBlock(nextBlock.BlockId, UpPos);
        else
            world.BlockAccessor.ExchangeBlock(nextBlock.BlockId, UpPos);
        #endregion

        #region Return nutrient consumption
        consumedNutrient = block.CropProps.RequiredNutrient;
        consumedAmount   = block.CropProps.NutrientConsumption / Math.Max(1, block.CropProps.GrowthStages - 1);
        #endregion

        return true;
    }

    public void OnCropBlockBroken() => TotalHoursForNextStage = -1;
    #endregion

    #region Tick
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
        consumedNutrient = EnumSoilNutrient.N;
        consumedAmount   = 0f;

        #region Require an active crop
        Block crop = GetCrop(world);
        if (crop is null)
            return false;
        #endregion

        #region Postpone growth if paused
        if (growthPaused)
        {
            TotalHoursForNextStage += hourInterval;
            return true;
        }
        #endregion

        #region Require moisture to grow
        if (moisture01 < 0.1f)
            return false;
        #endregion

        #region Require time elapsed
        if (currentTotalHours < TotalHoursForNextStage)
            return false;
        #endregion

        #region Grow crop and advance timer
        if (!TryGrowCrop(currentTotalHours, world, host, growthRate, out consumedNutrient, out consumedAmount))
            return false;

        Block nextCrop = GetCrop(world) ?? crop;
        TotalHoursForNextStage += GetHoursForNextStage(nextCrop, world, growthRate);
        #endregion

        return true;
    }
    #endregion

    #region Helpers
    private double GetHoursForNextStage(Block cropBlock, IWorldAccessor world, float growthRate)
    {
        if (cropBlock?.CropProps is null)
            return 99999999;

        float totalDays = cropBlock.CropProps.TotalGrowthDays;
        if (totalDays > 0)
        {
            // Backwards compat: convert legacy days to months, then rescale to current calendar
            float defaultMonths = totalDays / 12f;
            totalDays = defaultMonths * world.Calendar.DaysPerMonth;
        }
        else
        {
            totalDays = cropBlock.CropProps.TotalGrowthMonths * world.Calendar.DaysPerMonth;
        }

        float stageHours  = world.Calendar.HoursPerDay * totalDays / Math.Max(1, cropBlock.CropProps.GrowthStages - 1);
        stageHours       *= 1f / Math.Max(0.01f, growthRate);
        stageHours       *= (float)(0.9 + 0.2 * world.Rand.NextDouble());

        return stageHours / GrowthRateMul;
    }
    #endregion

    #region Persistence
    public void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetDouble("totalHoursForNextStage", TotalHoursForNextStage);
        tree.SetFloat("growthRateMul", GrowthRateMul);
    }

    public void FromTreeAttributes(ITreeAttribute tree)
    {
        TotalHoursForNextStage = tree.GetDouble("totalHoursForNextStage", -1);
        SetRules(tree.GetFloat("growthRateMul", GrowthRateMul));
    }
    #endregion
}
