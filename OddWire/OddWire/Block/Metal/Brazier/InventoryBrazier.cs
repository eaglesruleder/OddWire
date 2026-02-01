using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OddWire.VintageStory.API.Common;

#nullable disable

namespace OddWire.GameContent;

public class InventoryBrazier : InventoryBase, ISlotProvider
{
    ItemSlot[] slots;
    ItemSlot[] processingSlots;
    public BlockPos pos;
    int defaultStorageType = (int)(EnumItemStorageFlags.General | EnumItemStorageFlags.Agriculture | EnumItemStorageFlags.Alchemy | EnumItemStorageFlags.Jewellery | EnumItemStorageFlags.Metallurgy | EnumItemStorageFlags.Outfit);

    #region ISlotProvider
    public ItemSlot[] Slots => processingSlots;
    #endregion
    
    public override int Count => slots.Length;
    public override ItemSlot this[int slotId]
    {
        get
        {
            if (slotId < 0 || slotId >= Count) return null;
            return slots[slotId];
        }
        set
        {
            if (slotId < 0 || slotId >= Count) throw new ArgumentOutOfRangeException(nameof(slotId));
            if (value == null) throw new ArgumentNullException(nameof(value));
            slots[slotId] = value;
        }
    }
    
    public override Size3f MaxContentDimensions
    {   get => InputStack?.ItemAttributes?["maxContentDimensions"].AsObject<Size3f>(null);
        set {}
    }

    public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot) =>
        GetSlotId(sinkSlot) < 3
    ||  base.CanContain(sinkSlot, sourceSlot);
    
    
    public ItemSlot FuelSlot => this[0];
    public ItemStack FuelStack
    {   get => this[0].Itemstack;
        set { this[0].Itemstack = value; this[0].MarkDirty(); }
    }
        
    public ItemSlot InputSlot => this[1];
    public ItemStack InputStack
    {   get => this[1].Itemstack;
        set { this[1].Itemstack = value; this[1].MarkDirty(); }
    }
    public float InputTemp
    {   get => GetTemp(InputStack);
        set => SetTemp(InputStack, value);
    }
    public float InputMeltingPoint => InputStack.Collectible.GetMeltingPoint(Api.World, this, InputSlot);

    public ItemSlot OutputSlot => this[2];
    public ItemStack OutputStack
    {   get => this[2].Itemstack;
        set { this[2].Itemstack = value; this[2].MarkDirty(); }
    }
    public float OutputStackTemp
    {   get => GetTemp(OutputStack);
        set => SetTemp(OutputStack, value);
    }

    public ItemSlot ContentSlot => InputSlot.Empty ? OutputSlot : InputSlot;
    public ItemStack ContentStack => ContentSlot?.Itemstack;
    
    public ItemSlot[] CookingSlots => HasCookingContainer ? processingSlots : Array.Empty<ItemSlot>();
    
    ItemSlot[] _fuelSlotRefs;
    public ItemSlot[] FuelSlots => _fuelSlotRefs;

    public bool HasCookingContainer =>
        InputStack?.ItemAttributes?.KeyExists("cookingContainerSlots") == true;

    public float CookingSlotCapacityLitres =>
        InputStack?.ItemAttributes?["cookingSlotCapacityLitres"].AsFloat(6) ?? 6;

    public int ProcessingMaxSlotStackSize
    { get {
        if (HasCookingContainer)
            return InputStack.ItemAttributes["maxContainerSlotStackSize"].AsInt(999);
        if (InputStack?.CanBurn() == true)
            return 999;
        return 0;
    } }

    
    public InventoryBrazier(string inventoryID, ICoreAPI api) : base(inventoryID, api)
    {
        // slot 0 = fuel
        // slot 1 = input
        // slot 2 = output
        // slot 3,4,5,6 = extra input slots with crucible in input
        slots = GenEmptySlots(7);
        processingSlots = new[]{ slots[3], slots[4], slots[5], slots[6] };
        _fuelSlotRefs = new[]{ slots[0], slots[1], slots[3], slots[4], slots[5], slots[6] };
        baseWeight = 4f;
    }

    public InventoryBrazier(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
    {
        slots = GenEmptySlots(7);
        processingSlots = new[]{ slots[3], slots[4], slots[5], slots[6] };
        _fuelSlotRefs = new[]{ slots[0], slots[1], slots[3], slots[4], slots[5], slots[6] };
        baseWeight = 4f;
    }

    public override void LateInitialize(string inventoryID, ICoreAPI api)
    {
        base.LateInitialize(inventoryID, api);
        UpdateProcessingSlots();
    }

    protected override ItemSlot NewSlot(int i)
    {
        if (i == 0) return new ItemSlotSurvival(this); // Fuel
        if (i == 1) return new ItemSlotInput(this, 2);
        if (i == 2) return new ItemSlotOutput(this);
        return new ItemSlotWatertight(this);
    }
    
    public override void DidModifyItemSlot(ItemSlot slot, ItemStack extractedStack = null)
    {
        base.DidModifyItemSlot(slot, extractedStack);

        if (!HasCookingContainer)
            for(int i = 1; i < FuelSlots.Length; i++)
                if (FuelSlots[i]?.Itemstack?.CanBurn() == true)
                {
                    CollapseStacks();
                    break;
                }
        
        if (slot != InputSlot)
            return;

        if (slot?.Itemstack?.ItemAttributes?["storageType"].Exists == true)
        {
            UpdateProcessingSlots();
            return;
        }
        
        if (slot?.Itemstack?.CanBurn() == true)
        {
            DiscardProcessingSlotsNotFuel();
            UpdateProcessingSlots();
            return;
        }
        
        DiscardProcessingSlots();
    }

    public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op, List<ItemSlot> skipSlots = null)
    {
        if (!HasCookingContainer && InputStack?.CanBurn(true) != true)
        {
            if (skipSlots == null)
                skipSlots = new List<ItemSlot>();
            skipSlots.Add(slots[2]);
            skipSlots.Add(slots[3]);
            skipSlots.Add(slots[4]);
            skipSlots.Add(slots[5]);
            skipSlots.Add(slots[6]);
        }
        return base.GetBestSuitedSlot(sourceSlot, op, skipSlots);
    }

    public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
    {
        ItemStack stack = sourceSlot.Itemstack;

        if (targetSlot == InputSlot)
        {
            if(stack.Collectible is BlockSmeltingContainer or BlockCookingContainer)
                return 2.2f;
            
            if(stack.Collectible.CombustibleProps?.SmeltedStack == null)
                return 0.5f;
        }

        if (targetSlot == FuelSlot
            && (stack.Collectible.CombustibleProps == null
            ||  stack.Collectible.CombustibleProps.BurnTemperature <= 0
            ))
            return 0;
        
        return base.GetSuitability(sourceSlot, targetSlot, isMerge);
    }
    
    
    void UpdateProcessingSlots(ItemStack stack = null)
    {
        if (stack is null)
            stack = InputStack;
        
        int storageType = defaultStorageType;
        if (stack?.ItemAttributes?.KeyExists("storageType") == true)
            storageType = stack.ItemAttributes["storageType"].AsInt(defaultStorageType);

        for (int i = 0; i < processingSlots.Length; i++)
        {
            processingSlots[i].StorageType = (EnumItemStorageFlags)storageType;
            processingSlots[i].MaxSlotStackSize = ProcessingMaxSlotStackSize;
           (processingSlots[i] as ItemSlotWatertight).capacityLitres = CookingSlotCapacityLitres;
        }
    }
    
    private void CollapseStacks()
    {
        ItemSlot[] fuelSlots = FuelSlots;

        // Index first empty slot
        int i = 0;
        while (i < fuelSlots.Length && !fuelSlots[i].Empty)
            i++;
        if (i >= fuelSlots.Length)
            return;

        // Index next !empty
        int j = i + 1;
        while (j < fuelSlots.Length && fuelSlots[j].Empty)
            j++;
        if (j >= fuelSlots.Length)
            return;

        // Do exactly ONE step per call.
        // TryFlipWith calls DidModifyItemSlot, furthering collapse
        if (fuelSlots[j].TryFlipWith(fuelSlots[i]))
        {
            fuelSlots[i].MarkDirty();
            fuelSlots[j].MarkDirty();
        }
    }
    
    public void DiscardProcessingSlotsNotFuel()
    {
        Vec3d droppos = pos.ToVec3d().Add(0.5, 0.5, 0.5);
        for (int i = 0; i < processingSlots.Length; i++)
        {
            var slot = processingSlots[i];
            if (slot is not null
            && slot.Itemstack?.CanBurn() != true
               )
            {
                Api.World.SpawnItemEntity(slot.Itemstack, droppos);
                slot.Itemstack = null;
            }
        }
    }

    public void DiscardProcessingSlots()
    {
        Vec3d droppos = pos.ToVec3d().Add(0.5, 0.5, 0.5);
        for (int i = 0; i < processingSlots.Length; i++)
        {
            var slot = processingSlots[i];
            if (slot is not null)
            {
                Api.World.SpawnItemEntity(slot.Itemstack, droppos);
                slot.Itemstack = null;
            }
        }
    }
    
    
    private float GetTemp(ItemStack stack)
    {
        if (stack == null)
            return 0;

        if (CookingSlots.Length <= 0)
            return stack.Collectible.GetTemperature(Api.World, stack);
            
        bool haveStack = false;
        float lowestTemp = 0;
        for (int i = 0; i < CookingSlots.Length; i++)
        {
            ItemStack cookingStack = CookingSlots[i].Itemstack;
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
            
        if (CookingSlots.Length > 0)
            for (int i = 0; i < CookingSlots.Length; i++)
                CookingSlots[i].Itemstack?.Collectible.SetTemperature(Api.World, CookingSlots[i].Itemstack, value);
        else
            stack.Collectible.SetTemperature(Api.World, stack, value);
    }

    
    public string GetOutputText()
    {
        ItemStack inputStack = InputStack;
        if (inputStack == null)
            return null;

        if (inputStack.Collectible is BlockSmeltingContainer container)
            return container.GetOutputText(Api.World, this, InputSlot);
        if (inputStack.Collectible is BlockCookingContainer cookingContainer)
            return cookingContainer.GetOutputText(Api.World, this, InputSlot);

        ItemStack smeltedStack = inputStack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;
        if (smeltedStack == null)
            return null;
        if (inputStack.Collectible.CombustibleProps.SmeltingType == EnumSmeltType.Fire)
            return Lang.Get("Can't smelt, requires a kiln");
        if (inputStack.Collectible.CombustibleProps.RequiresContainer)
            return Lang.Get("Can't smelt, requires smelting container (i.e. Crucible)");

        return Lang.Get("firepit-gui-willcreate", inputStack.StackSize / inputStack.Collectible.CombustibleProps.SmeltedRatio, smeltedStack.GetName());
    }
    

    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        List<ItemSlot> modifiedSlots = new List<ItemSlot>();
        slots = SlotsFromTreeAttributes(tree, slots, modifiedSlots);
        for (int i = 0; i < modifiedSlots.Count; i++)
            DidModifyItemSlot(modifiedSlots[i]);

        if (Api != null)
            UpdateProcessingSlots();
    }

    public override void ToTreeAttributes(ITreeAttribute tree) => SlotsToTreeAttributes(slots, tree);
}