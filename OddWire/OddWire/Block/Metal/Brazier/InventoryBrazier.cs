using System;
using System.Collections.Generic;
using OddWire.VintageStory.API.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#nullable disable

namespace OddWire.GameContent;

public class InventoryBrazier : InventoryBase, ISlotProvider
{
    ItemSlot[] slots;
    ItemSlot[] cookingSlots;
    public BlockPos pos;
    int defaultStorageType = (int)(EnumItemStorageFlags.General | EnumItemStorageFlags.Agriculture | EnumItemStorageFlags.Alchemy | EnumItemStorageFlags.Jewellery | EnumItemStorageFlags.Metallurgy | EnumItemStorageFlags.Outfit);

    public ItemSlot[] CookingSlots { get { return HaveCookingContainer ? cookingSlots : Array.Empty<ItemSlot>(); } }

    /// <summary>
    /// Returns the cooking slots
    /// </summary>
    public ItemSlot[] Slots
    {
        get { return cookingSlots; }
    }

    
    public ItemSlot FuelSlot => this[0];
    public ItemStack FuelStack
    {
        get { return this[0].Itemstack; }
        set { this[0].Itemstack = value; this[0].MarkDirty(); }
    }
        
    public ItemSlot InputSlot => this[1];
    public ItemStack InputStack
    {
        get { return this[1].Itemstack; }
        set { this[1].Itemstack = value; this[1].MarkDirty(); }
    }
    public float InputStackTemp
    {   get => GetTemp(InputStack);
        set => SetTemp(InputStack, value);
    }

    

    public ItemSlot OutputSlot => this[2];
    public ItemStack OutputStack
    {
        get { return this[2].Itemstack; }
        set { this[2].Itemstack = value; this[2].MarkDirty(); }
    }
    public float OutputStackTemp
    {   get => GetTemp(OutputStack);
        set => SetTemp(OutputStack, value);
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
    
    
    public override Size3f MaxContentDimensions {
        get {
            return slots[1].Itemstack?.ItemAttributes?["maxContentDimensions"].AsObject<Size3f>(null);
        }
        set { }
    }

    public bool HaveCookingContainer
    {
        get { return slots[1].Itemstack?.ItemAttributes?.KeyExists("cookingContainerSlots") == true; }
    }

    public float CookingSlotCapacityLitres
    {
        get { return slots?[1]?.Itemstack?.ItemAttributes?["cookingSlotCapacityLitres"].AsFloat(6) ?? 6; }
    }

    public int CookingContainerMaxSlotStackSize
    {
        get {
            if (HaveCookingContainer)
                return slots[1].Itemstack.ItemAttributes["maxContainerSlotStackSize"].AsInt(999);
            if (InputSlot.Itemstack?.CanBurn() == true)
                return 999;
            return 0;
        }
    }

    public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
    {
        int slotid = GetSlotId(sinkSlot);
        return slotid < 3 || base.CanContain(sinkSlot, sourceSlot);
    }

    public InventoryBrazier(string inventoryID, ICoreAPI api) : base(inventoryID, api)
    {
        // slot 0 = fuel
        // slot 1 = input
        // slot 2 = output
        // slot 3,4,5,6 = extra input slots with crucible in input
        slots = GenEmptySlots(7);
        cookingSlots = new ItemSlot[] { slots[3], slots[4], slots[5], slots[6] };
        baseWeight = 4f;
        
    }

    public InventoryBrazier(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
    {
        slots = GenEmptySlots(7);
        cookingSlots = new ItemSlot[] { slots[3], slots[4], slots[5], slots[6] };
        baseWeight = 4f;
    }

    public override void LateInitialize(string inventoryID, ICoreAPI api)
    {
        base.LateInitialize(inventoryID, api);

        for (int i = 0; i < cookingSlots.Length; i++)
        {
            cookingSlots[i].MaxSlotStackSize = CookingContainerMaxSlotStackSize;
        }

        updateCookingSlotsByInputStack(slots[1].Itemstack);
    }

    public override int Count
    {
        get { return slots.Length; }
    }

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


    public override void DidModifyItemSlot(ItemSlot slot, ItemStack extractedStack = null)
    {
        base.DidModifyItemSlot(slot, extractedStack);

        if (slots[1] == slot)
        {
            if (slot?.Itemstack?.ItemAttributes?["storageType"].Exists == true)
                updateCookingSlotsByInputStack(slot.Itemstack);
            else
            if (slot?.Itemstack?.CanBurn() == true)
            {
                updateCookingSlotsByInputStack(slot.Itemstack);
                discardNotFuel();
            }
            else
                discardCookingSlots();
        }
    }

    void updateCookingSlotsByInputStack(ItemStack stack)
    {
        int storageType = defaultStorageType;
        if (stack?.ItemAttributes?.KeyExists("storageType") == true)
            storageType = stack.ItemAttributes["storageType"].AsInt(defaultStorageType);

        for (int i = 0; i < cookingSlots.Length; i++)
        {
            cookingSlots[i].StorageType = (EnumItemStorageFlags)storageType;
            cookingSlots[i].MaxSlotStackSize = CookingContainerMaxSlotStackSize;
           (cookingSlots[i] as ItemSlotWatertight).capacityLitres = CookingSlotCapacityLitres;
        }
    }


    public void discardNotFuel()
    {
        Vec3d droppos = pos.ToVec3d().Add(0.5, 0.5, 0.5);

        for (int i = 0; i < cookingSlots.Length; i++)
        {
            if (cookingSlots[i] == null
            ||  cookingSlots[i].Itemstack?.CanBurn() == true
               ) continue;
            Api.World.SpawnItemEntity(cookingSlots[i].Itemstack, droppos);
            cookingSlots[i].Itemstack = null;
        }
    }

    public void discardCookingSlots()
    {
        Vec3d droppos = pos.ToVec3d().Add(0.5, 0.5, 0.5);

        for (int i = 0; i < cookingSlots.Length; i++)
        {
            if (cookingSlots[i] == null) continue;
            Api.World.SpawnItemEntity(cookingSlots[i].Itemstack, droppos);
            cookingSlots[i].Itemstack = null;
        }
    }


    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        List<ItemSlot> modifiedSlots = new List<ItemSlot>();
        slots = SlotsFromTreeAttributes(tree, slots, modifiedSlots);
        for (int i = 0; i < modifiedSlots.Count; i++) DidModifyItemSlot(modifiedSlots[i]);

        if (Api != null)
        {
            for (int i = 0; i < cookingSlots.Length; i++)
            {
                cookingSlots[i].MaxSlotStackSize = CookingContainerMaxSlotStackSize;
            }
        }
    }
        
    

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        SlotsToTreeAttributes(slots, tree);
    }

    public override void OnItemSlotModified(ItemSlot slot)
    {
        base.OnItemSlotModified(slot);
    }

    protected override ItemSlot NewSlot(int i)
    {
        if (i == 0) return new ItemSlotSurvival(this); // Fuel
        if (i == 1) return new ItemSlotInput(this, 2);
        if (i == 2) return new ItemSlotOutput(this);

        return new ItemSlotWatertight(this, CookingSlotCapacityLitres);
    }


    public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op, List<ItemSlot> skipSlots = null)
    {
        if (!HaveCookingContainer)
        {
            if (skipSlots == null) skipSlots = new List<ItemSlot>();
            skipSlots.Add(slots[2]);
            skipSlots.Add(slots[3]);
            skipSlots.Add(slots[4]);
            skipSlots.Add(slots[5]);
            skipSlots.Add(slots[6]);
        }

        WeightedSlot slot = base.GetBestSuitedSlot(sourceSlot, op, skipSlots);

        return slot;
    }


    public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
    {
        ItemStack stack = sourceSlot.Itemstack;

        if (targetSlot == slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockCookingContainer)) return 2.2f;

        if (targetSlot == slots[0] && (stack.Collectible.CombustibleProps == null || stack.Collectible.CombustibleProps.BurnTemperature <= 0)) return 0;
        if (targetSlot == slots[1] && (stack.Collectible.CombustibleProps == null || stack.Collectible.CombustibleProps.SmeltedStack  == null)) return 0.5f;


        return base.GetSuitability(sourceSlot, targetSlot, isMerge);
    }


    public string GetOutputText()
    {
        ItemStack inputStack = slots[1].Itemstack;

        if (inputStack == null) return null;

        if (inputStack.Collectible is BlockSmeltingContainer)
        {
            return ((BlockSmeltingContainer)inputStack.Collectible).GetOutputText(Api.World, this, slots[1]);
        }
        if (inputStack.Collectible is BlockCookingContainer)
        {
            return ((BlockCookingContainer)inputStack.Collectible).GetOutputText(Api.World, this, slots[1]);
        }

        ItemStack smeltedStack = inputStack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

        if (smeltedStack == null) return null;
        if (inputStack.Collectible.CombustibleProps.SmeltingType == EnumSmeltType.Fire) return Lang.Get("Can't smelt, requires a kiln");
        if (inputStack.Collectible.CombustibleProps.RequiresContainer) return Lang.Get("Can't smelt, requires smelting container (i.e. Crucible)");

        return Lang.Get("firepit-gui-willcreate", inputStack.StackSize / inputStack.Collectible.CombustibleProps.SmeltedRatio, smeltedStack.GetName());
    }


}