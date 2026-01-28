using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using OddWire.VintageStory.API.Common;

namespace OddWire.VintageStory.API.Client
{
    public static class ITesselatorAPIExtensions
    {
        public static bool CacheTesselateShape
            (this ITesselatorAPI tesselator
            ,ICoreAPI api
            ,CollectibleObject textureCollectible
            ,string path, string cacheKey
            ,ITerrainMeshPool mesher
            ,int? qtyRootElements = null
            ,ModelTransform? transform = null
            )
        {
            if(!tesselator.CacheTesselateShape(api, textureCollectible, path, cacheKey, out MeshData meshData, qtyRootElements, transform))
                return false;
            
            if(mesher is not null)
                mesher.AddMeshData(meshData);
            return true;
        }
        
        public static bool CacheTesselateShape
            (this ITesselatorAPI tesselator
            ,ICoreAPI api
            ,CollectibleObject textureCollectible
            ,string path, string cacheKey
            ,out MeshData meshdata
            ,int? qtyRootElements = null
            ,ModelTransform? transform = null
            )
        {
            string meshKey = $"{path}{transform?.ToKey()}#{qtyRootElements}";
            
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(api, cacheKey, () => new Dictionary<string, MeshData>());
            if (meshes.TryGetValue(meshKey, out meshdata))
                return true;

            Shape shape = Shape.TryGet(api, $"{path}.json");
            if (shape == null)
                return false;

            //  Apply quantityElements as qty Root, not qty Nested
            int? renderElements = null;
            if (qtyRootElements != null)
            {
                renderElements = 0;
                for (int i = 0; i < qtyRootElements.Value && i < shape.Elements.Length; i++)
                    renderElements += 1 + CountElementChildren(shape.Elements[i].Children);
            }
            
            tesselator.TesselateShape(textureCollectible, shape, out meshdata, quantityElements: renderElements);
            if(transform is not null)
                meshdata.ModelTransform(transform);
            
            if(cacheKey is not null)
                meshes.TryAdd(meshKey, meshdata);
            return true;
        }

        private static int CountElementChildren(ShapeElement[] children)
        {
            if (children == null || children.Length == 0)
                return 0;
            
            int result = 0;
            for (int i = 0; i < children.Length; i++)
                result += 1 + CountElementChildren(children[i].Children);
            
            return result;
        }
    }
}
