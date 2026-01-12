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
                            BlockEntityBrazier beBrazier = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBrazier;
                            if (beBrazier == null || beBrazier.IsBurning || (beBrazier.FuelSlot?.Empty ?? true))
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

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) =>
            _interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        
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
            if (creatureType is EnumAICreatureType.LandCreature or EnumAICreatureType.Humanoid
            &&  GetBlockEntity<BlockEntityBrazier>(pos)?.IsBurning == true
                )
                return 10000f;
            return base.GetTraversalCost(pos, creatureType);
        }
        
        
        #region IIgnitable
        public EnumIgniteState OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityBrazier beBrazier
            || !beBrazier.IsBurning
               )
                return EnumIgniteState.NotIgnitable;
            
            return secondsIgniting > 2
            ?   EnumIgniteState.IgniteNow
            :   EnumIgniteState.Ignitable;
        }
        
        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityBrazier beBrazier)
                return EnumIgniteState.NotIgnitable;
            
            if (beBrazier.IsBurning
            ||  beBrazier.FuelSlot.Empty
                )
                return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 3
            ?   EnumIgniteState.IgniteNow
            :   EnumIgniteState.Ignitable;
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityBrazier beBrazier
            && !beBrazier.CanIgniteFuel
               )
            {
                beBrazier.CanIgniteFuel = true;
                beBrazier.extinguishedTotalHours = api.World.Calendar.TotalHours;
            }
            
            handling = EnumHandling.PreventDefault;
        }
        #endregion
        
        #region ISmokeEmitter
        public bool EmitsSmoke(BlockPos pos)
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityBrazier beBrazier) 
                return beBrazier.IsBurning;
            return false;
        }
        #endregion

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            isWindAffected = true;
            return base.ShouldReceiveClientParticleTicks(world, player, pos, out _);
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (IsExtinct
            ||  (manager.BlockAccess.GetBlockEntity(pos) is BlockEntityBrazier beBrazier
            &&  !beBrazier.IsWide
                ))
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
            if (stack is null
            ||  world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityBrazier beBrazier
                )
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            
            if (stack.Block?.HasBehavior<BlockBehaviorCanIgnite>() == true
            &&  OnTryIgniteBlock(byPlayer.Entity, blockSel.Position, 0) == EnumIgniteState.Ignitable
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
                    world.PlaySoundAt(loc.WithPathPrefixOnce("sounds/"), blockSel.Position.X, blockSel.Position.InternalY, blockSel.Position.Z, byPlayer, 0.88f + (float)world.Rand.NextDouble() * 0.24f, 16);
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
            
            if (stack.Collectible.CombustibleProps.MeltingPoint > 0)
            {
                ItemStackMoveOperation moveMeltOp = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, 1);
                byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(beBrazier.InputSlot, ref moveMeltOp);
                if (moveMeltOp.MovedQuantity > 0)
                    return true;
            }
            
            if (stack.Collectible.CombustibleProps.BurnTemperature > 0)
            {
                ItemStackMoveOperation moveBurnOp = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, 1);
                byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(beBrazier.FuelSlot, ref moveBurnOp);
                if (moveBurnOp.MovedQuantity > 0)
                    return true;
            }
            
            return false;
        }
        
        private bool OnBlockInteractStart_tryMealContainer(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, BlockEntityBrazier beBrazier, ItemStack stack)
        {
            if (stack.Collectible.Attributes?.IsTrue("mealContainer") != true)
                return false;
            
            ItemSlot potSlot = null;
            if (beBrazier.InputStack?.Collectible is BlockCookedContainer)
                potSlot = beBrazier.InputSlot;
            if (beBrazier.OutputStack?.Collectible is BlockCookedContainer)
                potSlot = beBrazier.OutputSlot;

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
            if(!beBrazier.InputSlot.Empty
            ||  byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(world, beBrazier.InputSlot, 1) == 0
              )
                beBrazier.OnPlayerRightClick(byPlayer, blockSel);

            return true;
        }

        private bool OnBlockInteractStart_trySmeltingContainer(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, BlockEntityBrazier beBrazier, ItemStack stack) =>
            stack?.Collectible is BlockSmeltingContainer or BlockSmeltedContainer
        &&  byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(world, beBrazier.InputSlot, 1) > 0;
    }
}
