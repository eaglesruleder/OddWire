using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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
                if (PlowlandEngine.CanPlowTarget(block))
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
        if (!PlowlandEngine.CanPlowTarget(targetBlock))
            return;
        #endregion

        #region Require valid support block
        Block supportBlock = world.BlockAccessor.GetBlock(targetPos.DownCopy());
        if (!PlowlandEngine.ResolveSupport(supportBlock).IsValid)
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
        if (!PlowlandEngine.CanPlowTarget(targetBlock)
        ||  !PlowlandEngine.ResolveSupport(supportBlock).IsValid
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
        Block targetBlock = world.BlockAccessor.GetBlock(targetPos);
        Block supportBlock = world.BlockAccessor.GetBlock(targetPos.DownCopy());
        #endregion

        #region Require valid target and support
        if (!PlowlandEngine.CanPlowTarget(targetBlock))
            return;

        PlowlandSupportModel support = PlowlandEngine.ResolveSupport(supportBlock);
        if (!support.IsValid)
            return;
        #endregion

        #region Resolve plowland block
        string fertilityCode = PlowlandEngine.ResolveFertilityCode(targetBlock);
        AssetLocation plowlandCode = PlowlandEngine.ResolvePlowlandCode(Code.Domain, PlowlandSettings.StateDry, fertilityCode);
        Block plowlandBlock = world.GetBlock(plowlandCode);
        if (plowlandBlock == null
        ||  plowlandBlock.Id == 0
            )
            return;
        #endregion

        #region Exchange target into plowland
        world.BlockAccessor.ExchangeBlock(plowlandBlock.Id, targetPos);
        #endregion

        #region Initialise placed BE
        if (world.BlockAccessor.GetBlockEntity(targetPos) is BlockEntityPlowland be)
            be.InitialiseFromPlow(targetBlock, supportBlock);
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

        world.BlockAccessor.MarkBlockDirty(targetPos);
        #endregion
    }
    #endregion

    #region Help
    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot) =>
        interactions.Append(base.GetHeldInteractionHelp(inSlot));
    #endregion
}
