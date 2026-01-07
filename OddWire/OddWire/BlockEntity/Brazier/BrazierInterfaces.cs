using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace OddWire.GameContent
{
    public interface IBrazier
    {
        bool IsBurning { get; }
    }

    public enum EnumBrazierModel
    {
        Normal = 0,
        Spit = 1,
        Wide = 2
    }

    public interface IInBrazierMeshSupplier
    {
        /// <summary>
        /// Return the mesh you want to be rendered in the brazier. You can return null to signify that you do not wish to use a custom mesh.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="brazierModel"></param>
        /// <returns></returns>
        MeshData GetMeshWhenInBrazier(ItemStack stack, IWorldAccessor world, BlockPos pos, ref EnumBrazierModel brazierModel);
    }

    public class InBrazierProps
    {
        public ModelTransform Transform;
        public EnumBrazierModel UseBrazierModel;
    }

    public interface IInBrazierRenderer : IRenderer
    {
        /// <summary>
        /// Called every 100ms in case you want to do custom stuff, such as playing a sound after a certain temperature
        /// </summary>
        /// <param name="temperature"></param>
        void OnUpdate(float temperature);

        /// <summary>
        /// Called when the itemstack has been moved to the output slot
        /// </summary>
        void OnCookingComplete();
    }

    public interface IInBrazierRendererSupplier
    {
        /// <summary>
        /// Return the renderer that perfroms the rendering of your block/item in the brazier. You can return null to signify that you do not wish to use a custom renderer
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        IInBrazierRenderer GetRendererWhenInBrazier(ItemStack stack, BlockEntityBrazier brazier, bool forOutputSlot);

        /// <summary>
        /// The model type the brazier should be using while you render your custom item
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        EnumBrazierModel GetDesiredBrazierModel(ItemStack stack, BlockEntityBrazier brazier, bool forOutputSlot);
    }

}