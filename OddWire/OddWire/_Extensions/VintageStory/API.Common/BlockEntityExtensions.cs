using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace OddWire.VintageStory.API.Common
{
    public static class BlockEntityExtensions
    {
        public static MeshData CacheMesh(this BlockEntity blockEntity, string path, string cacheKey)
        {
            Dictionary<string, MeshData> Meshes = ObjectCacheUtil.GetOrCreate(blockEntity.Api, cacheKey, () => new Dictionary<string, MeshData>());
            if (!Meshes.TryGetValue(path, out MeshData meshdata))
            {
                Block block = blockEntity.Api.World.BlockAccessor.GetBlock(blockEntity.Pos);
                if (block.BlockId == 0)
                    return null;

                ITesselatorAPI mesher = ((ICoreClientAPI)blockEntity.Api).Tesselator;
                mesher.TesselateShape(block, Shape.TryGet(blockEntity.Api, $"{path}.json"), out meshdata);
            }

            return meshdata;
        }
    }
}
