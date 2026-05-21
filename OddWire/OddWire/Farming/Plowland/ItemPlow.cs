using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public class ItemPlow : Item
{
    private PlowlandSettings Settings = new();
    private WorldInteraction[]? interactions;

    private bool CanPlow(ref BlockPos? pos, out Block? block)
    {
        if (pos is null)
        {
            block = null;
            return false;
        }
        
        block = api.World.BlockAccessor.GetBlock(pos);
        if (CanPlow(block))
            return true;

        if (block.Id == 0
        ||  block.BlockMaterial == EnumBlockMaterial.Plant
            )
        {
            BlockPos downPos = pos.DownCopy();
            block = api.World.BlockAccessor.GetBlock(downPos);
            if (CanPlow(block))
            {
                pos = downPos;
                return true;
            }
        }
        block = null;
        return false;
    }

    private static bool CanPlow(Block? block)
    {
        if (block is null
        ||  block.Id == 0
        ||  block.IsLiquid()
           )
            return false;

        if (block is BlockPlowland or BlockFarmland)
            return true;

        return
            FertilitySet.GetCode(block) is not null
        &&  block.BlockMaterial == EnumBlockMaterial.Soil;
    }
    
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api is not ICoreClientAPI capi)
            return;

        interactions = ObjectCacheUtil.GetOrCreate(capi, "plowInteractions", () =>
        {
            List<ItemStack> stacks = new();
            foreach (Block block in capi.World.Blocks)
                if (CanPlow(block))
                    stacks.Add(new ItemStack(block));

            return new[]
                {new WorldInteraction
                    {ActionLangCode = "heldhelp-plow"
                    ,MouseButton    = EnumMouseButton.Right
                    ,Itemstacks     = stacks.ToArray()
                    }
                };
        });
    }

    #region OnHeldInteract
    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        WorldInteraction[] baseInteractions = base.GetHeldInteractionHelp(inSlot);
        return interactions?.Append(baseInteractions) ?? baseInteractions;
    }

    public override void OnHeldInteractStart
        (ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,EntitySelection entitySel
        ,bool firstEvent
        ,ref EnumHandHandling handHandling
        )
    {
        if (blockSel is null
        || !firstEvent 
        || (byEntity.Controls.ShiftKey
        &&  byEntity.Controls.CtrlKey
           ))
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            return;
        }

        BlockPos? targetPos = blockSel.Position;
        if (!CanPlow(ref targetPos, out _))
            return;
        
        byEntity.Stats.Set("walkspeed", "OddWire.ItemPlow", -0.4f, true);
        (byEntity as EntityPlayer)?.walkSpeed = byEntity.Stats.GetBlended("walkspeed");
        
        byEntity.Attributes.SetInt("lastplowx", int.MinValue);
        handHandling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep
        (float secondsUsed
        ,ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,EntitySelection entitySel
        )
    {
        if (blockSel is null
        || (byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey)
            )
            return false;
        
        IWorldAccessor world = byEntity.World;
        BlockPos? targetPos  = blockSel.Position;
        if (!CanPlow(ref targetPos, out _) || targetPos is null)
            return false;

        #region if(Side.Server && seconds > 0.6 && targetPos != "lastplow") DoPlow()
        if (world.Side == EnumAppSide.Server
        &&  secondsUsed > 0.6f
        && (targetPos.X != byEntity.Attributes.GetInt("lastplowx", int.MinValue)
        ||  targetPos.Y != byEntity.Attributes.GetInt("lastplowy", int.MinValue)
        ||  targetPos.Z != byEntity.Attributes.GetInt("lastplowz", int.MinValue)
            ))
        {
            byEntity.Attributes.SetInt("lastplowx", targetPos.X);
            byEntity.Attributes.SetInt("lastplowy", targetPos.Y);
            byEntity.Attributes.SetInt("lastplowz", targetPos.Z);
            DoPlow(slot, byEntity, blockSel);
        }
        #endregion

        return true;
    }

    public override bool OnHeldInteractCancel
        (float secondsUsed
        ,ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,EntitySelection entitySel
        ,EnumItemUseCancelReason cancelReason
        )
    {
        byEntity.Stats.Remove("walkspeed", "OddWire.ItemPlow");
        (byEntity as EntityPlayer)?.walkSpeed = byEntity.Stats.GetBlended("walkspeed");
        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
    }

    public override void OnHeldInteractStop
        (float secondsUsed
        ,ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,EntitySelection entitySel
        )
    {
        byEntity.Stats.Remove("walkspeed", "OddWire.ItemPlow");
        (byEntity as EntityPlayer)?.walkSpeed = byEntity.Stats.GetBlended("walkspeed");
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
    }
    #endregion

    public virtual void DoPlow(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel)
    {
        #region if(!CanPlow(ref targetPos, out targetBlock)) return
        if (blockSel is null)
            return;

        IWorldAccessor world = byEntity.World;
        BlockPos? targetPos  = blockSel.Position;
        if (!CanPlow(ref targetPos, out Block? targetBlock) || targetPos is null)
            return;
        #endregion

        #region supportBlock = world.GetBlock(supportPos)
        BlockEntity? targetBlockEntity = world.BlockAccessor.GetBlockEntity(targetPos);
        BlockPos supportPos = targetPos.DownCopy();
        Block supportBlock  = world.BlockAccessor.GetBlock(supportPos);
        supportBlock = FertilitySet.RevertSupportToSoil(world, supportPos, supportBlock);
        #endregion

        #region ResolveCurrentNutrients(targetBlock / supportBlock)
        string? targetFertilityCode = FertilitySet.GetCode(targetBlock);
        if (targetFertilityCode is null)
            return;

        int targetFertility = FertilitySet.Index(targetFertilityCode);
        float[] targetNutrients = FertilitySet.ResolveNutrients(targetBlock, targetBlockEntity);
        int targetFertilityChange = 0;

        int supportFertility = FertilitySet.Index(supportBlock);
        float supportMax = FertilitySet.Value(supportBlock);
        int supportFertilityChange = 0;
        #endregion

        float chanceChange = (targetNutrients[0] + targetNutrients[1] + targetNutrients[2] + supportMax) /4f;
        float randChange = api.World.Rand.NextSingle() * 100f;
        if (chanceChange < 100f)
        #region if(chanceChange < randChange) highestBlock.fertility-- (support wins ties)
        {
            if (chanceChange < randChange)
            {
                if (targetFertility > supportFertility || supportFertility < 0)
                    targetFertilityChange--;
                else
                    supportFertilityChange--;
            }
        }
        #endregion
        else
        #region if(chanceChange-100 > randChange) lowestBlock.fertility++ (target wins ties)
        {
            if (chanceChange-100f > randChange)
            {
                if (targetFertility < supportFertility || supportFertility < 0)
                    targetFertilityChange++;
                else
                    supportFertilityChange++;
            }
        }
        #endregion

        #region plowlandBlock = GetBlock($"plowland-{targetMoistKey}-{targetFertilityCode}")
        float targetMoisture01 = 0;
        if (targetBlockEntity is BlockEntitySoilNutrition beNutrition)
            targetMoisture01 = beNutrition.MoistureLevel;
        
        string targetMoistKey =
            targetMoisture01 > Settings.MoistVisibleThreshold
        ?   Settings.StateMoist
        :   Settings.StateDry;

        if (targetFertilityChange != 0)
        {
            string? nextTargetCode = FertilitySet.StepCode(targetFertilityCode, targetFertilityChange);
            if (nextTargetCode is not null)
                targetFertilityCode = nextTargetCode;
        }
        
        BlockFacing facingDir = BlockFacing.HorizontalFromAngle(byEntity.SidedPos.Yaw);
        string sideCode = facingDir.Code;
        AssetLocation plowlandCode = new(Code.Domain, $"plowland-{sideCode}-{targetMoistKey}-{targetFertilityCode}");
        Block plowlandBlock = world.GetBlock(plowlandCode);
        if (plowlandBlock is null || plowlandBlock.Id == 0)
            return;
        #endregion
        
        if (supportFertilityChange != 0
        &&  FertilitySet.TryGetSteppedBlock(world, supportBlock, supportFertilityChange, out Block nextBlock)
            )
            world.BlockAccessor.ExchangeBlock(nextBlock.BlockId, supportPos);

        #region SetBlock(plowlandBlock, targetPos).Initialise()
        world.BlockAccessor.SetBlock(plowlandBlock.BlockId, targetPos);
        if (world.BlockAccessor.GetBlockEntity(targetPos) is not BlockEntityPlowland bePlowland)
            return;

        float targetFertMax = FertilitySet.Value(targetFertilityCode);
        float[] resultNutrients =
            {Math.Min(targetFertMax, targetNutrients[0] + supportMax)
            ,Math.Min(targetFertMax, targetNutrients[1] + supportMax)
            ,Math.Min(targetFertMax, targetNutrients[2] + supportMax)
            };
        
        bePlowland.Initialise(resultNutrients, targetMoisture01);
        #endregion

        #region bePlowland.AddSlowRelease(blockAbove & soilCoverage)
        int[] slowFertility = new int[3];
        Block blockAbove = world.BlockAccessor.GetBlock(targetPos.UpCopy());

        if (blockAbove.BlockMaterial == EnumBlockMaterial.Plant
        &&  blockAbove.CropProps == null
           )
        {
            slowFertility[0]++;
            world.BlockAccessor.SetBlock(0, targetPos.UpCopy());
        }
        
        if (targetBlock.BlockMaterial == EnumBlockMaterial.Soil
        &&  targetBlock.Variant["grasscoverage"].Equals("normal")
           )
            slowFertility[0]++;
        
        if (blockAbove.CropProps != null)
        {
            int.TryParse(blockAbove.LastCodePart(), out int currLvl);
            int remaining = blockAbove.CropProps.GrowthStages - currLvl;
            int fertMax = FertilitySet.Count - 1;
            int cropBonusLvl = fertMax - Math.Min(remaining, fertMax);
            slowFertility[(int)blockAbove.CropProps.RequiredNutrient] += cropBonusLvl;
        }

        bePlowland.AddSlowRelease
            (FertilitySet.Value(slowFertility[0])
            ,FertilitySet.Value(slowFertility[1])
            ,FertilitySet.Value(slowFertility[2])
            );
        #endregion

        #region ExchangeAdjacentPlowland([left, right])
        // Intent: This is resolving as left/right.
        Vec3i norm = facingDir.Normali;
        TryExchangePlowlandToFarmland(world, targetPos.AddCopy(norm.X, 0, -norm.Z));
        TryExchangePlowlandToFarmland(world, targetPos.AddCopy(-norm.X, 0, norm.Z));
        #endregion
        
        #region if(byPlayer is EntityPlayer) slot.DamageItem()
        IPlayer? byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer is not null)
        {
            slot.Itemstack?.Collectible.DamageItem(world, byEntity, slot);

            if (slot.Empty)
                world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), byEntity.Pos.X, byEntity.Pos.InternalY, byEntity.Pos.Z);
        }
        #endregion

        #region world.PlaySoundAt(targetBlock.Sounds?.Place)
        if (targetBlock.Sounds != null)
            world.PlaySoundAt(targetBlock.Sounds.Place, targetPos, 0.4, null);

        world.BlockAccessor.MarkBlockDirty(supportPos);
        world.BlockAccessor.MarkBlockDirty(targetPos);
        #endregion
    }
    
    private void TryExchangePlowlandToFarmland(IWorldAccessor world, BlockPos pos)
    {
        Block block = world.BlockAccessor.GetBlock(pos);
        if (block is not BlockPlowland
        ||  world.BlockAccessor.GetBlock(pos.UpCopy()).CropProps != null
           )
            return;

        string? fertilityCode = FertilitySet.GetCode(block);
        if (fertilityCode is null)
            return;

        #region prevData = beSrc.ToTreeAttributes() — captures NPK + moisture before block replacement
        TreeAttribute? prevData = null;
        float moisture01 = 0f;
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntitySoilNutrition beSrc)
        {
            prevData = new TreeAttribute();
            beSrc.ToTreeAttributes(prevData);
            moisture01 = beSrc.MoistureLevel;
        }
        #endregion

        string moistKey = moisture01 > Settings.MoistVisibleThreshold
            ?   Settings.StateMoist
            :   Settings.StateDry;

        AssetLocation farmlandCode = new("game", $"farmland-{moistKey}-{fertilityCode}");
        Block farmlandBlock = world.GetBlock(farmlandCode);
        if (farmlandBlock is null || farmlandBlock.Id == 0)
            return;

        world.BlockAccessor.SetBlock(farmlandBlock.BlockId, pos);

        #region beDst.OnCreatedFromSoil(block, prevData) — sets originalFertility, restores NPK + moisture
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityFarmland beDst)
            beDst.OnCreatedFromSoil(block, prevData);
        #endregion

        world.BlockAccessor.MarkBlockDirty(pos);
    }
}
