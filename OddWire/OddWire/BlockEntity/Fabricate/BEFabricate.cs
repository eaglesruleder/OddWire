using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable disable

namespace OddWire.GameContent
{
    public class BlockEntityFabricate : BlockEntity
    {
        InventoryGeneric fabricationInventory;
        FabricationResolvedRecipe fabricationRecipe;
        string fabricationRecipePattern;
        int fabricationStepIndex;
        int fabricationHammerHitsRemaining;

        public BlockEntityFabricate()
        {
            fabricationInventory = new InventoryGeneric(16, null, null);
            fabricationInventory.SlotModified += OnSlotModified;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            fabricationInventory.LateInitialize("fabrication-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

        }

        void OnSlotModified(int slotId)
        {
            MarkDirty(Api?.Side == EnumAppSide.Server);
        }

        public bool TryHandleFabricationInteraction(IPlayer byPlayer, ItemSlot activeSlot)
        {
            if (activeSlot?.Itemstack == null) return false;

            ResolveFabricationRecipe();
            if (fabricationRecipe == null) return false;

            if (fabricationHammerHitsRemaining > 0)
            {
                if (!IsHammer(activeSlot.Itemstack)) return false;
                fabricationHammerHitsRemaining--;
                if (fabricationHammerHitsRemaining <= 0)
                {
                    fabricationStepIndex++;
                    TryCompleteFabrication();
                }

                MarkDirty(true);
                return true;
            }

            if (!fabricationRecipe.MatchesStep(activeSlot.Itemstack, fabricationStepIndex, Api))
            {
                return false;
            }

            float requiredTemp = fabricationRecipe.GetRequiredTemperature(fabricationStepIndex);
            if (requiredTemp > 0 && activeSlot.Itemstack.Collectible.GetTemperature(Api.World, activeSlot.Itemstack) < requiredTemp)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "fabrication-toocold", Lang.GetWithFallback("fabrication-toocold", "That part needs to be heated before it can be added."));
                return false;
            }

            ItemStack placed = activeSlot.TakeOut(1);
            activeSlot.MarkDirty();
            if (placed == null) return false;

            fabricationInventory[fabricationStepIndex].Itemstack = placed;
            fabricationInventory[fabricationStepIndex].MarkDirty();

            int requiredHits = fabricationRecipe.GetRequiredHammerHits(fabricationStepIndex);
            if (requiredHits > 0)
            {
                fabricationHammerHitsRemaining = requiredHits;
            }
            else
            {
                fabricationStepIndex++;
            }

            TryCompleteFabrication();
            MarkDirty(true);
            return true;
        }

        void ResolveFabricationRecipe()
        {
            if (fabricationRecipe is not null)
            {
                return;
            }

            OddWireModSystem modSystem = Api?.ModLoader.GetModSystem<OddWireModSystem>();
            if (modSystem?.FabricationRecipes == null)
            {
                return;
            }

            string patternFromBlock = Block?.Attributes?["fabricationRecipePattern"].AsString(null);
            if (!string.IsNullOrWhiteSpace(patternFromBlock))
            {
                fabricationRecipePattern ??= patternFromBlock;
            }

            if (!string.IsNullOrWhiteSpace(fabricationRecipePattern))
            {
                fabricationRecipe = modSystem.FabricationRecipes.ResolveFor(Block, fabricationRecipePattern);
            }

            fabricationRecipe ??= modSystem.FabricationRecipes.ResolveFor(Block);
            if (fabricationRecipe != null && !string.IsNullOrWhiteSpace(fabricationRecipePattern))
            {
                fabricationRecipePattern = fabricationRecipe.Pattern;
            }
        }

        void TryCompleteFabrication()
        {
            if (fabricationRecipe == null) return;
            if (fabricationHammerHitsRemaining > 0) return;
            if (fabricationStepIndex < fabricationRecipe.Steps.Length) return;

            ItemStack output = fabricationRecipe.CreateOutputStack(Api.World);
            if (output == null) return;

            ClearFabricationState();
            MarkDirty(true);

            if (output.Class == EnumItemClass.Block && output.Block != null)
            {
                Api.World.BlockAccessor.ExchangeBlock(output.Block.BlockId, Pos);
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                return;
            }

            Api.World.SpawnItemEntity(output, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        void ClearFabricationState()
        {
            fabricationStepIndex = 0;
            fabricationHammerHitsRemaining = 0;
            fabricationRecipe = null;
            fabricationRecipePattern = null;
            for (int i = 0; i < fabricationInventory.Count; i++)
            {
                fabricationInventory[i].Itemstack = null;
            }
        }

        static bool IsHammer(ItemStack stack)
        {
            return stack?.Collectible?.Tool == EnumTool.Hammer;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (tree.GetTreeAttribute("fabrication") != null)
            {
                fabricationInventory.FromTreeAttributes(tree.GetTreeAttribute("fabrication"));
            }

            fabricationStepIndex = tree.GetInt("fabricationStepIndex");
            fabricationHammerHitsRemaining = tree.GetInt("fabricationHammerHitsRemaining");
            fabricationRecipePattern = tree.GetString("fabricationRecipePattern");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            ITreeAttribute craftingTree = new TreeAttribute();
            fabricationInventory.ToTreeAttributes(craftingTree);
            tree["fabrication"] = craftingTree;
            tree.SetInt("fabricationStepIndex", fabricationStepIndex);
            tree.SetInt("fabricationHammerHitsRemaining", fabricationHammerHitsRemaining);
            tree.SetString("fabricationRecipePattern", fabricationRecipePattern);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
        }
    }
}
