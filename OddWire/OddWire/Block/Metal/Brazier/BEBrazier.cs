using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using OddWire.System;
using OddWire.VintageStory.API.Client;
using OddWire.VintageStory.API.Common;
using OddWire.VintageStory.GameContent;

using DialogKeys = OddWire.GameContent.GuiDialogBlockEntityBrazier.TreeKeys;

#nullable disable

namespace OddWire.GameContent
{
    public class FuelBurnStack
    {
        public string Key;
        public string ShapeRoot;
        public CombustibleProperties CombustibleProps;
        public int BurnTemp => CombustibleProps?.BurnTemperature ?? 0;
        public GroundStorageProperties StorageProps;
    }
    
    public class FuelTesselateProperties
    {
        public string DefaultShapeRoot;
        public string ModelKey;
        public ModelTransform Transform;
        
        public FuelTesselateProperties Clone() => new()
            {DefaultShapeRoot = DefaultShapeRoot
            ,ModelKey = ModelKey
            ,Transform = Transform
            };
    }
    
    public class BlockEntityBrazier : BlockEntityOpenableContainer, IFirePit, IHeatSource, ITemperatureSensitive
    {
        public virtual string FuelShapePath => "oddwire:shapes/block/fuel/";
        public virtual string CacheKey => "brazier-meshes";
        
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
            return IsBurning ? 10 : (IgniteByInteraction ? 0.25f : 0);
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
                IgniteByInteraction = false;
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
        
        public float emptyBrazierBurnTimeMulBonus = 4f;
        
        // How much of the current fuel is consumed
        public float burnRemaining;
        
        // For how long the ore has been cooking
        public float inputStackCookingTime;
        
        public double extinguishedTotalHours;
        
        
        public EnumFirepitModel CurrentModel { get; private set; }
        
        // Current temperature of the furnace
        public float furnaceTemperature = 20;
        
        // Resting temperature
        public virtual int enviromentTemperature => 20;
        
        // If true, then the fire pit is currently hot enough to ignite fuel-
        public bool IgniteByInteraction;
        
        public virtual bool BurnsAllFuel => true;

        public int FuelBonusCapacity => Block.Attributes?["fuelBonusCapacity"]?.AsInt() ?? 0;
        
        private bool CanIgniteFuel =>
            BurnsAllFuel
        &&  (inventory.FuelCanBurn() == true
        ||   inventory.InputCanBurn()
            );
        
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
        
        
        private BrazierContentsRenderer renderer;

        private FuelTesselateProperties[] FuelRenderProps
        { get {
            var stackPositions =
                Block.Attributes?["fuelRendererProps"]?.AsObject<FuelTesselateProperties[]>() 
            ??  Array.Empty<FuelTesselateProperties>();

            int outLen = Math.Max(2, stackPositions.Length);
            var result = new FuelTesselateProperties[outLen];
            
            result[0] = stackPositions.Length > 0
            ?   stackPositions[0]
            :   new FuelTesselateProperties
                {DefaultShapeRoot = FuelShapePath
                ,ModelKey = "normal"
                ,Transform = new ModelTransform()
                };
            result[0].Transform.EnsureDefaultValues();
            
            result[1] = stackPositions.Length > 1
            ?   stackPositions[1]
            :   new FuelTesselateProperties
                {DefaultShapeRoot = FuelShapePath
                ,ModelKey = "wide"
                ,Transform = new ModelTransform()
                };
            result[1].Transform.EnsureDefaultValues();
            
            for (int i = 2; i < stackPositions.Length; i++)
            {
                result[i] = stackPositions[i];
                result[i].Transform.EnsureDefaultValues();
            }

            return result;
        } }


        private GuiDialogBlockEntityBrazier _clientDialog;
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
            inventory.FuelBonusCapacity = FuelBonusCapacity;
            inventory.LateInitialize("smelting-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

            RegisterGameTickListener(OnBurnTick, 100);
            RegisterGameTickListener(OnClientSync, 500);

            if (FuelBonusCapacity > 4)
                api.Logger.Error("FuelBonusCapacity limited to 4");
            
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
                toggleInventoryDialogClient(byPlayer, () =>
                {
                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                    SetDialogValues(dtree);
                    _clientDialog = new GuiDialogBlockEntityBrazier(DialogTitle, Inventory, Pos, dtree, Api as ICoreClientAPI);
                    return _clientDialog;
                });

            return true;
        }
        
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
            renderer = null;

            if (_clientDialog is not null)
            {
                _clientDialog.TryClose();
                _clientDialog?.Dispose();
                _clientDialog = null;
            }
        }
        
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
        }
        
        
        private bool _shouldRedraw;
        private void OnSlotModified(int slotid)
        {
            Block = Api.World.BlockAccessor.GetBlock(Pos);

            UpdateRenderer();
            MarkDirty(Api.Side == EnumAppSide.Server); // Save useless triple-remesh by only letting the server decide when to redraw
            _shouldRedraw = true;

            if (Api is ICoreClientAPI
            &&  _clientDialog != null
                )
                SetDialogValues(_clientDialog.Attributes);

            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }
        
        
        private void OnBurnTick(float dt)
        {
            // Only tick on the server and merely sync to client
            if (Api is ICoreClientAPI)
            {
                renderer?.contentStackRenderer?.OnUpdate(inventory.InputTemp);
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
            OnBurnSmeltItems(dt);

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
            &&  Math.Abs(furnaceTemperature - _burnStack.BurnTemp) < 50
                )
                burnBonus = emptyBrazierBurnTimeMulBonus;

            burnRemaining -= dt / burnBonus;
            if (burnRemaining > 0)
                return;
            
            burnRemaining = 0;
            _burnStack = null;
            if (!CanIgniteFuel) // This check avoids light flicker when a piece of fuel is consumed and more is available
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
                IgniteByInteraction = false;
                SetBlockState("cold");
            }
        }
        
        private void OnBurnHeatInput(float dt)
        {
            if (!CanSmeltInput
            &&  inventory.InputStack?.ItemAttributes?["allowHeating"]?.AsBool() != true
                )
                return;
            
            float currTemp = inventory.InputTemp;
            if (currTemp == 0)
                currTemp = enviromentTemperature;

            // Only Heat ore. Cooling happens already in the itemstack
            if (currTemp >= furnaceTemperature)
                return;
            
            float f = (1 + GameMath.Clamp((furnaceTemperature - currTemp) / 30, 0, 1.6f)) * dt;
            if (currTemp >= inventory.InputMeltingPoint)
                f /= 11;

            float newTemp = CalcTemperatureChange(currTemp, furnaceTemperature, f);
            int maxTemp = Math.Max(inventory.InputStack.Collectible.CombustibleProps?.MaxTemperature ?? 0, inventory.InputStack.ItemAttributes?["maxTemperature"]?.AsInt(0) ?? 0);
            if (maxTemp > 0)
                newTemp = Math.Min(maxTemp, newTemp);
            
            inventory.InputTemp = newTemp;
        }
        
        public void OnBurnHeatOutput(float dt)
        {
            if (inventory.OutputStack?.ItemAttributes?["allowHeating"]?.AsBool() != true)
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
        
        public void OnBurnSmeltItems(float dt)
        {
            if (!CanSmeltInput)
            {
                inputStackCookingTime = 0;
                return;
            }
            
            // Begin smelting when hot enough
            if (inventory.InputTemp >= inventory.InputMeltingPoint)
                inputStackCookingTime += GameMath.Clamp((int)(inventory.InputTemp / inventory.InputMeltingPoint), 1, 30) * dt;
            else
            if (inputStackCookingTime > 0)
                inputStackCookingTime--;
            
            float maxCookingTime =
                inventory.InputSlot?.Itemstack?.Collectible?.GetMeltingDuration(Api.World, inventory, inventory.InputSlot)
            ??  30;
            
            if (inputStackCookingTime <= maxCookingTime)
                return;
            
            inventory.InputStack.Collectible.DoSmelt(Api.World, inventory, inventory.InputSlot, inventory.OutputSlot);
            inventory.InputTemp = enviromentTemperature;
            inputStackCookingTime = 0;
            MarkDirty(true);
            inventory.InputSlot.MarkDirty();
        }
        
        public void OnBurnIgniteFuel()
        {
            if (IsBurning
            ||!(IgniteByInteraction && CanIgniteFuel)
                )
                return;

            var consumeSlot = GetSlotToIgnite(out _burnFromSlot);
            if (consumeSlot is not null)
                IgniteSlot(consumeSlot);
        }

        public virtual ItemSlot GetSlotToIgnite(out int index)
        {
            ItemSlot[] fuelSlots = inventory.FuelSlots;
            ItemSlot[] candidates = new ItemSlot[fuelSlots.Length];
            int candidateCount = 0;
            for (int i = 0; i < fuelSlots.Length; i++)
            {
                if (fuelSlots[i].Itemstack?.CanBurn() ?? false)
                {
                    candidates[i] = fuelSlots[i];
                    candidateCount++;
                }
                //  If !InputSlot.CanBurn, dont try burn ingredients
                else if (i == 1)
                    break;
            }

            if (candidateCount == 0)
            {
                index = 0;
                return null;
            }
            
            int candidateIndex = Api.World.Rand.Next(candidateCount);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == null)
                    continue;
                
                if (candidateIndex == 0)
                {
                    index = i;
                    return candidates[i];
                }
                candidateIndex--;    
            }

            index = 0;
            return null;
        }

        private void IgniteSlot(ItemSlot slot)
        {
            ItemStack stack = slot.Itemstack;
            
            var combustibleProps = stack.Collectible.CombustibleProps.Clone();
            combustibleProps.BurnDuration *= BurnDurationModifier;
            combustibleProps.BurnTemperature = (int)(combustibleProps.BurnTemperature * BurnTempModifier);

            var storagePropsSrc = stack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;
            var storageProps = storagePropsSrc?.Clone();
            // Fix for Clone()
            if (storageProps != null)
                storageProps.ModelItemsToStackSizeRatio = storagePropsSrc.ModelItemsToStackSizeRatio;
            
            _burnStack = new()
                {Key = stack.Collectible.Code.Path
                ,ShapeRoot = 
                    stack.Item?.Attributes["shapeFuelStackRoot"]?.ToString()
                ??  stack.Block?.Attributes["shapeFuelStackRoot"]?.ToString()
                ,CombustibleProps = combustibleProps
                ,StorageProps = storageProps
                };
            
            burnRemaining = combustibleProps.BurnDuration;
            SetBlockState("lit");
            MarkDirty(true);

            stack.StackSize -= 1;
            if (stack.StackSize <= 0)
                slot.Itemstack = null;
            slot.MarkDirty();
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
        
        
        public int prevClientSyncTemp = 20;
        private void OnClientSync(float dt)
        {
            if (Api is ICoreServerAPI
            && (IsBurning || prevClientSyncTemp != (int)furnaceTemperature)
               )
                MarkDirty();

            prevClientSyncTemp = (int)furnaceTemperature;
        }
        
        
        private InFirePitProps GetRenderProps(ItemStack contentStack)
        {
            if (contentStack?.ItemAttributes?.KeyExists("inFirePitProps") != true)
                return null;
            
            InFirePitProps props = contentStack.ItemAttributes["inFirePitProps"].AsObject<InFirePitProps>();
            props.Transform.EnsureDefaultValues();

            return props;
        }
        
        private BlockEntityFirepit EmulateBEFirepit => new() {Pos = Pos};
        
        private void UpdateRenderer()
        {
            if (renderer == null)
                return;

            ItemStack contentStack = inventory.ContentStack;

            var contentRenderSupplier = contentStack?.Collectible as IInFirepitRendererSupplier;
            if (renderer.ContentStack is not null
            &&  renderer.contentStackRenderer is not null
            &&  contentRenderSupplier is not null
            &&  renderer.ContentStack.Equals(Api.World, contentStack, GlobalConstants.IgnoredStackAttributes)
               )
                return; // Otherwise the cooking sounds restarts all the time

            renderer.contentStackRenderer?.Dispose();
            renderer.contentStackRenderer = null;

            IInFirepitRenderer childRenderer = contentRenderSupplier?.GetRendererWhenInFirepit(contentStack, EmulateBEFirepit, contentStack == inventory.OutputStack);
            if (childRenderer is not null)
            {
                renderer.SetChildRenderer(contentStack, childRenderer);
                return;
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
            tesselator.CacheTesselateShape(Api, Block, Block.Shape.Path(), CacheKey, mesher);
            
            ItemStack contentStack = inventory.ContentStack;
            MeshData contentmesh = GetContentMesh(contentStack, tesselator);
            if (contentmesh is not null)
            {
                contentmesh.Translate(new Vec3f(0, 4f / 16f, 0));
                mesher.AddMeshData(contentmesh);
            }
            
            if (CurrentModel == EnumFirepitModel.Spit)
                tesselator.CacheTesselateShape(Api, Block, Block.Shape.Folder() + "spit-stick", CacheKey, mesher);
            
            string burnState = Block.Variant["burnstate"];
            if (burnState == null)
                return true;

            var stackPositions = FuelRenderProps;

            ItemSlot normalSlot = IsWide ? null : inventory.FuelSlot;
            ItemSlot wideSlot;
            if (IsWide
            &&  inventory.FuelSlot?.StackSize > 0
                )
                wideSlot = inventory.FuelSlot;
            else
            {
                ItemSlot contentSlot = inventory.ContentSlot;
                wideSlot = 
                    contentSlot.Itemstack?.CanBurn() ?? false
                ?   contentSlot
                :   null;
            }
            
            TesselateEmbers(mesher, tesselator, burnState, 0, normalSlot, stackPositions[0]);
            TesselateEmbers(mesher, tesselator, burnState, 1, wideSlot, stackPositions[1]);
            
            if (!IsWide)
                TesselateFuel(mesher, tesselator, burnState, normalSlot, IsBurning && _burnFromSlot == 0 ? _burnStack : null, stackPositions[0]);
            TesselateFuel(mesher, tesselator, burnState, wideSlot, IsBurning && _burnFromSlot == 1 ? _burnStack : null, stackPositions[1]);
            
            var fuelSlots = inventory.FuelSlots;
            for (int i = 2; i < fuelSlots.Length && i < stackPositions.Length; i++)
            {
                ItemSlot fuelSlot = fuelSlots[i];
                bool slotIsBurning = IsBurning && _burnFromSlot == i;
                if (slotIsBurning || fuelSlot?.Itemstack?.CanBurn() == true)
                    TesselateFuel(mesher, tesselator, burnState, fuelSlot, slotIsBurning ? _burnStack : null, stackPositions[i]);
            }
            return true;
        }

        private void TesselateEmbers(ITerrainMeshPool mesher, ITesselatorAPI tesselator, string burnState, int slotIndex, ItemSlot fuelSlot, FuelTesselateProperties props)
        {
            string embersZeroKey =
                IsBurning
            &&  _burnFromSlot != slotIndex
            && (fuelSlot?.StackSize ?? 0) <= 0
            ?   "extinct" : burnState;
            
            tesselator.CacheTesselateShape
                (Api, Block
                ,$"{FuelShapePath}embers/{embersZeroKey}-{props.ModelKey}", CacheKey
                ,mesher, transform: props.Transform
                );
        }

        private void TesselateFuel(ITerrainMeshPool mesher, ITesselatorAPI tesselator, string burnState, ItemSlot fuelSlot, FuelBurnStack burnStack, FuelTesselateProperties props)
        {
            if (burnState == null
            || (burnStack == null
            &&  fuelSlot?.StackSize <= 0
                )
               )
                return;
            
            string rootPath;
            float? stackRatio;

            if (burnStack != null)
            {
                rootPath = burnStack.ShapeRoot;
                stackRatio = burnStack.StorageProps?.ModelItemsToStackSizeRatio;
            }
            else
            {
                rootPath =
                    fuelSlot?.Itemstack?.Item?.Attributes["shapeFuelStackRoot"]?.ToString()
                ??  fuelSlot?.Itemstack?.Block?.Attributes["shapeFuelStackRoot"]?.ToString();
                stackRatio =
                    fuelSlot?.Itemstack?.Collectible
                        ?.GetBehavior<CollectibleBehaviorGroundStorable>()
                        ?.StorageProps
                        ?.ModelItemsToStackSizeRatio;
            }

            rootPath ??= $"{FuelShapePath}firewood/";
            stackRatio ??= 0.5f;

            int stackQty = fuelSlot?.StackSize ?? 0;
            if (burnStack != null)
                stackQty++;

            int modelQty = stackQty;
            if (stackRatio > 0)
                modelQty = (int)Math.Ceiling(stackRatio.Value * modelQty);
        
            string shapeKey = $"{rootPath.EndWith('/')}{burnState}-{props.ModelKey}";
        
            bool hasMesh = tesselator.CacheTesselateShape(Api, Block, shapeKey, CacheKey, mesher, modelQty, props.Transform);
            if (!hasMesh)
                tesselator.CacheTesselateShape(Api, Block, $"{FuelShapePath}firewood/{burnState}-{props.ModelKey}", CacheKey, mesher, modelQty, props.Transform);
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
            dialogTree.SetFloat(DialogKeys.FURNACE_TEMPERATURE, furnaceTemperature);
            
            dialogTree.SetFloat(DialogKeys.MAX_ORE_COOKING_TIME, inputStackCookingTime);
            dialogTree.SetFloat(DialogKeys.MAX_FUEL_BURN_TIME, _burnStack?.CombustibleProps?.BurnDuration ?? 0);
            dialogTree.SetFloat(DialogKeys.FUEL_BURN_TIME, burnRemaining);

            if (inventory.InputStack == null)
                dialogTree.RemoveAttribute(DialogKeys.ORE_TEMPERATURE);
            else
            {
                dialogTree.SetFloat(DialogKeys.ORE_TEMPERATURE, inventory.InputTemp);
                dialogTree.SetFloat(DialogKeys.MAX_ORE_COOKING_TIME, inventory.InputStack.Collectible.GetMeltingDuration(Api.World, inventory, inventory.InputSlot));
            }
            
            dialogTree.SetString(DialogKeys.OUTPUT_TEXT, inventory.GetOutputText());

            DialogKeys.InputTypeEnum inputType =
                inventory.InputSlot.Empty ? DialogKeys.InputTypeEnum.None
            :   inventory.HasCookingContainer ? DialogKeys.InputTypeEnum.Container
            :   inventory.InputCanBurn() ? DialogKeys.InputTypeEnum.Fuel
            :   DialogKeys.InputTypeEnum.Undefined;
            dialogTree.SetInt(DialogKeys.INPUT_TYPE, (int)inputType);
            
            int quantitySlots = 0;
            if(inputType == DialogKeys.InputTypeEnum.Container)
                quantitySlots = inventory.CookingSlots.Length;
            else if (inputType == DialogKeys.InputTypeEnum.Fuel)
            {
                ItemSlot[] fuelSlots = inventory.FuelSlots;
                for (int i = 2; i < fuelSlots.Length && i < 2+FuelBonusCapacity; i++)
                    if(!fuelSlots[i].Empty)
                        quantitySlots++;

                if (quantitySlots < FuelBonusCapacity)
                    quantitySlots++;
            }
            dialogTree.SetInt(DialogKeys.INPUT_ADDITIONAL_SLOTS, quantitySlots);
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
                tree.SetString("_burnStack.ShapeRoot", _burnStack.ShapeRoot);
                tree.SetCombustibleProps("_burnStack.CombustibleProps", _burnStack.CombustibleProps);
                tree.SetGroundStorageProps("_burnStack.GSProps",_burnStack.StorageProps);
            }
            tree.SetDouble("extinguishedTotalHours", extinguishedTotalHours);
            tree.SetBool("canIgniteFuel", IgniteByInteraction);
        }
        
        private bool clientSidePrevBurning;
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
                ,ShapeRoot = tree.GetString("_burnStack.ShapeRoot")
                ,CombustibleProps = tree.GetCombustibleProps("_burnStack.CombustibleProps")
                ,StorageProps = tree.GetGroundStorageProps("_burnStack.GSProps")
                };
            extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours");
            IgniteByInteraction = tree.GetBool("canIgniteFuel", true);

            if (Api?.Side != EnumAppSide.Client)
                return;
            
            UpdateRenderer();

            if (_clientDialog != null)
                SetDialogValues(_clientDialog.Attributes);
            
            if (clientSidePrevBurning != IsBurning || _shouldRedraw)
            {
                GetBehavior<BEBehaviorFirepitAmbient>().ToggleAmbientSounds(IsBurning);
                clientSidePrevBurning = IsBurning;
                MarkDirty(true);
                _shouldRedraw = false;
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