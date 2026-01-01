using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace OddWire.GameContent
{
    public class BrazierContentsRenderer : IRenderer
    {
        readonly ICoreClientAPI api;
        readonly StackContentsRenderer stackRenderer;
        readonly ModelTransform defaultTransform;
        readonly Vec3f defaultOffset;

        ItemStack[] craftingStacks;
        Vec3f[] craftingOffsets;

        public ItemStack ContentStack;
        public IInBrazierRenderer contentStackRenderer;

        public bool RequireSpit
        {
            get
            {
                return contentStackRenderer == null && ContentStack?.Item != null;
            }
        }

        public double RenderOrder
        {
            get { return stackRenderer.RenderOrder; }
        }

        public int RenderRange
        {
            get { return stackRenderer.RenderRange; }
        }

        public BrazierContentsRenderer(ICoreClientAPI api, BlockPos pos, ModelTransform defaultTransform, Vec3f defaultOffset)
        {
            this.api = api;
            stackRenderer = new StackContentsRenderer(api, pos);
            this.defaultTransform = defaultTransform;
            this.defaultOffset = defaultOffset ?? new Vec3f();
        }

        internal void SetChildRenderer(ItemStack contentStack, IInBrazierRenderer renderer)
        {
            ContentStack = contentStack;
            stackRenderer.SetStacks(null, (ModelTransform)null, null);
            ClearCraftingMeshes();
            contentStackRenderer = renderer;
        }

        internal void SetCraftingSteps(ItemStack[] steps)
        {
            contentStackRenderer?.Dispose();
            contentStackRenderer = null;
            ClearCraftingMeshes();

            if (steps == null || steps.Length == 0)
            {
                ContentStack = null;
                stackRenderer.SetStacks(null, (ModelTransform)null, null);
                return;
            }

            craftingStacks = steps;
            craftingOffsets = new Vec3f[steps.Length];
            for (int index = 0; index < steps.Length; index++)
            {
                craftingOffsets[index] = GetCraftingOffset(index, steps.Length);
            }

            ModelTransform craftingTransform = new ModelTransform().EnsureDefaultValues();
            craftingTransform.ScaleXYZ.X = 0.25f;
            craftingTransform.ScaleXYZ.Y = 0.25f;
            craftingTransform.ScaleXYZ.Z = 0.25f;

            stackRenderer.SetStacks(craftingStacks, craftingTransform, craftingOffsets);
            ContentStack = steps[0];
        }

        public void SetContents(ItemStack newContentStack, ModelTransform transform)
        {
            contentStackRenderer?.Dispose();
            contentStackRenderer = null;
            ClearCraftingMeshes();

            if (newContentStack == null || newContentStack.Class == EnumItemClass.Block)
            {
                ContentStack = null;
                stackRenderer.SetStacks(null, (ModelTransform)null, null);
                return;
            }

            ModelTransform resolvedTransform = transform ?? defaultTransform;
            if (resolvedTransform == null)
            {
                resolvedTransform = new ModelTransform().EnsureDefaultValues();
            }
            resolvedTransform.EnsureDefaultValues();

            stackRenderer.SetStacks(new[] { newContentStack }, resolvedTransform, new[] { defaultOffset });
            ContentStack = newContentStack;
        }

        void ClearCraftingMeshes()
        {
            craftingStacks = null;
            craftingOffsets = null;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (contentStackRenderer != null)
            {
                contentStackRenderer.OnRenderFrame(deltaTime, stage);
                return;
            }

            stackRenderer.OnRenderFrame(deltaTime, stage);
        }

        Vec3f GetCraftingOffset(int index, int total)
        {
            if (total <= 1) return new Vec3f(0.5f, 0.5f, 0.5f);

            float angle = (float)(index * GameMath.TWOPI / total);
            float radius = 0.18f;
            float x = 0.5f + GameMath.Cos(angle) * radius;
            float z = 0.5f + GameMath.Sin(angle) * radius;
            return new Vec3f(x, 0.5f, z);
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            stackRenderer?.Dispose();
            ClearCraftingMeshes();
            contentStackRenderer?.Dispose();
        }
    }
}
