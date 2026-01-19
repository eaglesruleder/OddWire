using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OddWire.VintageStory.API.Common;

#nullable disable

namespace OddWire.GameContent;
public class FuelRenderer
{
    public virtual string ShapePath => "oddwire:shapes/block/fuel/";
    public virtual string CacheKey => "fuel-meshes";
    
    private readonly string slotKey;
    private readonly Vec3f translate;
    
    public FuelRenderer(string slotKey, Vec3f translate = null)
    {
        this.slotKey = slotKey;
        this.translate = translate ?? Vec3f.Zero;
    }

    public void Tesselate(ITerrainMeshPool mesher, BlockEntity be, ItemSlot slot, string burnState, FuelBurnStack burnStack)
    {
        if (mesher == null
        ||  be == null
        ||  burnState == null
            ) return;
        
        bool renderFuel =
            burnStack != null
        ||  slot?.StackSize > 0;
        
        bool isBurning = be is IFirePit firepit && firepit.IsBurning;
        
        string emberKey = renderFuel
            ? $"{burnState}-{slotKey}"
            : isBurning ? $"extinct-{slotKey}" : $"cold-{slotKey}";

        AddEmbers(mesher, be, emberKey);
        if (renderFuel)
            AddFuel(mesher, be, slot, burnState, burnStack);
    }

    private void AddEmbers(ITerrainMeshPool mesher, BlockEntity be, string meshKey)
    {
        be.CacheMesh($"{ShapePath}embers/{meshKey}", CacheKey, out MeshData embersMesh);
        embersMesh.Translate(translate);
        mesher.AddMeshData(embersMesh);
    }

    private void AddFuel(ITerrainMeshPool mesher, BlockEntity be, ItemSlot slot, string burnState, FuelBurnStack burnStack)
    {
        string key;
        GroundStorageProperties gsProps;

        if (burnStack != null)
        {
            key = burnStack.Key;
            gsProps = burnStack.GSProps;
        }
        else
        {
            key =
                slot?.Itemstack?.Item?.Code.Path
            ??  slot?.Itemstack?.Block?.Code.Path
            ??  "firewood";

            gsProps =
                slot?.Itemstack?.Collectible
                ?.GetBehavior<CollectibleBehaviorGroundStorable>()
                ?.StorageProps;
        }

        int stackQty = slot?.StackSize ?? 0;
        if (burnStack != null)
            stackQty++;

        int modelQty = stackQty;
        if (gsProps?.ModelItemsToStackSizeRatio > 0)
            modelQty = (int)Math.Ceiling(gsProps.ModelItemsToStackSizeRatio * modelQty);

        string meshKey = $"{burnState}-{slotKey}";

        MeshData fuelMesh;
        bool hasMesh = be.CacheMesh($"{ShapePath}{key}/{meshKey}", CacheKey, out fuelMesh, modelQty);
        if (!hasMesh)
            be.CacheMesh($"{ShapePath}firewood/{meshKey}", CacheKey, out fuelMesh, (int)Math.Ceiling(0.5f * stackQty));
        
        fuelMesh.Translate(translate);
        mesher.AddMeshData(fuelMesh);
    }
}