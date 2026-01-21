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
using OddWire.VintageStory.GameContent;

#nullable disable

namespace OddWire.GameContent
{
    public class FuelBurnStack
    {
        public string Key;
        public CombustibleProperties CombustibleProps;
        public GroundStorageProperties GSProps;
    }
    
    public class BlockEntityBrazier : BlockEntityOpenableContainer, IFirePit, IHeatSource, ITemperatureSensitive
    {
        private static Vec3f rotate90deg = new(0, 90, 0); 
        public virtual Vec3f ShortFuelTranslate => new(0, 3f / 16f, 0);
        public virtual Vec3f TallFuelTranslate => new(0, 8f / 16f, 0);
            
        public virtual string CacheKey => "brazier-meshes";

        public bool IsTall => Block.Variant["height"] == "tall";
        
        #region BlockEntityContainer
        internal InventoryBrazier inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "stove";
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
                _burnStack = null;
            }

            MarkDirty(true);
        }
        #endregion

        private int _burnFromSlot = 0;
        private FuelBurnStack _burnStack;
        public virtual float BurnTempModifier => 1;
        public virtual float BurnDurationModifier => 1;
        
        
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

        FuelRenderer _fuelShortNormalRenderer;
        FuelRenderer _fuelShortWideRenderer;
        FuelRenderer _fuelTallNormalRenderer;
        FuelRenderer _fuelTallWideRenderer;
        
        GuiDialogBlockEntityBrazier clientDialog;
        public virtual string DialogTitle => Lang.Get("Brazier");
        
        
        public BlockEntityBrazier()
        {
            inventory = new InventoryBrazier(null, null);
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

                _fuelShortNormalRenderer = new FuelRenderer("normal", ShortFuelTranslate);
                _fuelShortWideRenderer = new FuelRenderer("wide", ShortFuelTranslate);
                if (IsTall)
                {
                    _fuelTallNormalRenderer = new FuelRenderer("normal", TallFuelTranslate, rotate90deg, false);
                    _fuelTallWideRenderer = new FuelRenderer("wide", TallFuelTranslate, rotate90deg, false);
                }

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
                renderer?.contentStackRenderer?.OnUpdate(inventory.InputStackTemp);
                return;
            }

            OnBurnFuel(dt);

            // Too cold to ignite fuel after 2 hours
            if (!IsBurning)
                OnBurnExtinctGoesCold(dt);

            // Furnace is burning: Heat furnace
            if (IsBurning)
                furnaceTemperature = CalcTemperatureChange(furnaceTemperature, _burnStack?.CombustibleProps?.BurnTemperature ?? 0, dt);

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
            if (inventory.InputSlot.Empty
                &&  Math.Abs(furnaceTemperature - (_burnStack?.CombustibleProps.BurnTemperature ?? 0)) < 50
                )
                burnBonus = emptyBrazierBurnTimeMulBonus;

            burnRemaining -= dt / burnBonus;
            if (burnRemaining > 0)
                return;
            
            burnRemaining = 0;
            _burnStack = null;
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
        ||  inventory.InputStack?.ItemAttributes?["allowHeating"]?.AsBool() == true;
        
        private void OnBurnHeatInput(float dt)
        {
            if (!CanHeatInput)
            {
                inputStackCookingTime = 0;
                return;
            }
            
            float currTemp = inventory.InputStackTemp;
            if (currTemp == 0)
                currTemp = enviromentTemperature;
            float meltingPoint = inventory.InputStack.Collectible.GetMeltingPoint(Api.World, inventory, inventory.InputSlot);

            // Only Heat ore. Cooling happens already in the itemstack
            if (currTemp < furnaceTemperature)
            {
                float f = (1 + GameMath.Clamp((furnaceTemperature - currTemp) / 30, 0, 1.6f)) * dt;
                if (currTemp >= meltingPoint)
                    f /= 11;

                float newTemp = CalcTemperatureChange(currTemp, furnaceTemperature, f);
                int maxTemp = Math.Max(inventory.InputStack.Collectible.CombustibleProps?.MaxTemperature ?? 0, inventory.InputStack.ItemAttributes?["maxTemperature"]?.AsInt(0) ?? 0);
                if (maxTemp > 0)
                    newTemp = Math.Min(maxTemp, newTemp);
                
                currTemp = newTemp;
                inventory.InputStackTemp = newTemp;
            }

            // Begin smelting when hot enough
            if (currTemp >= meltingPoint)
                inputStackCookingTime += GameMath.Clamp((int)(currTemp / meltingPoint), 1, 30) * dt;
            else
            if (inputStackCookingTime > 0)
                inputStackCookingTime--;
        }
        
        public bool CanHeatOutput =>
            inventory.OutputStack?.ItemAttributes?["allowHeating"]?.AsBool() == true;
        
        public void OnBurnHeatOutput(float dt)
        {
            if (!CanHeatOutput)
                return;
            
            float currTemp = inventory.OutputStackTemp;
            if(currTemp == 0)
                currTemp = enviromentTemperature;

            // Only Heat ore. Cooling happens already in the itemstack
            if (currTemp >= furnaceTemperature)
                return;
            
            float newTemp = CalcTemperatureChange(currTemp, furnaceTemperature, 2 * dt);
            int maxTemp = Math.Max(inventory.OutputStack.Collectible.CombustibleProps?.MaxTemperature ?? 0, inventory.OutputStack.ItemAttributes?["maxTemperature"]?.AsInt(0) ?? 0);
            if (maxTemp > 0)
                newTemp = Math.Min(maxTemp, newTemp);
            
            inventory.OutputStackTemp = newTemp;
        }
        
        public bool CanSmeltInput
        { get {
            if (inventory.InputStack == null)
                return false;

            if (inventory.InputStack.Collectible.OnSmeltAttempt(inventory))
                MarkDirty(true);

            return
                inventory.InputStack.Collectible.CanSmelt(Api.World, inventory, inventory.InputStack, inventory.OutputStack)
            &&  inventory.InputStack.Collectible.CombustibleProps?.RequiresContainer != true;
        } }
        
        private bool CanSmelt
        { get {
            if(!BurnsAllFuel && !CanHeatInput)
                return false;

            return
                (inventory.FuelStack?.CanBurn() ?? false)
            ||  (inventory.InputStack?.CanBurn() ?? false)
            ||  (IsTall
                && ((inventory[3].Itemstack?.CanBurn() ?? false)
                ||  (inventory[4].Itemstack?.CanBurn() ?? false)
                    )
                );
        } }
        
        public void OnBurnSmeltItems()
        {
            float maxCookingTime =
                inventory.InputSlot?.Itemstack?.Collectible?.GetMeltingDuration(Api.World, inventory, inventory.InputSlot)
            ??  30;
            
            if (inputStackCookingTime <= maxCookingTime
            || !CanSmeltInput
               )
                return;
            
            inventory.InputStack.Collectible.DoSmelt(Api.World, inventory, inventory.InputSlot, inventory.OutputSlot);
            inventory.InputStackTemp = enviromentTemperature;
            inputStackCookingTime = 0;
            MarkDirty(true);
            inventory.InputSlot.MarkDirty();
        }
        
        public void OnBurnIgniteFuel()
        {
            if (IsBurning
            || !CanIgniteFuel
            || !CanSmelt
                )
                return;

            ItemSlot[] candidates = new ItemSlot[inventory.Count];
            int candidateCount = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (i == 2)
                    continue;
                
                if (inventory[i].StackSize > 0
                && (inventory[i].Itemstack?.CanBurn() ?? false)
                    )
                {
                    candidates[i] = inventory[i];
                    candidateCount++;
                }
            }

            if (candidateCount == 0)
                return;

            ItemSlot consumeSlot = inventory[_burnFromSlot];
            int candidateIndex = Api.World.Rand.Next(candidateCount);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == null)
                    continue;
                
                if (candidateIndex == 0)
                {
                    _burnFromSlot = i;
                    consumeSlot = candidates[i];
                    break;
                }
                candidateIndex--;    
            }
            if (consumeSlot is null)
                return;

            ItemStack consumeStack = consumeSlot.Itemstack;
            
            var combustibleProps = consumeStack.Collectible.CombustibleProps.Clone();
            combustibleProps.BurnDuration *= BurnDurationModifier;
            combustibleProps.BurnTemperature = (int)(combustibleProps.BurnTemperature * BurnTempModifier);

            var gsPropsTemp = consumeStack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;
            var groundStorageProps = gsPropsTemp?.Clone();
            // Fix for Clone()
            if (groundStorageProps != null)
                groundStorageProps.ModelItemsToStackSizeRatio = gsPropsTemp.ModelItemsToStackSizeRatio;
            
            _burnStack = new()
                {Key = consumeStack.Collectible.Code.Path
                ,CombustibleProps = combustibleProps
                ,GSProps = groundStorageProps
                };
            
            burnRemaining = combustibleProps.BurnDuration;
            SetBlockState("lit");
            MarkDirty(true);

            consumeStack.StackSize -= 1;
            if (consumeStack.StackSize <= 0)
            {
                consumeSlot.Itemstack = null;
                consumeSlot.MarkDirty();
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
        
        private BlockEntityFirepit EmulateBEFirepit => new() {Pos = Pos};
        
        void UpdateRenderer()
        {
            if (renderer == null)
                return;

            ItemStack contentStack = inventory.InputStack ?? inventory.OutputStack;

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
                IInFirepitRenderer childrenderer = contentRenderSupplier.GetRendererWhenInFirepit(contentStack, EmulateBEFirepit, contentStack == inventory.OutputStack);
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
        
        bool InputSlotImposes
        { get {
            ItemSlot contentSlot = inventory.InputSlot.Empty ? inventory.OutputSlot : inventory.InputSlot;
            return
                contentSlot?.StackSize > 0
            &&!(contentSlot?.Itemstack?.CanBurn() ?? false);
        } }
        
        ItemSlot FuelNormalSlot => InputSlotImposes ? null : inventory.FuelSlot;
        
        ItemSlot FuelWideSlot
        { get {
            if (InputSlotImposes
            &&  inventory.FuelSlot?.StackSize > 0
                )
                return inventory.FuelSlot;
            
            ItemSlot contentSlot = inventory.InputSlot.Empty ? inventory.OutputSlot : inventory.InputSlot;
            return
               (contentSlot.Itemstack?.CanBurn() ?? false)
            &&  contentSlot.StackSize > 0
            ?   contentSlot
            :   null;
        } }
        
        
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            this.CacheMesh(Block.Shape.Path(), CacheKey, out var brazierMesh);
            mesher.AddMeshData(brazierMesh);
            
            ItemStack contentStack = inventory.InputStack ?? inventory.OutputStack;
            MeshData contentmesh = GetContentMesh(contentStack, tesselator);
            if (contentmesh is not null)
            {
                contentmesh.Translate(new Vec3f(0, 4f / 16f, 0));
                mesher.AddMeshData(contentmesh);
            }
            
            string burnState = Block.Variant["burnstate"];
            if (burnState == null)
                return true;
            
            if (!InputSlotImposes)
                _fuelShortNormalRenderer.Tesselate(mesher, this, FuelNormalSlot, burnState, IsBurning && _burnFromSlot == 0 ? _burnStack : null);
            _fuelShortWideRenderer.Tesselate(mesher, this, FuelWideSlot, burnState, IsBurning && _burnFromSlot == 1 ? _burnStack : null);

            if (!IsTall)
                return true;
            
            ItemSlot tallNormalSlot = inventory[3];
            bool tallNormalBurning = IsBurning && _burnFromSlot == 3;
            if ((tallNormalSlot.Itemstack.CanBurn() && tallNormalSlot.StackSize > 0) || tallNormalBurning)
                _fuelTallNormalRenderer.Tesselate(mesher, this, tallNormalSlot, burnState, tallNormalBurning ? _burnStack : null);

            ItemSlot tallWideSlot = inventory[4];
            bool tallWideBurning = IsBurning && _burnFromSlot == 4;
            if ((tallWideSlot.Itemstack.CanBurn() && tallWideSlot.StackSize > 0) || tallWideBurning)
                _fuelTallWideRenderer.Tesselate(mesher, this, tallWideSlot, burnState, tallWideBurning ? _burnStack : null);
            
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
                EnumFirepitModel model = contentRendererSupplier.GetDesiredFirepitModel(contentStack, EmulateBEFirepit, contentStack == inventory.OutputStack);
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

            dialogTree.SetInt("maxTemperature", _burnStack?.CombustibleProps?.BurnTemperature ?? 0);
            dialogTree.SetFloat("oreCookingTime", inputStackCookingTime);
            dialogTree.SetFloat("maxFuelBurnTime", _burnStack?.CombustibleProps?.BurnDuration ?? 0);
            dialogTree.SetFloat("fuelBurnTime", burnRemaining);

            if (inventory.InputStack == null)
                dialogTree.RemoveAttribute("oreTemperature");
            else
            {
                float meltingDuration = inventory.InputStack.Collectible.GetMeltingDuration(Api.World, inventory, inventory.InputSlot);

                dialogTree.SetFloat("oreTemperature", inventory.InputStackTemp);
                dialogTree.SetFloat("maxOreCookingTime", meltingDuration);
            }
            
            dialogTree.SetString("outputText", inventory.GetOutputText());

            bool haveCookingContainer = inventory.HaveCookingContainer;
            dialogTree.SetInt("haveCookingContainer", haveCookingContainer ? 1 : 0);
            
            bool showTallFuelSlots =
                IsTall
            &&  inventory.InputStack.CanBurn()
            && !inventory.HaveCookingContainer;
            int quantitySlots = haveCookingContainer
                ? inventory.CookingSlots.Length
                : (showTallFuelSlots ? 2 : 0);
            dialogTree.SetInt("showTallFuelSlots", showTallFuelSlots ? 1 : 0);
            dialogTree.SetInt("quantityCookingSlots", quantitySlots);
            
            dialogTree.SetInt("inputCanBurn", inventory.InputStack.CanBurn() ? 1 : 0);
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
            tree.SetInt("_burnFromSlot", _burnFromSlot);
            if (_burnStack != null)
            {
                tree.SetString("_burnStack.Key", _burnStack.Key);
                tree.SetCombustibleProps("_burnStack.CombustibleProps", _burnStack.CombustibleProps);
                tree.SetGroundStorageProps("_burnStack.GSProps",_burnStack.GSProps);
            }
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
            _burnFromSlot = tree.GetInt("_burnFromSlot");
            string _burnKey = tree.GetString("_burnStack.Key");
            if (_burnKey != null)
                _burnStack = new()
                {Key = _burnKey
                ,CombustibleProps = tree.GetCombustibleProps("_burnStack.CombustibleProps")
                ,GSProps = tree.GetGroundStorageProps("_burnStack.GSProps")
                };
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