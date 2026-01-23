using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace OddWire.VintageStory.API.Common
{
    public static class BlockEntityExtensions
    {
        public static bool CacheMesh
            (this BlockEntity blockEntity
            ,string path, string cacheKey
            ,out MeshData meshdata
            ,int? qtyRootElements = null
            ,ModelTransform? transform = null
            )
        {
            string meshKey = $"{path}{transform?.ToKey()}#{qtyRootElements}";
            
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(blockEntity.Api, cacheKey, () => new Dictionary<string, MeshData>());
            if (meshes.TryGetValue(meshKey, out meshdata))
                return true;
            
            Block block = blockEntity.Api.World.BlockAccessor.GetBlock(blockEntity.Pos);
            if (block.BlockId == 0)
                return false;

            Shape shape = Shape.TryGet(blockEntity.Api, $"{path}.json");
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
            
            ITesselatorAPI mesher = ((ICoreClientAPI)blockEntity.Api).Tesselator;
            mesher.TesselateShape(block, shape, out meshdata, quantityElements: renderElements);
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
