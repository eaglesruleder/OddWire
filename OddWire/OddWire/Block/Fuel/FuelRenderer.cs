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
    public class Properties
    {
        public string ShapePath;
        public string ModelKey;
        public bool ShowEmbers;
        public ModelTransform Transform;
        
        public Properties Clone() => new()
            {ShapePath = ShapePath
            ,ModelKey = ModelKey
            ,ShowEmbers = ShowEmbers
            ,Transform = Transform
            };
    }
    
    public virtual string CacheKey => "fuel-meshes";
    
    public readonly string ShapePath;
    private readonly string _modelKey;
    private readonly bool _showEmbers;
    private readonly ModelTransform _transform;

    public FuelRenderer(Properties props)
    {
        ShapePath = props.ShapePath;
        _modelKey = props.ModelKey ?? "normal";
        _showEmbers = props.ShowEmbers;
        _transform = props.Transform ?? new ModelTransform().EnsureDefaultValues();
    }
    
    public FuelRenderer(string shapePath, string modelKey, bool showEmbers, ModelTransform transform)
    {
        ShapePath = shapePath;
        _modelKey = modelKey;
        _showEmbers = showEmbers;
        _transform = transform ?? new ModelTransform().EnsureDefaultValues();
    }

    public void Tesselate(ITerrainMeshPool mesher, BlockEntity be, ItemSlot slot, string burnState, FuelBurnStack burnStack, bool? showEmbers = null)
    {
        if (mesher == null
        ||  be == null
        ||  burnState == null
            ) return;
        
        bool renderFuel =
            burnStack != null
        ||  slot?.StackSize > 0;

        if (showEmbers ?? _showEmbers)
        {
            bool isBurning = be is IFirePit firepit && firepit.IsBurning;
            string emberKey = renderFuel
                ? $"{burnState}-{_modelKey}"
                : isBurning ? $"extinct-{_modelKey}" : $"cold-{_modelKey}";
            AddEmbers(mesher, be, emberKey);
        }
        
        if (renderFuel)
            AddFuel(mesher, be, slot, burnState, burnStack);
    }

    private void AddEmbers(ITerrainMeshPool mesher, BlockEntity be, string meshKey)
    {
        if (be.CacheMesh($"{ShapePath}embers/{meshKey}", CacheKey, out MeshData embersMesh, transform: _transform))
            mesher.AddMeshData(embersMesh);
    }

    private void AddFuel(ITerrainMeshPool mesher, BlockEntity be, ItemSlot slot, string burnState, FuelBurnStack burnStack)
    {
        string key;
        GroundStorageProperties gsProps;

        if (burnStack != null)
        {
            key = burnStack.Key;
            gsProps = burnStack.StorageProps;
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
        
        string meshKey = $"{burnState}-{_modelKey}";

        MeshData fuelMesh;
        bool hasMesh = be.CacheMesh($"{ShapePath}{key}/{meshKey}", CacheKey, out fuelMesh, modelQty, _transform);
        if (!hasMesh)
            be.CacheMesh($"{ShapePath}firewood/{meshKey}", CacheKey, out fuelMesh, (int)Math.Ceiling(0.5f * stackQty), _transform);
        mesher.AddMeshData(fuelMesh);
    }
}