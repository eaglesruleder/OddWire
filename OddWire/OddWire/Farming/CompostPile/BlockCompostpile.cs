using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using OddWire.VintageStory.API.Common;

namespace OddWire.GameContent;
public class BlockCompostpile : Block
{
    private static readonly CompostpileSettings Settings = CompostpileSettings.Default;
    
    #region InteractionHelp
    private WorldInteraction[] _interactions;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        _interactions = ObjectCacheUtil.GetOrCreate(api, "compostpileInteractions", () =>
        {
            List<WorldInteraction> list = new();

            #region LClick + Shovel = Turn
            List<ItemStack> shovels = new();
            foreach (Item item in api.World.Items)
                if (item?.Tool == EnumTool.Shovel)
                    shovels.Add(new ItemStack(item));

            list.Add(new WorldInteraction
                {ActionLangCode = "oddwire:compostpile-blockhelp-turn"
                ,MouseButton = EnumMouseButton.Left
                ,Itemstacks = shovels.ToArray()
                });
            #endregion
            
            #region RClick + Browns/Inoculum/CompostPile = Add
            List<string> addCodes = new();
            if (Settings.Browns.AddItemCodeRatios is not null)
                addCodes.AddRange(Settings.Browns.AddItemCodeRatios.Keys);
            if (Settings.Inoculum.AddItemCodeRatios is not null)
                addCodes.AddRange(Settings.Inoculum.AddItemCodeRatios.Keys);
            addCodes.Add(Settings.HarvestCompostpilePath);

            ItemStack[] addStacks = api.ResolveStacks(addCodes.ToArray());
            if (addStacks.Length > 0)
                list.Add(new WorldInteraction
                    {ActionLangCode = "oddwire:compostpile-blockhelp-add"
                    ,MouseButton = EnumMouseButton.Right
                    ,Itemstacks = addStacks
                    });
            #endregion

            #region RClick + NutrientItems = Boost
            ItemStack[] boostStacks = api.ResolveStacks(Settings.NutritionHelpCodes);
            if (boostStacks.Length > 0)
                list.Add(new WorldInteraction
                    {ActionLangCode = "oddwire:compostpile-blockhelp-boost"
                    ,MouseButton = EnumMouseButton.Right
                    ,Itemstacks = boostStacks
                    });
            #endregion

            return list.ToArray();
        });
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        => _interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    #endregion

    #region OnBlockInteract
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (blockSel is null
        ||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
        ||  slot?.Empty != false
            )
            return false;
        
        if(!be.TryAdd(slot, byPlayer.Entity.Controls.CtrlKey ? 5 : 1, out int accepted)
        ||  accepted < 1
           )
            return false;

        if (world.Side == EnumAppSide.Server)
        {
            slot.TakeOut(accepted);
            slot.MarkDirty();
        }
        return true;
    }
    
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        #region if(!block is BECompostpile) return base();
        if (blockSel is null
        ||  byEntity.Controls.ShiftKey
        ||  byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCompostpile be
            )
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        #endregion

        #region if(be.TryAdd && Side == Server) slot.TakeOut(accepted); return;
        if (be.TryAdd(slot, byEntity.Controls.CtrlKey ? 5 : 1, out int accepted)
        &&  accepted > 0
           )
        {
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                slot.TakeOut(accepted);
                slot.MarkDirty();
            }

            handling = EnumHandHandling.PreventDefault;
            return;
        }
        #endregion

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }
    #endregion
    
    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);

        if (world.Side == EnumAppSide.Server
        &&  world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCompostpile be
            )
            be.NeighboursDirty = true;
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        #region if(GetBlockEntity(pos) is not BECompostpile || Side != EnumAppSide.Server) return;
        if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCompostpile be)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }
        
        if (world.Side != EnumAppSide.Server)
            return; // Server decides at harvest. Client state remains pre-harvest.
        #endregion
        
        be.Harvest(dropQuantityMultiplier);
        if (be.CanHarvest())
            return;
        
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
