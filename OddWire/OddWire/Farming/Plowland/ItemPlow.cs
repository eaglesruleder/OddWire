using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace OddWire.GameContent;

public class ItemPlow : Item
{
    private WorldInteraction[]? interactions;

    #region Setup
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api is not ICoreClientAPI capi)
            return;

        interactions = ObjectCacheUtil.GetOrCreate(capi, "plowInteractions", () =>
        {
            List<ItemStack> stacks = new();
            foreach (Block block in capi.World.Blocks)
                if (CanPlowTarget(block))
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
    #endregion

    #region HeldInteract
    public override void OnHeldInteractStart
        (ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,EntitySelection entitySel
        ,bool firstEvent
        ,ref EnumHandHandling handHandling
        )
    {
        #region Require first event and block target
        if (blockSel is null
        ||  !firstEvent
            )
            return;
        #endregion

        #region Allow override chord
        if (byEntity.Controls.ShiftKey
        &&  byEntity.Controls.CtrlKey
            )
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            return;
        }
        #endregion

        #region Require uncovered target
        IWorldAccessor world = byEntity.World;
        BlockPos targetPos = blockSel.Position;
        if (world.BlockAccessor.GetBlock(targetPos.UpCopy()).Id != 0)
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "covered", Lang.Get("Requires no block above"));
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }
        #endregion

        #region Require valid plow target
        Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
        if (!CanPlowTarget(targetBlock))
            return;
        #endregion

        #region Require valid support block
        Block supportBlock = world.BlockAccessor.GetBlock(targetPos.DownCopy());
        if (!IsValidSupportBlock(supportBlock))
            return;
        #endregion

        #region Begin held plow use
        byEntity.Attributes.SetInt("didplow", 0);
        handHandling = EnumHandHandling.PreventDefault;
        #endregion
    }

    public override bool OnHeldInteractStep
        (float secondsUsed
        ,ItemSlot slot
        ,EntityAgent byEntity
        ,BlockSelection blockSel
        ,EntitySelection entitySel
        )
    {
        #region Require active block target
        if (blockSel is null)
            return false;
        #endregion

        #region Allow override chord
        if (byEntity.Controls.ShiftKey
        &&  byEntity.Controls.CtrlKey
            )
            return false;
        #endregion

        #region Require uncovered target
        IWorldAccessor world = byEntity.World;
        BlockPos targetPos = blockSel.Position;
        if (world.BlockAccessor.GetBlock(targetPos.UpCopy()).Id != 0)
            return false;
        #endregion

        #region Require still-valid target
        Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
        Block supportBlock = world.BlockAccessor.GetBlock(targetPos.DownCopy());
        if (!CanPlowTarget(targetBlock)
        ||  !IsValidSupportBlock(supportBlock)
            )
            return false;
        #endregion

        #region Apply plow once on server
        if (secondsUsed > 0.6f
        &&  byEntity.Attributes.GetInt("didplow") == 0
        &&  world.Side == EnumAppSide.Server
            )
        {
            byEntity.Attributes.SetInt("didplow", 1);
            DoPlow(slot, byEntity, blockSel);
        }
        #endregion

        #region Continue until action completes
        return secondsUsed < 1f;
        #endregion
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

    #region DoPlow
    public virtual void DoPlow(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel)
    {
        #region Require target
        if (blockSel is null)
            return;
        #endregion

        #region Read target and support blocks
        IWorldAccessor world = byEntity.World;
        BlockPos targetPos = blockSel.Position;
        BlockPos supportPos = targetPos.DownCopy();

        Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
        BlockEntity? targetBlockEntity = world.BlockAccessor.GetBlockEntity(targetPos);
        Block supportBlock = world.BlockAccessor.GetBlock(supportPos);
        #endregion

        #region Require valid target
        if (!CanPlowTarget(targetBlock))
            return;
        #endregion

        #region Resolve current nutrients and fertility values
        int targetFertility = FertilitySet.Index(targetBlock);
        float[] targetNutrients = ResolveCurrentNutrients(targetBlock, targetBlockEntity);
        float targetAvgNutrients = GetAverage(targetNutrients);

        int supportFertility = FertilitySet.Index(supportBlock);
        int supportChange = 0;
        float supportMax = FertilitySet.Value(supportBlock);
        #endregion

        float randChange = api.World.Rand.NextSingle() * 100f;
        #region Pull fertility from below when target is not yet saturated
        if (targetAvgNutrients < 100)
        {
            if (targetAvgNutrients < randChange)
            {
                if (targetFertility > supportFertility
                &&  supportFertility > 0
                    )
                    supportChange--;
                else
                    targetFertility--;
            }
        }
        #endregion
        else
        #region Chance fertility step upward when the target is overfed
            if (targetAvgNutrients - 100f > randChange)
            {
                if (targetFertility > supportFertility
                ||  supportFertility < 0
                    )
                    targetFertility++;
                else
                    supportChange++;
            }
        #endregion

        #region Build the new nutrient state for the plowed target
        float[] resultNutrients = new float[targetNutrients.Length];
        for (int i = 0; i < targetNutrients.Length; i++)
            resultNutrients[i] = Math.Min(150f, targetNutrients[i] + supportMax);
        #endregion

        #region Apply support fertility change
        if (supportChange != 0)
            supportBlock = ApplySupportFertilityStep(world, supportPos, supportBlock, supportChange);
        #endregion

        #region Resolve plowland init state
        float targetMoisture01 = ResolveCurrentMoisture(targetBlockEntity);
        bool targetMoist = targetMoisture01 > 0.10f;

        int currentTargetFertility = FertilitySet.Index(targetBlock);
        string targetFertilityCode = FertilitySet.StepCode
            (FertilitySet.GetCode(targetBlock)
            ,targetFertility - currentTargetFertility
            );
        
        if (!TryResolveSupportState
            (supportBlock
            ,out string? supportCode
            ,out float supportRetentionDays
            ,out float supportWaterQuality01
            ,out string supportFertilityCode
            ))
            return;
        #endregion

        #region Place plowland at the target position
        AssetLocation plowlandCode = ResolvePlowlandCode
            (Code.Domain
            ,targetMoist ? PlowlandSettings.StateMoist : PlowlandSettings.StateDry
            ,targetFertilityCode
            );

        Block plowlandBlock = world.GetBlock(plowlandCode);
        if (plowlandBlock is null
        ||  plowlandBlock.Id == 0
            )
            return;

        world.BlockAccessor.SetBlock(plowlandBlock.BlockId, targetPos);

        if (world.BlockAccessor.GetBlockEntity(targetPos) is not BlockEntityPlowland bePlowland)
            return;

        string originalFertilityCode = ResolveOriginalFertilityCode(targetBlock, supportBlock);
        float[] originalNutrients = FertilitySet.MakeUniformNutrients(originalFertilityCode);
        
        bePlowland.Initialise
            (originalNutrients
            ,resultNutrients
            ,targetMoisture01
            ,supportCode
            ,true
            ,supportRetentionDays
            ,supportWaterQuality01
            ,supportFertilityCode
            );
        #endregion

        #region Apply tool wear
        IPlayer? byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer is not null)
        {
            slot.Itemstack?.Collectible.DamageItem(world, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot);

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
    #endregion

    #region PlowRules
    private static bool CanPlowTarget(Block targetBlock)
    {
        if (targetBlock is null
        ||  targetBlock.Id == 0
        ||  targetBlock.IsLiquid()
            )
            return false;

        if (targetBlock is BlockPlowland
        ||  targetBlock is BlockFarmland
            )
            return true;

        return targetBlock.BlockMaterial == EnumBlockMaterial.Soil;
    }

    private static bool IsValidSupportBlock(Block supportBlock) =>
        TryResolveSupportState
            (supportBlock
            ,out _
            ,out _
            ,out _
            ,out _
            );

    private static bool TryResolveSupportState
        (Block supportBlock
        ,out string? supportCode
        ,out float supportRetentionDays
        ,out float supportWaterQuality01
        ,out string supportFertilityCode
        )
    {
        supportCode = null;
        supportRetentionDays = 0f;
        supportWaterQuality01 = 0f;
        supportFertilityCode = PlowlandSettings.DefaultFertility;

        if (supportBlock is null
        ||  supportBlock.Id == 0
        ||  supportBlock.IsLiquid()
            )
            return false;

        supportCode = supportBlock.Code?.ToShortString();
        supportWaterQuality01 = 1f;
        supportFertilityCode = FertilitySet.GetCode(supportBlock) ?? PlowlandSettings.DefaultFertility;

        if (supportBlock is BlockFarmland)
        {
            supportRetentionDays = 4.5f;
            return true;
        }

        if (supportBlock is BlockPlowland)
        {
            supportRetentionDays = 4.25f;
            return true;
        }

        if (supportBlock.BlockMaterial == EnumBlockMaterial.Soil)
        {
            supportRetentionDays = 4f;
            return true;
        }

        return false;
    }

    private static string ResolveOriginalFertilityCode(Block targetBlock, Block supportBlock)
    {
        string? fertilityCode = targetBlock?.LastCodePart();
        if (FertilitySet.Contains(fertilityCode))
            return fertilityCode!;

        fertilityCode = supportBlock?.LastCodePart();
        return FertilitySet.Contains(fertilityCode)
            ? fertilityCode!
            : PlowlandSettings.DefaultFertility;
    }

    private static AssetLocation ResolvePlowlandCode(string domain, string state, string fertilityCode) =>
        new(domain, $"plowland-{state}-{fertilityCode}");
    #endregion

    #region FertilityHelpers
    private static float[] ResolveCurrentNutrients(Block targetBlock, BlockEntity? targetBlockEntity)
    {
        if (targetBlockEntity is BlockEntityFarmland beFarmland)
            return CloneNutrients(beFarmland.Nutrients);

        if (targetBlockEntity is BlockEntityPlowland bePlowland)
            return CloneNutrients(bePlowland.Nutrients);

        float fertility = FertilitySet.Value(targetBlock?.LastCodePart());
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

    private static float GetAverage(float[] values)
    {
        if (values.Length == 0)
            return 0f;

        float total = 0f;
        for (int i = 0; i < values.Length; i++)
            total += values[i];

        return total / values.Length;
    }

    private static Block ApplySupportFertilityStep(IWorldAccessor world, BlockPos pos, Block block, int delta)
    {
        string currentCode = FertilitySet.GetCode(block)!;
        string nextCode = FertilitySet.StepCode(currentCode, delta);
        if (nextCode == currentCode)
            return block;

        string[] codeParts = block.Code.Path.Split('-');
        if (codeParts.Length == 0)
            return block;

        codeParts[^1] = nextCode;
        AssetLocation nextBlockCode = new(block.Code.Domain, string.Join("-", codeParts));
        Block nextBlock = world.GetBlock(nextBlockCode);
        if (nextBlock is null
        ||  nextBlock.Id == 0
            )
            return block;

        world.BlockAccessor.ExchangeBlock(nextBlock.BlockId, pos);
        SyncSupportBlockEntity(world, pos, nextCode);
        return world.BlockAccessor.GetBlock(pos);
    }

    private static void SyncSupportBlockEntity(IWorldAccessor world, BlockPos pos, string fertilityCode)
    {
        int fertility = (int)FertilitySet.Value(fertilityCode);

        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityFarmland beFarmland)
        {
            for (int i = 0; i < 3; i++)
            {
                beFarmland.OriginalFertility[i] = fertility;
                beFarmland.Nutrients[i] = Math.Min(beFarmland.Nutrients[i], fertility);
            }

            beFarmland.MarkDirty(true);
            return;
        }

        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityPlowland bePlowland)
        {
            bePlowland.SetFertility(fertilityCode);
            return;
        }
    }
    #endregion

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        WorldInteraction[] baseInteractions = base.GetHeldInteractionHelp(inSlot);
        return interactions == null
            ? baseInteractions
            : interactions.Append(baseInteractions);
    }
}
