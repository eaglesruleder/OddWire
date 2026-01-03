using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable

namespace OddWire.GameContent
{
    public class BlockEntityBrazier : BlockEntityOpenableContainer, IBrazier, IHeatSource, ITemperatureSensitive
    {
        #region Expose for BlockBrazier
        public EnumIgniteState GetIgnitableState(float secondsIgniting)
        {
            if (IsBurning
            ||  fuelSlot.Empty
                ) return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }

        public EnumBrazierModel CurrentModel { get; private set; }
        #endregion
        
        #region BlockEntityContainer
        internal InventorySmelting inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "stove";
        
        public ItemSlot fuelSlot => inventory[0];
        public ItemStack fuelStack
        {
            get { return inventory[0].Itemstack; }
            set { inventory[0].Itemstack = value; inventory[0].MarkDirty(); }
        }
        
        public CombustibleProperties fuelCombustibleOpts => getCombustibleOpts(0);
        public CombustibleProperties getCombustibleOpts(int slotid) =>
            inventory[slotid].Itemstack?.Collectible.CombustibleProps;
        
        public ItemSlot inputSlot => inventory[1];
        public ItemStack inputStack
        {
            get { return inventory[1].Itemstack; }
            set { inventory[1].Itemstack = value; inventory[1].MarkDirty(); }
        }

        public ItemSlot outputSlot => inventory[2];
        public ItemStack outputStack
        {
            get { return inventory[2].Itemstack; }
            set { inventory[2].Itemstack = value; inventory[2].MarkDirty(); }
        }
        #endregion
        
        public bool IsBurning => fuelBurnTime > 0;
        
        #region IHeatSource
        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return IsBurning ? 10 : (canIgniteFuel ? 0.25f : 0);
        }
        #endregion

        #region ITemperatureSensitive
        public bool IsHot => IsBurning;
        public void CoolNow(float amountRel)
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, -0.5, null, false, 16);

            fuelBurnTime -= amountRel / 10f;

            if (fuelBurnTime <= 0
            ||  Api.World.Rand.NextDouble() < amountRel / 5f
                )
            {
                setBlockState("cold");
                extinguishedTotalHours = -99;
                canIgniteFuel = false;
                fuelBurnTime = 0;
                maxFuelBurnTime = 0;
            }

            MarkDirty(true);
        }
        #endregion

        
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
                ModelTransform contentTransform = CreateBrazierContentTransform();
                contentsRenderer = new BrazierContentsRenderer(clientApi, Pos, contentTransform, Vec3f.Zero);
                clientApi.Event.RegisterRenderer(contentsRenderer, EnumRenderStage.Opaque, "brazier-contents");
                
                fuelRenderer = new StackContentsRenderer(clientApi, Pos);
                clientApi.Event.RegisterRenderer(fuelRenderer, EnumRenderStage.Opaque, "brazier-fuel");

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

            contentsRenderer?.Dispose();
            contentsRenderer = null;
            fuelRenderer?.Dispose();
            fuelRenderer = null;

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                clientDialog?.Dispose();
                clientDialog = null;
            }
        }
        
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            contentsRenderer?.Dispose();
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
                contentsRenderer?.contentStackRenderer?.OnUpdate(InputStackTemp);
                return;
            }

            OnBurnFuel(dt);

            // Too cold to ignite fuel after 2 hours
            if (!IsBurning)
                OnBurnExtinctGoesCold(dt);

            // Furnace is burning: Heat furnace
            if (IsBurning)
                furnaceTemperature = CalcTemperatureChange(furnaceTemperature, maxTemperature, dt);

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
            if (fuelBurnTime <= 0)
                return;

            float burnBonus = 1;
            if (inputSlot.Empty
            &&  Math.Abs(furnaceTemperature - maxTemperature) < 50
                )
                burnBonus = emptyBrazierBurnTimeMulBonus;

            fuelBurnTime -= dt / burnBonus;
            if (fuelBurnTime > 0)
                return;
            
            fuelBurnTime = 0;
            maxFuelBurnTime = 0;
            if (!CanSmelt) // This check avoids light flicker when a piece of fuel is consumed and more is available
            {
                setBlockState("extinct");
                extinguishedTotalHours = Api.World.Calendar.TotalHours;
            }
        }

        private void OnBurnExtinctGoesCold(float dt)
        {
            if (Block.Variant["burnstate"] == "extinct"
            &&  Api.World.Calendar.TotalHours - extinguishedTotalHours > 2
                )
            {
                canIgniteFuel = false;
                setBlockState("cold");
            }
        }
        
        public bool CanHeatInput => 
            CanSmeltInput
        ||  inputStack?.ItemAttributes?["allowHeating"]?.AsBool() == true;
        
        private void OnBurnHeatInput(float dt)
        {
            if (!CanHeatInput)
            {
                inputStackCookingTime = 0;
                return;
            }
            
            float currTemp = InputStackTemp;
            float meltingPoint = inputStack.Collectible.GetMeltingPoint(Api.World, inventory, inputSlot);

            // Only Heat ore. Cooling happens already in the itemstack
            if (currTemp < furnaceTemperature)
            {
                float f = (1 + GameMath.Clamp((furnaceTemperature - currTemp) / 30, 0, 1.6f)) * dt;
                if (currTemp >= meltingPoint)
                    f /= 11;

                float newTemp = CalcTemperatureChange(currTemp, furnaceTemperature, f);
                int maxTemp = Math.Max(inputStack.Collectible.CombustibleProps?.MaxTemperature ?? 0, inputStack.ItemAttributes?["maxTemperature"]?.AsInt(0) ?? 0);
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
            outputStack?.ItemAttributes?["allowHeating"]?.AsBool() == true;
        
        public void OnBurnHeatOutput(float dt)
        {
            if (!CanHeatOutput)
                return;
            
            float currTemp = OutputStackTemp;

            // Only Heat ore. Cooling happens already in the itemstack
            if (currTemp >= furnaceTemperature)
                return;
            
            float newTemp = CalcTemperatureChange(currTemp, furnaceTemperature, 2 * dt);
            int maxTemp = Math.Max(outputStack.Collectible.CombustibleProps?.MaxTemperature ?? 0, outputStack.ItemAttributes?["maxTemperature"]?.AsInt(0) ?? 0);
            if (maxTemp > 0)
                newTemp = Math.Min(maxTemp, newTemp);
            
            OutputStackTemp = newTemp;
        }
        
        public bool CanSmeltInput
        { get {
            if (inputStack == null)
                return false;

            if (inputStack.Collectible.OnSmeltAttempt(inventory))
                MarkDirty(true);

            return
                inputStack.Collectible.CanSmelt(Api.World, inventory, inputStack, outputStack)
            &&  inputStack.Collectible.CombustibleProps?.RequiresContainer != true;
        } }
        
        private bool CanSmelt
        { get {
            CombustibleProperties fuelCopts = fuelCombustibleOpts;
            if (fuelCopts == null)
                return false;

            return
                (BurnsAllFuel || CanHeatInput)
                // Require fuel
            &&  fuelCopts.BurnTemperature * HeatModifier > 0;
        } }
        
        public void OnBurnSmeltItems()
        {
            float maxCookingTime =
                inputSlot?.Itemstack?.Collectible?.GetMeltingDuration(Api.World, inventory, inputSlot)
            ??  30;
            
            if (inputStackCookingTime <= maxCookingTime
            || !CanSmeltInput
               )
                return;
            
            inputStack.Collectible.DoSmelt(Api.World, inventory, inputSlot, outputSlot);
            InputStackTemp = enviromentTemperature;
            inputStackCookingTime = 0;
            MarkDirty(true);
            inputSlot.MarkDirty();
        }
        
        public void OnBurnIgniteFuel()
        {
            if (!canIgniteFuel || !CanSmelt)
                return;
            
            CombustibleProperties fuelCopts = fuelStack.Collectible.CombustibleProps;

            maxFuelBurnTime = fuelBurnTime = fuelCopts.BurnDuration * BurnDurationModifier;
            maxTemperature = (int)(fuelCopts.BurnTemperature * HeatModifier);
            setBlockState("lit");
            MarkDirty(true);

            fuelStack.StackSize -= 1;
            if (fuelStack.StackSize <= 0)
                fuelStack = null;
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

        
        // Current temperature of the furnace
        public float furnaceTemperature = 20;
        
        // Maximum temperature that can be reached with the currently used fuel
        public int maxTemperature;
        
        // How much of the current fuel is consumed
        public float fuelBurnTime;
        
        // How much fuel is available
        public float maxFuelBurnTime;
        
        /// If true, then the fire pit is currently hot enough to ignite fuel-
        public bool canIgniteFuel;
        
        
        // For how long the ore has been cooking
        public float inputStackCookingTime;
        
        public double extinguishedTotalHours;
        
        // Resting temperature
        public virtual int enviromentTemperature => 20;
        public virtual float HeatModifier => 1;
        public virtual float BurnDurationModifier => 1;
        public virtual bool BurnsAllFuel => true;
        
        
        
        public float emptyBrazierBurnTimeMulBonus = 4f;
        

        
        
        
        
        BrazierContentsRenderer contentsRenderer;
        StackContentsRenderer fuelRenderer;
        
        private static ModelTransform CreateBrazierFuelTransform()
        {
            ModelTransform transform = new ModelTransform().EnsureDefaultValues();
            transform.Origin.Set(0.5f, 1 / 16f, 0.5f);
            transform.Rotation.Set(90, 90, 0);
            transform.ScaleXYZ.Set(0.2f, 0.2f, 0.2f);
            return transform;
        }

        private static ModelTransform CreateBrazierContentTransform()
        {
            ModelTransform transform = new ModelTransform().EnsureDefaultValues();
            transform.Origin.Set(0.5f, 1 / 16f, 0.5f);
            transform.Rotation.Set(90, 90, 0);
            transform.Translation.Set(0, 0.25f, 0);
            transform.ScaleXYZ.Set(0.25f, 0.25f, 0.25f);
            return transform;
        }
        
        public void setBlockState(string state)
        {
            AssetLocation loc = Block.CodeWithVariant("burnstate", state);
            Block block = Api.World.GetBlock(loc);
            if (block == null)
                return;

            Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
            Block = block;
        }
        
        InBrazierProps GetRenderProps(ItemStack contentStack)
        {
            if (contentStack?.ItemAttributes?.KeyExists("inBrazierProps") == true)
            {
                InBrazierProps props = contentStack.ItemAttributes["inBrazierProps"].AsObject<InBrazierProps>();
                props.Transform.EnsureDefaultValues();

                return props;
            }
            return null;
        }
        
        void UpdateRenderer()
        {
            if (contentsRenderer == null)
                return;

            ItemStack contentStack = inputStack ?? outputStack;

            bool useOldRenderer =
                contentsRenderer.ContentStack != null
            &&  contentsRenderer.contentStackRenderer != null
            &&  contentStack?.Collectible is IInBrazierRendererSupplier
            &&  contentsRenderer.ContentStack.Equals(Api.World, contentStack, GlobalConstants.IgnoredStackAttributes);

            if (useOldRenderer)
                return; // Otherwise the cooking sounds restarts all the time

            contentsRenderer.contentStackRenderer?.Dispose();
            contentsRenderer.contentStackRenderer = null;

            if (contentStack?.Collectible is IInBrazierRendererSupplier contentRenderSupplier)
            {
                IInBrazierRenderer childrenderer = contentRenderSupplier.GetRendererWhenInBrazier(contentStack, this, contentStack == outputStack);
                if (childrenderer != null)
                {
                    contentsRenderer.SetChildRenderer(contentStack, childrenderer);
                    return;
                }
            }

            InBrazierProps props = GetRenderProps(contentStack);
            if (contentStack?.Collectible != null
            &&!(contentStack?.Collectible is IInBrazierMeshSupplier)
            &&  props != null
                )
                contentsRenderer.SetContents(contentStack, props.Transform);
            else
                contentsRenderer.SetContents(null, null);

            UpdateFuelRenderer();
        }
        
        void UpdateFuelRenderer()
        {
            if (fuelRenderer == null)
                return;

            ItemStack[] fuelStacks = GetFuelStacksForRender();
            if (fuelStacks == null || fuelStacks.Length == 0)
            {
                fuelRenderer.SetStacks(null, (ModelTransform)null, null);
                return;
            }

            fuelRenderer.SetStacks(fuelStacks, CreateBrazierFuelTransform(), GetFuelOffsets(fuelStacks.Length));
        }
        
        ItemStack[] GetFuelStacksForRender()
        {
            if (fuelSlot == null || fuelSlot.Empty) return null;
            return new[] { fuelStack };
        }

        static Vec3f[] GetFuelOffsets(int count)
        {
            if (count <= 0)
                return null;
            
            Vec3f[] offsets = new Vec3f[count];
            for (int i = 0; i < count; i++)
                offsets[i] = new Vec3f(0.5f, 0.1f, 0.5f);

            return offsets;
        }
        
        
        GuiDialogBlockEntityBrazier clientDialog;
        public virtual string DialogTitle => Lang.Get("Brazier");
        
        void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("furnaceTemperature", furnaceTemperature);

            dialogTree.SetInt("maxTemperature", maxTemperature);
            dialogTree.SetFloat("oreCookingTime", inputStackCookingTime);
            dialogTree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
            dialogTree.SetFloat("fuelBurnTime", fuelBurnTime);

            if (inputStack == null)
                dialogTree.RemoveAttribute("oreTemperature");
            else
            {
                float meltingDuration = inputStack.Collectible.GetMeltingDuration(Api.World, inventory, inputSlot);

                dialogTree.SetFloat("oreTemperature", InputStackTemp);
                dialogTree.SetFloat("maxOreCookingTime", meltingDuration);
            }
            
            dialogTree.SetString("outputText", inventory.GetOutputText());
            dialogTree.SetInt("haveCookingContainer", inventory.HaveCookingContainer ? 1 : 0);
            dialogTree.SetInt("quantityCookingSlots", inventory.CookingSlots.Length);
        }
        
        
        
        
        
        
        
        public float InputStackTemp
        {   get => GetTemp(inputStack);
            set => SetTemp(inputStack, value);
        }

        public float OutputStackTemp
        {   get => GetTemp(outputStack);
            set => SetTemp(outputStack, value);
        }

        float GetTemp(ItemStack stack)
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
                if (cookingStack != null)
                {
                    float stackTemp = cookingStack.Collectible.GetTemperature(Api.World, cookingStack);
                    lowestTemp = haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp;
                    haveStack = true;
                }
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
            tree.SetInt("maxTemperature", maxTemperature);
            tree.SetFloat("oreCookingTime", inputStackCookingTime);
            tree.SetFloat("fuelBurnTime", fuelBurnTime);
            tree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
            tree.SetDouble("extinguishedTotalHours", extinguishedTotalHours);
            tree.SetBool("canIgniteFuel", canIgniteFuel);
        }
        
        bool clientSidePrevBurning;
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api != null)
                Inventory.AfterBlocksLoaded(Api.World);

            furnaceTemperature = tree.GetFloat("furnaceTemperature");
            maxTemperature = tree.GetInt("maxTemperature");
            inputStackCookingTime = tree.GetFloat("oreCookingTime");
            fuelBurnTime = tree.GetFloat("fuelBurnTime");
            maxFuelBurnTime = tree.GetFloat("maxFuelBurnTime");
            extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours");
            canIgniteFuel = tree.GetBool("canIgniteFuel", true);

            if (Api?.Side != EnumAppSide.Client)
                return;
            
            UpdateRenderer();

            if (clientDialog != null)
                SetDialogValues(clientDialog.Attributes);
            
            if (clientSidePrevBurning != IsBurning || shouldRedraw)
            {
                GetBehavior<BEBehaviorBrazierAmbient>()?.ToggleAmbientSounds(IsBurning);
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
        
        
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            ItemStack contentStack = inputStack ?? outputStack;
            MeshData contentmesh = getContentMesh(contentStack, tesselator);
            if (contentmesh != null)
                mesher.AddMeshData(contentmesh);

            string burnState = Block.Variant["burnstate"];
            if (burnState == null)
                return true;
            
            string contentState = CurrentModel.ToString().ToLowerInvariant();
            if (burnState == "cold"
            &&  fuelSlot.Empty
                )
                burnState = "extinct";
            
            mesher.AddMeshData(getOrCreateMesh(burnState, contentState));

            return true;
        }
        
        private MeshData getContentMesh(ItemStack contentStack, ITesselatorAPI tesselator)
        {
            CurrentModel = EnumBrazierModel.Normal;

            if (contentStack == null)
                return null;

            if (contentStack.Collectible is IInBrazierMeshSupplier contentMeshSupplier)
            {
                EnumBrazierModel model = EnumBrazierModel.Normal;
                MeshData mesh = contentMeshSupplier.GetMeshWhenInBrazier(contentStack, Api.World, Pos, ref model);
                CurrentModel = model;

                if (mesh != null)
                    return mesh;
            }

            if (contentStack.Collectible is IInBrazierRendererSupplier contentRendererSupplier)
            {
                EnumBrazierModel model = contentRendererSupplier.GetDesiredBrazierModel(contentStack, this, contentStack == outputStack);
                CurrentModel = model;
                return null;
            }

            InBrazierProps renderProps = GetRenderProps(contentStack);
            if (renderProps == null)
            {
                if (contentsRenderer.RequireSpit)
                    CurrentModel = EnumBrazierModel.Spit;
                return null; // Mesh drawing is handled by the BrazierContentsRenderer
            }
            
            CurrentModel = renderProps.UseBrazierModel;
            if (contentStack.Class == EnumItemClass.Item)
                return null;
            
            tesselator.TesselateBlock(contentStack.Block, out MeshData ingredientMesh);

            ingredientMesh.ModelTransform(renderProps.Transform);

            // Lower by 1 voxel if extinct
            if(!IsBurning
            &&  renderProps.UseBrazierModel != EnumBrazierModel.Spit
                )
                ingredientMesh.Translate(0, -1 / 16f, 0);

            return ingredientMesh;
        }

        public MeshData getOrCreateMesh(string burnstate, string contentstate)
        {
            Dictionary<string, MeshData> Meshes = ObjectCacheUtil.GetOrCreate(Api, "brazier-meshes", () => new Dictionary<string, MeshData>());

            string key = burnstate + "-" + contentstate;
            if (!Meshes.TryGetValue(key, out MeshData meshdata))
            {
                Block block = Api.World.BlockAccessor.GetBlock(Pos);
                if (block.BlockId == 0)
                    return null;
                
                ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;
                mesher.TesselateShape(block, Shape.TryGet(Api, "oddwire:shapes/block/metal/brazier/" + key + ".json"), out meshdata);
            }

            return meshdata;
        }
    }
}