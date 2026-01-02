using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable

namespace OddWire.GameContent
{
    public class BlockBrazier : Block, IIgnitable, ISmokeEmitter
    {
        public bool IsExtinct;

        private AdvancedParticleProperties[] _ringParticles;
        private Vec3f[] _basePos;
        private WorldInteraction[] _interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            IsExtinct = !WildCardMatch("*-lit-*");
            
            if (api.Side == EnumAppSide.Client)
                OnLoaded_Particles(api);

            _interactions = ObjectCacheUtil.GetOrCreate(api, "brazierInteractions", () =>
            {
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, true);

                return new []
                    {new WorldInteraction
                        {
                            ActionLangCode = "blockhelp-brazier-open",
                            MouseButton = EnumMouseButton.Right,
                        }
                    ,new WorldInteraction
                    {
                        ActionLangCode = "blockhelp-brazier-ignite",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBrazier;
                            if (bef == null || bef.IsBurning || (bef.fuelSlot?.Empty ?? true))
                                return null;
                            return wi.Itemstacks;
                        }
                    }
                    ,new WorldInteraction
                    {
                        ActionLangCode = "blockhelp-brazier-refuel",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift"
                    }
                };
            });
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return _interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
        
        private void OnLoaded_Particles(ICoreAPI api)
        {
            if (ParticleProperties == null || IsExtinct)
                return;
            
            _ringParticles = new AdvancedParticleProperties[ParticleProperties.Length*4];
            _basePos = new Vec3f[_ringParticles.Length];

            Cuboidf[] spawnBoxes = new Cuboidf[]
                {new (x1: 0.125f, y1: 0, z1: 0.125f, x2: 0.3125f, y2: 0.5f, z2: 0.875f)
                ,new (x1: 0.7125f, y1: 0, z1: 0.125f, x2: 0.875f, y2: 0.5f, z2: 0.875f)
                ,new (x1: 0.125f, y1: 0, z1: 0.125f, x2: 0.875f, y2: 0.5f, z2: 0.3125f)
                ,new (x1: 0.125f, y1: 0, z1: 0.7125f, x2: 0.875f, y2: 0.5f, z2: 0.875f)
                };
               
            for (int i = 0; i < ParticleProperties.Length; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    AdvancedParticleProperties props = ParticleProperties[i].Clone();

                    Cuboidf box = spawnBoxes[j];
                    _basePos[i * 4 + j] = new Vec3f(0,0,0);

                    props.PosOffset[0].avg = box.MidX;
                    props.PosOffset[0].var = box.Width/2;

                    props.PosOffset[1].avg = 0.1f;
                    props.PosOffset[1].var = 0.05f;

                    props.PosOffset[2].avg = box.MidZ;
                    props.PosOffset[2].var = box.Length / 2;

                    props.Quantity.avg /= 4f;
                    props.Quantity.var /= 4f;

                    _ringParticles[i * 4 + j] = props;
                }   
            }
        }

        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (world.Rand.NextDouble() < 0.05
            &&  GetBlockEntity<BlockEntityBrazier>(pos)?.IsBurning == true
                )
                entity.ReceiveDamage(new DamageSource
                    {Source = EnumDamageSource.Block
                    ,SourceBlock = this
                    ,Type = EnumDamageType.Fire
                    ,SourcePos = pos.ToVec3d()
                    },0.5f);

            base.OnEntityInside(world, entity, pos);
        }
        
        public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
        {
            if (creatureType is EnumAICreatureType.LandCreature or EnumAICreatureType.Humanoid)
                return GetBlockEntity<BlockEntityBrazier>(pos)?.IsBurning == true ? 10000f : 1f;

            return base.GetTraversalCost(pos, creatureType);
        }
        
        
        #region IIgnitable
        public EnumIgniteState OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            if (bef?.IsBurning == false)
                return secondsIgniting > 2
                ?   EnumIgniteState.IgniteNow
                :   EnumIgniteState.Ignitable;
            return EnumIgniteState.NotIgnitable;
        }
        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            if (bef != null) 
                return bef.GetIgnitableState(secondsIgniting);
            return EnumIgniteState.NotIgnitable;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            if (bef?.canIgniteFuel == false)
            {
                bef.canIgniteFuel = true;
                bef.extinguishedTotalHours = api.World.Calendar.TotalHours;
            }

            handling = EnumHandling.PreventDefault;
        }
        #endregion
        
        #region ISmokeEmitter
        public bool EmitsSmoke(BlockPos pos)
        {
            var bebrazier = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            return bebrazier?.IsBurning == true;
        }
        #endregion

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            bool val = base.ShouldReceiveClientParticleTicks(world, player, pos, out _);
            isWindAffected = true;
            return val;
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            BlockEntityBrazier bef = manager.BlockAccess.GetBlockEntity(pos) as BlockEntityBrazier;
            if (IsExtinct
            ||  bef?.CurrentModel != EnumBrazierModel.Wide
                )
            {
                base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
                return;
            }
            
            for (int i = 0; i < _ringParticles.Length; i++)
            {
                AdvancedParticleProperties bps = _ringParticles[i];
                bps.WindAffectednesAtPos = windAffectednessAtPos;
                bps.basePos.X = pos.X + _basePos[i].X;
                bps.basePos.Y = pos.InternalY + _basePos[i].Y;
                bps.basePos.Z = pos.Z + _basePos[i].Z;

                manager.Spawn(bps);
            }
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null
            && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)
                )
                return false;
            
            ItemStack stack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
            BlockEntityBrazier beBrazier = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBrazier;
            if (stack is null
            ||  beBrazier == null
                )
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            
            if (stack.Block.HasBehavior<BlockBehaviorCanIgnite>()
            &&  beBrazier.GetIgnitableState(0) == EnumIgniteState.Ignitable
                )
                return false;
            
            if (OnBlockInteractStart_tryStackCombustible(world, byPlayer, blockSel, beBrazier, stack)
            ||  OnBlockInteractStart_tryMealContainer(world, byPlayer, blockSel, beBrazier, stack)
            ||  OnBlockInteractStart_trySmeltingContainer(world, byPlayer, blockSel, beBrazier, stack)
                )
            {
                (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                if (stack.ItemAttributes?["placeSound"].Exists == true)
                {
                    var loc = AssetLocation.Create(stack.ItemAttributes["placeSound"].AsString(), stack.Collectible.Code.Domain);
                    api.World.PlaySoundAt(loc.WithPathPrefixOnce("sounds/"), blockSel.Position.X, blockSel.Position.InternalY, blockSel.Position.Z, byPlayer, 0.88f + (float)api.World.Rand.NextDouble() * 0.24f, 16);
                }

                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        private bool OnBlockInteractStart_tryStackCombustible(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, BlockEntityBrazier beBrazier, ItemStack stack)
        {
            if(!byPlayer.Entity.Controls.ShiftKey
            ||  stack.Collectible.CombustibleProps == null
                )
                return false;

            ItemSlot moveSlot = null;
            if (stack.Collectible.CombustibleProps.MeltingPoint > 0)
                moveSlot = beBrazier.inputSlot;
            else if (stack.Collectible.CombustibleProps.BurnTemperature > 0)
                moveSlot = beBrazier.fuelSlot;

            if (moveSlot == null)
                return false;
            
            ItemStackMoveOperation moveOp = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, 1);
            byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(moveSlot, ref moveOp);
            return moveOp.MovedQuantity > 0;
        }
        
        private bool OnBlockInteractStart_tryMealContainer(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, BlockEntityBrazier beBrazier, ItemStack stack)
        {
            if (stack.Collectible.Attributes?.IsTrue("mealContainer") != true)
                return false;
            
            ItemSlot potSlot = null;
            if (beBrazier.inputStack?.Collectible is BlockCookedContainer)
                potSlot = beBrazier.inputSlot;
            if (beBrazier.outputStack?.Collectible is BlockCookedContainer)
                potSlot = beBrazier.outputSlot;

            if (potSlot != null)
            {
                BlockCookedContainer blockPot = potSlot.Itemstack.Collectible as BlockCookedContainer;
                ItemSlot targetSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (byPlayer.InventoryManager.ActiveHotbarSlot.StackSize > 1)
                {
                    targetSlot = new DummySlot(targetSlot.TakeOut(1));
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    blockPot.ServeIntoStack(targetSlot, potSlot, world);
                    if (!byPlayer.InventoryManager.TryGiveItemstack(targetSlot.Itemstack, true))
                        world.SpawnItemEntity(targetSlot.Itemstack, byPlayer.Entity.ServerPos.XYZ);
                }
                else
                    blockPot.ServeIntoStack(targetSlot, potSlot, world);
            }
            else
            if(!beBrazier.inputSlot.Empty
            ||  byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(api.World, beBrazier.inputSlot, 1) == 0
              )
                beBrazier.OnPlayerRightClick(byPlayer, blockSel);

            return true;
        }

        private bool OnBlockInteractStart_trySmeltingContainer(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, BlockEntityBrazier beBrazier, ItemStack stack)
        {
            return
                stack?.Collectible is BlockSmeltingContainer or BlockSmeltedContainer
            &&  byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(api.World, beBrazier.inputSlot, 1) > 0;
        }
    }
}
