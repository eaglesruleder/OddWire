using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace OddWire.VintageStory.API.Common
{
    public static class BlockEntityExtensions
    {
        public static bool CacheMesh(this BlockEntity blockEntity, string path, string cacheKey, out MeshData meshdata, int? quantityElements = null, Action<MeshData> onCreate = null)
        {
            string meshKey = $"{path}#{quantityElements}";
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(blockEntity.Api, cacheKey, () => new Dictionary<string, MeshData>());
            if (meshes.TryGetValue(meshKey, out meshdata))
                return true;
            
            Block block = blockEntity.Api.World.BlockAccessor.GetBlock(blockEntity.Pos);
            if (block.BlockId == 0)
                return false;

            Shape shape = Shape.TryGet(blockEntity.Api, $"{path}.json");
            if (shape == null)
                return false;
            
            ITesselatorAPI mesher = ((ICoreClientAPI)blockEntity.Api).Tesselator;
            mesher.TesselateShape(block, shape, out meshdata, quantityElements: quantityElements);
            onCreate?.Invoke(meshdata);
            meshes.TryAdd(meshKey, meshdata);
            return true;
        }
    }
}
