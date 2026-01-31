using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using OddWire.System;
using OddWire.VintageStory.API.Client;

#nullable disable

namespace OddWire.GameContent;
public class FuelRenderer
{
    public class Properties
    {
        public string DefaultShapeRoot;
        public string ModelKey;
        public bool ShowEmbers;
        public ModelTransform Transform;
        
        public Properties Clone() => new()
            {DefaultShapeRoot = DefaultShapeRoot
            ,ModelKey = ModelKey
            ,ShowEmbers = ShowEmbers
            ,Transform = Transform
            };
    }
    
    public virtual string CacheKey => "fuel-meshes";
    
    private readonly string _defaultShapeRoot;
    private readonly string _modelKey;
    private readonly bool _showEmbers;
    private readonly ModelTransform _transform;

    public FuelRenderer(Properties props)
    {
        _defaultShapeRoot = props.DefaultShapeRoot;
        _modelKey = props.ModelKey ?? "normal";
        _showEmbers = props.ShowEmbers;
        _transform = props.Transform ?? new ModelTransform().EnsureDefaultValues();
    }
    
    public FuelRenderer(string defaultShapeRoot, string modelKey, bool showEmbers, ModelTransform transform)
    {
        _defaultShapeRoot = defaultShapeRoot;
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
                ,be.Block
                ,$"{_defaultShapeRoot}embers/{emberKey}", CacheKey
                ,mesher, transform: _transform
                );
        }
        
        if (renderFuel)
            AddFuel(mesher, tesselator, be, slot, burnState, burnStack);
    }

    private void AddFuel(ITerrainMeshPool mesher, ITesselatorAPI tesselator, BlockEntity be, ItemSlot slot, string burnState, FuelBurnStack burnStack)
    {
        string rootPath;
        float? stackRatio;

        if (burnStack != null)
        {
            rootPath = burnStack.ShapeRoot;
            stackRatio = burnStack.StorageProps?.ModelItemsToStackSizeRatio;
        }
        else
        {
            rootPath = slot?.Itemstack?.Item?.Attributes["shapeFuelStackRoot"]?.ToString()
                  ??   slot?.Itemstack?.Block?.Attributes["shapeFuelStackRoot"]?.ToString();
            stackRatio =
                slot?.Itemstack?.Collectible
                ?.GetBehavior<CollectibleBehaviorGroundStorable>()
                ?.StorageProps
                ?.ModelItemsToStackSizeRatio;
        }

        rootPath ??= $"{_defaultShapeRoot}firewood/";
        stackRatio ??= 0.5f;

        int stackQty = slot?.StackSize ?? 0;
        if (burnStack != null)
            stackQty++;

        int modelQty = stackQty;
        if (stackRatio > 0)
            modelQty = (int)Math.Ceiling(stackRatio.Value * modelQty);
        
        string meshKey = $"{burnState}-{_modelKey}";
        
        bool hasMesh = tesselator.CacheTesselateShape(be.Api, be.Block, $"{rootPath.EndWith('/')}{meshKey}", CacheKey, mesher, modelQty, _transform);
        if (!hasMesh)
            throw new Exception($"Shape not found - {rootPath.EndWith('/')}{meshKey}");
    }
}