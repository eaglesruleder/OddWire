using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using OddWire.VintageStory.API.Common;

#nullable disable

namespace OddWire.GameContent
{
    public class BlockEntityBrazier : BlockEntityOpenableContainer, IFirePit, IHeatSource, ITemperatureSensitive
    {
        public virtual string ShapePath => "oddwire:shapes/block/metal/brazier/";
        public virtual string CacheKey => "brazier-meshes";
        
        #region BlockEntityContainer
        internal InventorySmelting inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "stove";
        
        public ItemSlot FuelSlot => inventory[0];
        public ItemStack FuelStack
        {
            get { return inventory[0].Itemstack; }
            set { inventory[0].Itemstack = value; inventory[0].MarkDirty(); }
        }
        
        public ItemSlot InputSlot => inventory[1];
        public ItemStack InputStack
        {
            get { return inventory[1].Itemstack; }
            set { inventory[1].Itemstack = value; inventory[1].MarkDirty(); }
        }

        public ItemSlot OutputSlot => inventory[2];
        public ItemStack OutputStack
        {
            get { return inventory[2].Itemstack; }
            set { inventory[2].Itemstack = value; inventory[2].MarkDirty(); }
        }
        #endregion

        #region IBrazier
        public bool IsBurning => burnRemaining > 0;
        public bool IsWide => CurrentModel == EnumFirepitModel.Wide;
        #endregion
        
        
        #region IHeatSource
        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return IsBurning ? 10 : (CanIgniteFuel ? 0.25f : 0);
        }
        #endregion

        #region ITemperatureSensitive
        public bool IsHot => IsBurning;
        public void CoolNow(float amountRel)
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, -0.5, null, false, 16);

            burnRemaining -= amountRel / 10f;

            if (burnRemaining <= 0
            ||  Api.World.Rand.NextDouble() < amountRel / 5f
                )
            {
                SetBlockState("cold");
                extinguishedTotalHours = -99;
                CanIgniteFuel = false;
                burnRemaining = 0;
                _burnProps = null;
            }

            MarkDirty(true);
        }
        #endregion

        private CombustibleProperties _burnProps;
        public virtual float BurnTempModifier => 1;
        public virtual float BurnDurationModifier => 1;
        
        
        public float InputStackTemp
        {   get => GetTemp(InputStack);
            set => SetTemp(InputStack, value);
        }

        public float OutputStackTemp
        {   get => GetTemp(OutputStack);
            set => SetTemp(OutputStack, value);
        }

        private float GetTemp(ItemStack stack)
        {
            if (stack == null)
                return enviromentTemperature;

            if (inventory.CookingSlots.Length <= 0)
                return stack.Collectible.GetTemperature(Api.World, stack);
            
            bool haveStack = false;
            float lowestTemp = 0;
            for (int i = 0; i < inventory.CookingSlots.Length; i++)
            {
                ItemStack cookingStack = inventory.CookingSlots[i].Itemstack;
                if (cookingStack == null)
                    continue;
                
                float stackTemp = cookingStack.Collectible.GetTemperature(Api.World, cookingStack);
                lowestTemp = haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp;
                haveStack = true;
            }

            return lowestTemp;
        }

        void SetTemp(ItemStack stack, float value)
        {
            if (stack == null)
                return;
            
            if (inventory.CookingSlots.Length > 0)
                for (int i = 0; i < inventory.CookingSlots.Length; i++)
                    inventory.CookingSlots[i].Itemstack?.Collectible.SetTemperature(Api.World, inventory.CookingSlots[i].Itemstack, value);
            else
                stack.Collectible.SetTemperature(Api.World, stack, value);
        }
        
        
        public EnumFirepitModel CurrentModel { get; private set; }
        
        // Current temperature of the furnace
        public float furnaceTemperature = 20;
        
        // Resting temperature
        public virtual int enviromentTemperature => 20;
        
        
        // If true, then the fire pit is currently hot enough to ignite fuel-
        public bool CanIgniteFuel;
        
        public virtual bool BurnsAllFuel => true;
        
        public float emptyBrazierBurnTimeMulBonus = 4f;
        
        // How much of the current fuel is consumed
        public float burnRemaining;
        
        
        // For how long the ore has been cooking
        public float inputStackCookingTime;
        
        public double extinguishedTotalHours;
        
        
        BrazierContentsRenderer renderer;
        
        GuiDialogBlockEntityBrazier clientDialog;
        public virtual string DialogTitle => Lang.Get("Brazier");
        
        
        public BlockEntityBrazier()
        {
            inventory = new InventorySmelting(null, null);
            inventory.SlotModified += OnSlotModified;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.pos = Pos;
            inventory.LateInitialize("smelting-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

            RegisterGameTickListener(OnBurnTick, 100);
            RegisterGameTickListener(OnClientSync, 500);

            if (api is ICoreClientAPI clientApi)
            {
                renderer = new BrazierContentsRenderer(clientApi, Pos);
                clientApi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "brazier-contents");

                UpdateRenderer();
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                toggleInventoryDialogClient(byPlayer, () => {
                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                    SetDialogValues(dtree);
                    clientDialog = new GuiDialogBlockEntityBrazier(DialogTitle, Inventory, Pos, dtree, Api as ICoreClientAPI);
                    return clientDialog;
                });
            }

            return true;
        }
        
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
            renderer = null;

            if (clientDialog == null)
                return;
            
            clientDialog.TryClose();
            clientDialog?.Dispose();
            clientDialog = null;
        }
        
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();
        }
        
        
        private bool shouldRedraw;
        private void OnSlotModified(int slotid)
        {
            Block = Api.World.BlockAccessor.GetBlock(Pos);

            UpdateRenderer();
            MarkDirty(Api.Side == EnumAppSide.Server); // Save useless triple-remesh by only letting the server decide when to redraw
            shouldRedraw = true;

            if (Api is ICoreClientAPI
            &&  clientDialog != null
                )
                SetDialogValues(clientDialog.Attributes);

            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }
        
        
        private void OnBurnTick(float dt)
        {
            // Only tick on the server and merely sync to client
            if (Api is ICoreClientAPI)
            {
                renderer?.contentStackRenderer?.OnUpdate(InputStackTemp);
                return;
            }

            OnBurnFuel(dt);

            // Too cold to ignite fuel after 2 hours
            if (!IsBurning)
                OnBurnExtinctGoesCold(dt);

            // Furnace is burning: Heat furnace
            if (IsBurning)
                furnaceTemperature = CalcTemperatureChange(furnaceTemperature, _burnProps?.BurnTemperature ?? 0, dt);

            // Ore follows furnace temperature
            OnBurnHeatInput(dt);
            OnBurnHeatOutput(dt);

            // Finished smelting? Turn to smelted item
            OnBurnSmeltItems();

            // Furnace is not burning and can burn: Ignite the fuel
            if (!IsBurning)
                OnBurnIgniteFuel();
            
            // Furnace is not burning: Cool down furnace and ore also turn of fire
            if (!IsBurning)
                furnaceTemperature = CalcTemperatureChange(furnaceTemperature, enviromentTemperature, dt);
        }
        
        public float CalcTemperatureChange(float fromTemp, float toTemp, float dt)
        {
            float diff = Math.Abs(fromTemp - toTemp);
            dt += dt * (diff / 28);

            if (diff < dt)
                return toTemp;

            if (fromTemp > toTemp)
                dt = -dt;

            if (Math.Abs(fromTemp - toTemp) < 1)
                return toTemp;
            return fromTemp + dt;
        }
        
        private void OnBurnFuel(float dt)
        {
            if (burnRemaining <= 0)
                return;

            float burnBonus = 1;
            if (InputSlot.Empty
            &&  Math.Abs(furnaceTemperature - (_burnProps?.BurnTemperature ?? 0)) < 50
                )
                burnBonus = emptyBrazierBurnTimeMulBonus;

            burnRemaining -= dt / burnBonus;
            if (burnRemaining > 0)
                return;
            
            burnRemaining = 0;
            _burnProps = null;
            if (!CanSmelt) // This check avoids light flicker when a piece of fuel is consumed and more is available
            {
                SetBlockState("extinct");
                extinguishedTotalHours = Api.World.Calendar.TotalHours;
            }
        }

        private void OnBurnExtinctGoesCold(float dt)
        {
            if (Block.Variant["burnstate"] == "extinct"
            &&  Api.World.Calendar.TotalHours - extinguishedTotalHours > 2
                )
            {
                CanIgniteFuel = false;
                SetBlockState("cold");
            }
        }
        
        public bool CanHeatInput => 
            CanSmeltInput
        ||  InputStack?.ItemAttributes?["allowHeating"]?.AsBool() == true;
        
        private void OnBurnHeatInput(float dt)
        {
            if (!CanHeatInput)
            {
                inputStackCookingTime = 0;
                return;
            }
            
            float currTemp = InputStackTemp;
            float meltingPoint = InputStack.Collectible.GetMeltingPoint(Api.World, inventory, InputSlot);

            // Only Heat ore. Cooling happens already in the itemstack
            if (currTemp < furnaceTemperature)
            {
                float f = (1 + GameMath.Clamp((furnaceTemperature - currTemp) / 30, 0, 1.6f)) * dt;
                if (currTemp >= meltingPoint)
                    f /= 11;

                float newTemp = CalcTemperatureChange(currTemp, furnaceTemperature, f);
                int maxTemp = Math.Max(InputStack.Collectible.CombustibleProps?.MaxTemperature ?? 0, InputStack.ItemAttributes?["maxTemperature"]?.AsInt(0) ?? 0);
                if (maxTemp > 0)
                    newTemp = Math.Min(maxTemp, newTemp);
                
                currTemp = newTemp;
                InputStackTemp = newTemp;
            }

            // Begin smelting when hot enough
            if (currTemp >= meltingPoint)
                inputStackCookingTime += GameMath.Clamp((int)(currTemp / meltingPoint), 1, 30) * dt;
            else
            if (inputStackCookingTime > 0)
                inputStackCookingTime--;
        }
        
        public bool CanHeatOutput =>
            OutputStack?.ItemAttributes?["allowHeating"]?.AsBool() == true;
        
        public void OnBurnHeatOutput(float dt)
        {
            if (!CanHeatOutput)
                return;
            
            float currTemp = OutputStackTemp;

            // Only Heat ore. Cooling happens already in the itemstack
            if (currTemp >= furnaceTemperature)
                return;
            
            float newTemp = CalcTemperatureChange(currTemp, furnaceTemperature, 2 * dt);
            int maxTemp = Math.Max(OutputStack.Collectible.CombustibleProps?.MaxTemperature ?? 0, OutputStack.ItemAttributes?["maxTemperature"]?.AsInt(0) ?? 0);
            if (maxTemp > 0)
                newTemp = Math.Min(maxTemp, newTemp);
            
            OutputStackTemp = newTemp;
        }
        
        public bool CanSmeltInput
        { get {
            if (InputStack == null)
                return false;

            if (InputStack.Collectible.OnSmeltAttempt(inventory))
                MarkDirty(true);

            return
                InputStack.Collectible.CanSmelt(Api.World, inventory, InputStack, OutputStack)
            &&  InputStack.Collectible.CombustibleProps?.RequiresContainer != true;
        } }
        
        private bool CanSmelt
        { get
        {
            CombustibleProperties burnProps =
                FuelStack?.Collectible.CombustibleProps
            ??  InputStack?.Collectible.CombustibleProps;
            if (burnProps == null)
                return false;

            return
                (BurnsAllFuel || CanHeatInput)
                // Require fuel
            &&  burnProps.BurnTemperature > 0;
        } }
        
        public void OnBurnSmeltItems()
        {
            float maxCookingTime =
                InputSlot?.Itemstack?.Collectible?.GetMeltingDuration(Api.World, inventory, InputSlot)
            ??  30;
            
            if (inputStackCookingTime <= maxCookingTime
            || !CanSmeltInput
               )
                return;
            
            InputStack.Collectible.DoSmelt(Api.World, inventory, InputSlot, OutputSlot);
            InputStackTemp = enviromentTemperature;
            inputStackCookingTime = 0;
            MarkDirty(true);
            InputSlot.MarkDirty();
        }
        
        public void OnBurnIgniteFuel()
        {
            if (!CanIgniteFuel || !CanSmelt)
                return;

            var consumeInput =
                (InputStack?.Collectible.CombustibleProps?.CanBurn() ?? false)
            &&  (FuelSlot.Empty
            ||   Api.World.Rand.NextDouble() > 0.5
                );
            var consumeStack = consumeInput ? InputStack : FuelStack;
            
            _burnProps = consumeStack.Collectible.CombustibleProps.Clone();
            _burnProps.BurnDuration *= BurnDurationModifier;
            _burnProps.BurnTemperature = (int)(_burnProps.BurnTemperature * BurnTempModifier);
            
            burnRemaining = _burnProps.BurnDuration;
            SetBlockState("lit");
            MarkDirty(true);

            consumeStack.StackSize -= 1;
            if (consumeStack.StackSize <= 0)
            {
                if (consumeInput)
                    InputStack = null;
                else
                    FuelStack = null;
            }
        }
        
        // Temperature before the half second tick
        public float prevFurnaceTemperature = 20;
        private void OnClientSync(float dt)
        {
            if (Api is ICoreServerAPI
            && (IsBurning || (int)prevFurnaceTemperature != (int)furnaceTemperature)
               )
                MarkDirty();

            prevFurnaceTemperature = furnaceTemperature;
        }
        
        public void SetBlockState(string state)
        {
            AssetLocation loc = Block.CodeWithVariant("burnstate", state);
            Block block = Api.World.GetBlock(loc);
            if (block == null)
                return;

            Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
            Block = block;
        }
        
        InFirePitProps GetRenderProps(ItemStack contentStack)
        {
            if (contentStack?.ItemAttributes?.KeyExists("inFirePitProps") == true)
            {
                InFirePitProps props = contentStack.ItemAttributes["inFirePitProps"].AsObject<InFirePitProps>();
                props.Transform.EnsureDefaultValues();

                return props;
            }
            return null;
        }
        
        private BlockEntityFirepit EmulateBEFirepit => new BlockEntityFirepit()
            {Pos = Pos
            };
        
        void UpdateRenderer()
        {
            if (renderer == null)
                return;

            ItemStack contentStack = InputStack ?? OutputStack;

            bool useOldRenderer =
                renderer.ContentStack != null
            &&  renderer.contentStackRenderer != null
            &&  contentStack?.Collectible is IInFirepitRendererSupplier
            &&  renderer.ContentStack.Equals(Api.World, contentStack, GlobalConstants.IgnoredStackAttributes);

            if (useOldRenderer)
                return; // Otherwise the cooking sounds restarts all the time

            renderer.contentStackRenderer?.Dispose();
            renderer.contentStackRenderer = null;

            if (contentStack?.Collectible is IInFirepitRendererSupplier contentRenderSupplier)
            {
                IInFirepitRenderer childrenderer = contentRenderSupplier.GetRendererWhenInFirepit(contentStack, EmulateBEFirepit, contentStack == OutputStack);
                if (childrenderer != null)
                {
                    renderer.SetChildRenderer(contentStack, childrenderer);
                    return;
                }
            }

            InFirePitProps props = GetRenderProps(contentStack);
            if (contentStack?.Collectible != null
            &&!(contentStack?.Collectible is IInFirepitMeshSupplier)
            &&  props != null
                )
                renderer.SetContents(contentStack, props.Transform);
            else
                renderer.SetContents(null, null);
        }
        
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            ItemSlot contentSlot = InputSlot ?? OutputSlot;
            ItemStack contentStack = InputStack ?? OutputStack;
            MeshData contentmesh = GetContentMesh(contentStack, tesselator);
            if (contentmesh != null)
                mesher.AddMeshData(contentmesh);
            
            string burnState = Block.Variant["burnstate"];
            if (burnState == null)
                return true;

            bool fuelHasCombustible = FuelStack?.Collectible?.CombustibleProps.CanBurn() ?? false;
            bool contentHasCombustible = contentStack?.Collectible?.CombustibleProps.CanBurn() ?? false;
            bool contentHasItem = !contentSlot.Empty && !contentHasCombustible;

            // If we're cold and have no combustible at all, treat as extinct for visuals
            if (burnState == "cold" && !(fuelHasCombustible || contentHasCombustible))
                burnState = "extinct";
            
            if (fuelHasCombustible && !contentHasItem)
            {
                var firewoodMesh = this.CacheMesh($"{ShapePath}firewood/{burnState}-normal", CacheKey);
                firewoodMesh.Translate(new Vec3f(0, 3f / 16f, 0));
                mesher.AddMeshData(firewoodMesh);
            }

            if (contentHasCombustible || contentHasItem)
            {
                var firewoodMesh = this.CacheMesh($"{ShapePath}firewood/{burnState}-wide", CacheKey);
                firewoodMesh.Translate(new Vec3f(0, 3f / 16f, 0));
                mesher.AddMeshData(firewoodMesh);
            }
            
            mesher.AddMeshData(this.CacheMesh($"{ShapePath}parts/brazier", CacheKey));

            return true;
        }
        
        private MeshData GetContentMesh(ItemStack contentStack, ITesselatorAPI tesselator)
        {
            CurrentModel = EnumFirepitModel.Normal;

            if (contentStack == null)
                return null;

            if (contentStack.Collectible is IInFirepitMeshSupplier contentMeshSupplier)
            {
                EnumFirepitModel model = EnumFirepitModel.Normal;
                MeshData mesh = contentMeshSupplier.GetMeshWhenInFirepit(contentStack, Api.World, Pos, ref model);
                CurrentModel = model;

                if (mesh != null)
                    return mesh;
            }

            if (contentStack.Collectible is IInFirepitRendererSupplier contentRendererSupplier)
            {
                EnumFirepitModel model = contentRendererSupplier.GetDesiredFirepitModel(contentStack, EmulateBEFirepit, contentStack == OutputStack);
                CurrentModel = model;
                return null;
            }

            InFirePitProps renderProps = GetRenderProps(contentStack);
            if (renderProps == null)
            {
                if (renderer.RequireSpit)
                    CurrentModel = EnumFirepitModel.Spit;
                return null; // Mesh drawing is handled by the BrazierContentsRenderer
            }
            
            CurrentModel = renderProps.UseFirepitModel;
            if (contentStack.Class == EnumItemClass.Item)
                return null;
            
            tesselator.TesselateBlock(contentStack.Block, out MeshData ingredientMesh);

            ingredientMesh.ModelTransform(renderProps.Transform);

            // Lower by 1 voxel if extinct
            if(!IsBurning
            &&  renderProps.UseFirepitModel != EnumFirepitModel.Spit
                )
                ingredientMesh.Translate(0, -1 / 16f, 0);

            return ingredientMesh;
        }
        
        
        private void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("furnaceTemperature", furnaceTemperature);

            dialogTree.SetInt("maxTemperature", _burnProps?.BurnTemperature ?? 0);
            dialogTree.SetFloat("oreCookingTime", inputStackCookingTime);
            dialogTree.SetFloat("maxFuelBurnTime", _burnProps?.BurnDuration ?? 0);
            dialogTree.SetFloat("fuelBurnTime", burnRemaining);

            if (InputStack == null)
                dialogTree.RemoveAttribute("oreTemperature");
            else
            {
                float meltingDuration = InputStack.Collectible.GetMeltingDuration(Api.World, inventory, InputSlot);

                dialogTree.SetFloat("oreTemperature", InputStackTemp);
                dialogTree.SetFloat("maxOreCookingTime", meltingDuration);
            }
            
            dialogTree.SetString("outputText", inventory.GetOutputText());
            dialogTree.SetInt("haveCookingContainer", inventory.HaveCookingContainer ? 1 : 0);
            dialogTree.SetInt("quantityCookingSlots", inventory.CookingSlots.Length);
        }
        
        
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                (Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
                invDialog?.TryClose();
                invDialog?.Dispose();
                invDialog = null;
            }
        }
        
        
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("furnaceTemperature", furnaceTemperature);
            tree.SetFloat("oreCookingTime", inputStackCookingTime);
            tree.SetFloat("fuelBurnTime", burnRemaining);
            tree.SetCombustibleProps("burnProps", _burnProps);
            tree.SetDouble("extinguishedTotalHours", extinguishedTotalHours);
            tree.SetBool("canIgniteFuel", CanIgniteFuel);
        }
        
        bool clientSidePrevBurning;
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api != null)
                Inventory.AfterBlocksLoaded(Api.World);

            furnaceTemperature = tree.GetFloat("furnaceTemperature");
            inputStackCookingTime = tree.GetFloat("oreCookingTime");
            burnRemaining = tree.GetFloat("fuelBurnTime");
            _burnProps = tree.GetCombustibleProps("burnProps");
            extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours");
            CanIgniteFuel = tree.GetBool("canIgniteFuel", true);

            if (Api?.Side != EnumAppSide.Client)
                return;
            
            UpdateRenderer();

            if (clientDialog != null)
                SetDialogValues(clientDialog.Attributes);
            
            if (clientSidePrevBurning != IsBurning || shouldRedraw)
            {
                GetBehavior<BEBehaviorFirepitAmbient>().ToggleAmbientSounds(IsBurning);
                clientSidePrevBurning = IsBurning;
                MarkDirty(true);
                shouldRedraw = false;
            }
        }
        
        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null)
                    continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                else
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;

                slot.Itemstack.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
            }

            foreach (ItemSlot slot in inventory.CookingSlots)
            {
                if (slot.Itemstack == null)
                    continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                else
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;

                slot.Itemstack.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
            }
        }
    }
}