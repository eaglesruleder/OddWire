using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using OddWire.VintageStory.API.Client;

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

    public void Tesselate(ITerrainMeshPool mesher, ITesselatorAPI tesselator, BlockEntity be, ItemSlot slot, string burnState, FuelBurnStack burnStack, bool? showEmbers = null)
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
            bool isBurning = (be as IFirePit)?.IsBurning == true;
            string emberKey = renderFuel
                ? $"{burnState}-{_modelKey}"
                : isBurning ? $"extinct-{_modelKey}" : $"cold-{_modelKey}";
            tesselator.CacheTesselateShape
                (be.Api
                ,slot.Itemstack.Collectible
                ,$"{ShapePath}embers/{emberKey}", CacheKey
                ,mesher, transform: _transform
                );
        }
        
        if (renderFuel)
            AddFuel(mesher, tesselator, be, slot, burnState, burnStack);
    }

    private void AddFuel(ITerrainMeshPool mesher, ITesselatorAPI tesselator, BlockEntity be, ItemSlot slot, string burnState, FuelBurnStack burnStack)
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
        
        bool hasMesh = tesselator.CacheTesselateShape(be.Api, be.Block, $"{ShapePath}{key}/{meshKey}", CacheKey, mesher, modelQty, _transform);
        if (!hasMesh)
            tesselator.CacheTesselateShape(be.Api, be.Block, $"{ShapePath}firewood/{meshKey}", CacheKey, mesher, (int)Math.Ceiling(0.5f * stackQty), _transform);
    }
}