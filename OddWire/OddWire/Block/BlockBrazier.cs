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
        public int Stage { get {
            if(WildCardMatch("*-construct1-*")) return 1;
            if(WildCardMatch("*-construct2-*")) return 2;
            if(WildCardMatch("*-construct3-*")) return 3;
            if(WildCardMatch("*-construct4-*")) return 4;
            return 5;
        } }

        public string NextStageCodePart
        {
            get
            {
                switch (Stage)
                {
                    case 1: return "construct2";
                    case 2: return "construct3";
                    case 3: return "construct4";
                    case 4: return "cold";
                }
                return "cold";
            }
        }


        public bool IsExtinct;

        AdvancedParticleProperties[] ringParticles;
        Vec3f[] basePos;
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            api.Logger.Warning("BlockBrazier:OnLoaded");
            
            IsExtinct = !WildCardMatch("*-lit-*");

            if (!IsExtinct && api.Side == EnumAppSide.Client && this.ParticleProperties == null)
                api.Logger.Error("BlockBrazier:OnLoaded this.ParticleProperties == null");
            if (!IsExtinct && api.Side == EnumAppSide.Client && this.ParticleProperties != null)
            {
                ringParticles = new AdvancedParticleProperties[this.ParticleProperties.Length*4];
                basePos = new Vec3f[ringParticles.Length];

                Cuboidf[] spawnBoxes = new Cuboidf[]
                {
                    new Cuboidf(x1: 0.125f, y1: 0, z1: 0.125f, x2: 0.3125f, y2: 0.5f, z2: 0.875f),
                    new Cuboidf(x1: 0.7125f, y1: 0, z1: 0.125f, x2: 0.875f, y2: 0.5f, z2: 0.875f),
                    new Cuboidf(x1: 0.125f, y1: 0, z1: 0.125f, x2: 0.875f, y2: 0.5f, z2: 0.3125f),
                    new Cuboidf(x1: 0.125f, y1: 0, z1: 0.7125f, x2: 0.875f, y2: 0.5f, z2: 0.875f)
                };
               
                for (int i = 0; i < ParticleProperties.Length; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        AdvancedParticleProperties props = ParticleProperties[i].Clone();

                        Cuboidf box = spawnBoxes[j];
                        basePos[i * 4 + j] = new Vec3f(0,0,0);

                        props.PosOffset[0].avg = box.MidX;
                        props.PosOffset[0].var = box.Width/2;

                        props.PosOffset[1].avg = 0.1f;
                        props.PosOffset[1].var = 0.05f;

                        props.PosOffset[2].avg = box.MidZ;
                        props.PosOffset[2].var = box.Length / 2;

                        props.Quantity.avg /= 4f;
                        props.Quantity.var /= 4f;

                        ringParticles[i * 4 + j] = props;
                    }   
                }
            }


            interactions = ObjectCacheUtil.GetOrCreate(api, "brazierInteractions-"+Stage, () =>
            {
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, true);

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-brazier-open",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection) =>
                        {
                            return Stage == 5;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-brazier-ignite",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBrazier;
                            if (bef?.fuelSlot != null && !bef.fuelSlot.Empty && !bef.IsBurning)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-brazier-refuel",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift"
                    }
                };
            });
        }


        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (world.Rand.NextDouble() < 0.05 && GetBlockEntity<BlockEntityBrazier>(pos)?.IsBurning == true)
            {
                entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Block, SourceBlock = this, Type = EnumDamageType.Fire, SourcePos = pos.ToVec3d() }, 0.5f);
            }

            base.OnEntityInside(world, entity, pos);
        }


        EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
        {
            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            if (bef.IsBurning) return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            return EnumIgniteState.NotIgnitable;
        }
        public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            if (bef == null) return EnumIgniteState.NotIgnitable;
            return bef.GetIgnitableState(secondsIgniting);
        }

        public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            BlockEntityBrazier bef = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            if (bef != null && !bef.canIgniteFuel)
            {
                bef.canIgniteFuel = true;
                bef.extinguishedTotalHours = api.World.Calendar.TotalHours;
            }

            handling = EnumHandling.PreventDefault;
        }


        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            bool val = base.ShouldReceiveClientParticleTicks(world, player, pos, out _);
            isWindAffected = true;

            return val;
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (IsExtinct)
            {
                base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
                return;
            }

            BlockEntityBrazier bef = manager.BlockAccess.GetBlockEntity(pos) as BlockEntityBrazier;
            if (bef != null && bef.CurrentModel == EnumBrazierModel.Wide)
            {
                for (int i = 0; i < ringParticles.Length; i++)
                {
                    AdvancedParticleProperties bps = ringParticles[i];
                    bps.WindAffectednesAtPos = windAffectednessAtPos;
                    bps.basePos.X = pos.X + basePos[i].X;
                    bps.basePos.Y = pos.InternalY + basePos[i].Y;
                    bps.basePos.Z = pos.Z + basePos[i].Z;

                    manager.Spawn(bps);
                }

                return;
            }

            base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                api.Logger.Warning("OnBlockInteractStart => !blockSel && !Claims.TryAccess");
                return false;
            }

            int stage = Stage;
            api.Logger.Warning($"OnBlockInteractStart Stage: {stage}");
            
            ItemStack stack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;

            if (stage == 5)
            {
                BlockEntityBrazier beBrazier = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBrazier;
                
                if (beBrazier!=null && stack?.Block != null && stack.Block.HasBehavior<BlockBehaviorCanIgnite>() && beBrazier.GetIgnitableState(0) == EnumIgniteState.Ignitable)
                {
                    return false;
                }

                if (beBrazier != null && stack != null)
                {
                    bool activated = false;

                    if (byPlayer.Entity.Controls.ShiftKey)
                    {
                        if (stack.Collectible.CombustibleProps != null && stack.Collectible.CombustibleProps.MeltingPoint > 0)
                        {
                            ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, 1);
                            byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(beBrazier.inputSlot, ref op);
                            if (op.MovedQuantity > 0) activated = true;
                        }

                        if (stack.Collectible.CombustibleProps != null && stack.Collectible.CombustibleProps.BurnTemperature > 0)
                        {
                            ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, 1);
                            byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(beBrazier.fuelSlot, ref op);
                            if (op.MovedQuantity > 0) activated = true;
                        }
                    }

                    if (stack.Collectible.Attributes?.IsTrue("mealContainer") == true && !activated)
                    {
                        ItemSlot potSlot = null;
                        if (beBrazier.inputStack?.Collectible is BlockCookedContainer)
                        {
                            potSlot = beBrazier.inputSlot;
                        }
                        if (beBrazier.outputStack?.Collectible is BlockCookedContainer)
                        {
                            potSlot = beBrazier.outputSlot;
                        }

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
                                {
                                    world.SpawnItemEntity(targetSlot.Itemstack, byPlayer.Entity.ServerPos.XYZ);
                                }
                            }
                            else blockPot.ServeIntoStack(targetSlot, potSlot, world);
                        }
                        else if (!beBrazier.inputSlot.Empty || byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(api.World, beBrazier.inputSlot, 1) == 0)
                        {
                            beBrazier.OnPlayerRightClick(byPlayer, blockSel);
                        }

                        activated = true;
                    }

                    if (stack?.Collectible is BlockSmeltingContainer or BlockSmeltedContainer && !activated)
                    {
                        if (byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(api.World, beBrazier.inputSlot, 1) > 0) activated = true;
                    }

                    if (activated)
                    {
                        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                        var loc = stack.ItemAttributes?["placeSound"].Exists == true ? AssetLocation.Create(stack.ItemAttributes["placeSound"].AsString(), stack.Collectible.Code.Domain) : null;

                        if (loc != null)
                        {
                            api.World.PlaySoundAt(loc.WithPathPrefixOnce("sounds/"), blockSel.Position.X, blockSel.Position.InternalY, blockSel.Position.Z, byPlayer, 0.88f + (float)api.World.Rand.NextDouble() * 0.24f, 16);
                        }

                        return true;
                    }
                }



                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }
            
            if (stack != null && TryConstruct(world, blockSel.Position, stack.Collectible, byPlayer))
            {
                if (byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                }
                return true;
            }


            return false;
        }

        public bool TryConstruct(IWorldAccessor world, BlockPos pos, CollectibleObject obj, IPlayer player) {
            int stage = Stage;

            api.Logger.Warning($"TryConstruct Stage: {stage}");
            
            if (obj.Attributes?.IsTrue("firepitConstructable") != true) return false;

            if (stage == 5) return false;

            /*
            if (stage == 4 && IsFirewoodPile(world, pos.DownCopy()))
            {
                Block charcoalPitBlock = world.GetBlock(new AssetLocation("charcoalpit"));
                if (charcoalPitBlock != null)
                {
                    world.BlockAccessor.SetBlock(charcoalPitBlock.BlockId, pos);

                    BlockEntityCharcoalPit be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCharcoalPit;
                    be?.Init(player);

                    (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    return true;
                }
            }
            */

            Block block = world.GetBlock(CodeWithParts(NextStageCodePart));
            world.BlockAccessor.ExchangeBlock(block.BlockId, pos);
            world.BlockAccessor.MarkBlockDirty(pos);
            if (block.Sounds != null) world.PlaySoundAt(block.Sounds.Place, pos, -0.5, player);

            if (stage == 4)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntityBrazier)
                {
                    ((BlockEntityBrazier)be).inventory[0].Itemstack = new ItemStack(obj, 4);
                }
            }

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }

        /*
        public static bool IsFirewoodPile(IWorldAccessor world, BlockPos pos)
        {
            var beg = world.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
            return beg != null && beg.Inventory[0]?.Itemstack?.Collectible is ItemFirewood;
        }

        public static int GetFireWoodQuanity(IWorldAccessor world, BlockPos pos)
        {
            var beg = world.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
            return beg?.Inventory[0]?.StackSize ?? 0;
        }
        */

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
        {
            if (creatureType == EnumAICreatureType.LandCreature || creatureType == EnumAICreatureType.Humanoid)
            {
                return GetBlockEntity<BlockEntityBrazier>(pos)?.IsBurning == true ? 10000f : 1f;
            }

            return base.GetTraversalCost(pos, creatureType);
        }

        public bool EmitsSmoke(BlockPos pos)
        {
            var bebrazier = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrazier;
            return bebrazier?.IsBurning == true;
        }
    }
}
