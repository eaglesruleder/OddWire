using System;
using System.Collections.Generic;
using OddWire.System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public class ItemPlow : Item
{
    private PlowlandSettings Settings = new();
    private WorldInteraction[]? interactions;
    
    #region FertilityHelpers
    private static float[] ResolveCurrentNutrients(Block targetBlock, BlockEntity? targetBlockEntity)
    {
        if (targetBlockEntity is BlockEntityFarmland beFarmland)
            return CloneNutrients(beFarmland.Nutrients);

        if (targetBlockEntity is BlockEntityPlowland bePlowland)
            return CloneNutrients(bePlowland.Nutrients);

        float fertility = FertilitySet.Value(targetBlock);
        return new[] { fertility, fertility, fertility };
    }

    private static float ResolveCurrentMoisture(BlockEntity? targetBlockEntity)
    {
        if (targetBlockEntity is BlockEntityFarmland beFarmland)
            return beFarmland.MoistureLevel;

        if (targetBlockEntity is BlockEntityPlowland bePlowland)
            return bePlowland.Moisture01;

        return 0f;
    }

    private static float[] CloneNutrients(float[] nutrients)
    {
        float[] clone = new float[nutrients.Length];
        for (int i = 0; i < nutrients.Length; i++)
            clone[i] = nutrients[i];
        return clone;
    }
    #endregion
    
    private static bool CanPlow(Block? block)
    {
        if (block is null
            ||  block.Id == 0
            ||  block.IsLiquid()
           )
            return false;

        if (block is BlockPlowland or BlockFarmland)
            return true;

        string? targetFertilityCode = FertilitySet.GetCode(block);
        if (targetFertilityCode is null)
            return false;
        
        return block.BlockMaterial == EnumBlockMaterial.Soil;
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
                    ,MouseButton = EnumMouseButton.Right
                    ,Itemstacks = stacks.ToArray()
                    }
                };
        });
    }

    #region HeldInteract
    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        WorldInteraction[] baseInteractions = base.GetHeldInteractionHelp(inSlot);
        return interactions is null
        ?   baseInteractions
        :   interactions.Append(baseInteractions);
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
        ||  !firstEvent
            )
            return;
        
        if (byEntity.Controls.ShiftKey
        &&  byEntity.Controls.CtrlKey
            )
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            return;
        }

        #region if(covered) TriggerIngameError
        IWorldAccessor world = byEntity.World;
        BlockPos targetPos = blockSel.Position;
        if (world.BlockAccessor.GetBlock(targetPos.UpCopy()).Id != 0)
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "covered", Lang.Get("Requires no block above"));
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }
        #endregion
        
        Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
        if (!CanPlow(targetBlock))
            return;
        
        byEntity.Attributes.SetInt("didplow", 0);
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
        if (blockSel is null)
            return false;
        
        if (byEntity.Controls.ShiftKey
        &&  byEntity.Controls.CtrlKey
            )
            return false;

        #region if(covered || !CanPlowTarget) return false
        IWorldAccessor world = byEntity.World;
        BlockPos targetPos = blockSel.Position;
        if (world.BlockAccessor.GetBlock(targetPos.UpCopy()).Id != 0)
            return false;
        
        Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
        if (!CanPlow(targetBlock))
            return false;
        #endregion

        #region if(secondsUsed > 0.6f && Side.Server) DoPlow()
        if (secondsUsed > 0.6f
        &&  byEntity.Attributes.GetInt("didplow") == 0
        &&  world.Side == EnumAppSide.Server
            )
        {
            byEntity.Attributes.SetInt("didplow", 1);
            DoPlow(slot, byEntity, blockSel);
        }
        #endregion
        
        return secondsUsed < 1f;
    }

    public override bool OnHeldInteractCancel
        (float secondsUsed
        ,ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,EntitySelection entitySel
        ,EnumItemUseCancelReason cancelReason
        ) => false;
    #endregion
    
    public virtual void DoPlow(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel)
    {
        if (blockSel is null)
            return;

        #region targetBlock = GetBlock(targetPos)
        IWorldAccessor world = byEntity.World;
        BlockPos targetPos = blockSel.Position;
        Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
        #endregion

        if (!CanPlow(targetBlock))
            return;
        
        #region supportBlock = GetBlock(supportPos)
        BlockEntity? targetBlockEntity = world.BlockAccessor.GetBlockEntity(targetPos);
        BlockPos supportPos = targetPos.DownCopy();
        Block supportBlock = world.BlockAccessor.GetBlock(supportPos);
        supportBlock = RevertSupportToSoil(world, supportPos, supportBlock);
        #endregion

        #region Get nutrientState
        string targetFertilityCode = FertilitySet.GetCode(targetBlock)!;
        int targetFertility = FertilitySet.Index(targetFertilityCode);
        float[] targetNutrients = ResolveCurrentNutrients(targetBlock, targetBlockEntity);
        float targetAvgNutrients = targetNutrients.Avg();
        int targetFertilityChange = 0;

        int supportFertility = FertilitySet.Index(supportBlock);
        float supportMax = FertilitySet.Value(supportBlock);
        int supportFertilityChange = 0;
        #endregion
        
        float randChange = api.World.Rand.NextSingle() * 100f;
        if (targetAvgNutrients < 100f)
        #region Loose Fertility when Underfed
        {
            if (targetAvgNutrients < randChange)
            {
                if (targetFertility > supportFertility
                ||  supportFertility < 0
                    )
                    targetFertilityChange--;
                else
                    supportFertilityChange--;
            }
        }
        #endregion
        else
        #region Gain Fert when Overfed
        {
            if (targetAvgNutrients - 100f > randChange)
            {
                if (targetFertility < supportFertility
                ||  supportFertility < 0
                   )
                    targetFertilityChange++;
                else
                    supportFertilityChange++;
            }
        }
        #endregion

        #region Build plowland
        float targetMoisture01 = ResolveCurrentMoisture(targetBlockEntity);
        string targetMoistKey =
            targetMoisture01 > 0.10f
        ?   Settings.StateMoist
        :   Settings.StateDry;

        if (targetFertilityChange != 0)
        {
            string? nextTargetCode = FertilitySet.StepCode(targetFertilityCode, targetFertilityChange);
            if (nextTargetCode is not null)
                targetFertilityCode = nextTargetCode;
        }

        AssetLocation plowlandCode = new(Code.Domain, $"plowland-{targetMoistKey}-{targetFertilityCode}");
        Block plowlandBlock = world.GetBlock(plowlandCode);
        if (plowlandBlock is null
        ||  plowlandBlock.Id == 0
            )
            return;
        
        float[] resultNutrients = new float[targetNutrients.Length];
        for (int i = 0; i < targetNutrients.Length; i++)
            resultNutrients[i] = Math.Min(Settings.MaxFertilizedNutrient, targetNutrients[i] + supportMax);
        #endregion
        
        if (supportFertilityChange != 0
        &&  FertilitySet.TryGetSteppedBlock(world, supportBlock, supportFertilityChange, out Block nextBlock)
            )
            world.BlockAccessor.ExchangeBlock(nextBlock.BlockId, supportPos);

        #region Place & init plowland
        world.BlockAccessor.SetBlock(plowlandBlock.BlockId, targetPos);
        if (world.BlockAccessor.GetBlockEntity(targetPos) is not BlockEntityPlowland bePlowland)
            return;

        bePlowland.Initialise(resultNutrients, targetMoisture01);
        #endregion

        #region Apply tool wear
        IPlayer? byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer is not null)
        {
            slot.Itemstack?.Collectible.DamageItem(world, byEntity, slot);

            if (slot.Empty)
                world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), byEntity.Pos.X, byEntity.Pos.InternalY, byEntity.Pos.Z);
        }
        #endregion

        #region Play block feedback
        if (targetBlock.Sounds != null)
            world.PlaySoundAt(targetBlock.Sounds.Place, targetPos, 0.4, null);

        world.BlockAccessor.MarkBlockDirty(supportPos);
        world.BlockAccessor.MarkBlockDirty(targetPos);
        #endregion
    }
    
    private Block RevertSupportToSoil(IWorldAccessor world, BlockPos supportPos, Block supportBlock)
    {
        if (supportBlock is not (BlockFarmland or BlockPlowland))
            return supportBlock;

        BlockEntity? supportBlockEntity = world.BlockAccessor.GetBlockEntity(supportPos);
        float supportNutrientAvg = ResolveCurrentNutrients(supportBlock, supportBlockEntity).Avg();

        string? revertFertilityCode = FertilitySet.GetCode(supportBlock);
        if (supportNutrientAvg < 100f
        &&  api.World.Rand.NextSingle() < supportNutrientAvg / 100
            )
            revertFertilityCode = FertilitySet.StepCode(revertFertilityCode, -1) ?? revertFertilityCode;

        if (revertFertilityCode is null)
            return supportBlock;
        
        AssetLocation soilCode = new("game", $"soil-{revertFertilityCode}");
        Block soilBlock = world.GetBlock(soilCode);
        if (soilBlock is not null
        &&  soilBlock.Id != 0
            )
        {
            world.BlockAccessor.ExchangeBlock(soilBlock.BlockId, supportPos);
            return soilBlock;
        }

        return supportBlock;
    }
}
