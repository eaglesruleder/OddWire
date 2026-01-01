using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace OddWire.GameContent
{
    public class BrazierContentsRenderer : IRenderer
    {
        MultiTextureMeshRef meshref;
        MultiTextureMeshRef[] craftingMeshRefs;
        ICoreClientAPI api;
        BlockPos pos;
        public ItemStack ContentStack;
        int textureId;
        Matrixf ModelMat = new Matrixf();

        ModelTransform transform;
        ModelTransform defaultTransform;
        ItemStack[] craftingStacks;
        Vec3f[] craftingOffsets;

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
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 48; }
        }

        public BrazierContentsRenderer(ICoreClientAPI api, BlockPos pos)
        {
            this.api = api;
            this.pos = pos;
            transform = new ModelTransform().EnsureDefaultValues();
            transform.Origin.X = 8 / 16f;
            transform.Origin.Y = 1 / 16f;
            transform.Origin.Z = 8 / 16f;
            transform.Rotation.X = 90;
            transform.Rotation.Y = 90;
            transform.Rotation.Z = 0;
            transform.Translation.X = 0 / 32f;
            transform.Translation.Y = 4f / 16f;
            transform.Translation.Z = 0 / 32f;
            transform.ScaleXYZ.X = 0.25f;
            transform.ScaleXYZ.Y = 0.25f;
            transform.ScaleXYZ.Z = 0.25f;

            defaultTransform = transform;

        }


        internal void SetChildRenderer(ItemStack contentStack, IInBrazierRenderer renderer)
        {
            this.ContentStack = contentStack;
            meshref?.Dispose();
            meshref = null;
            ClearCraftingMeshes();
            
            contentStackRenderer = renderer;
        }

        internal void SetCraftingSteps(ItemStack[] steps)
        {
            contentStackRenderer?.Dispose();
            contentStackRenderer = null;

            meshref?.Dispose();
            meshref = null;

            ClearCraftingMeshes();

            if (steps == null || steps.Length == 0)
            {
                ContentStack = null;
                return;
            }

            craftingStacks = steps;
            craftingMeshRefs = new MultiTextureMeshRef[steps.Length];
            craftingOffsets = new Vec3f[steps.Length];

            for (int index = 0; index < steps.Length; index++)
            {
                ItemStack stack = steps[index];
                if (stack == null) continue;

                MeshData ingredientMesh;
                if (stack.Class == EnumItemClass.Item)
                {
                    api.Tesselator.TesselateItem(stack.Item, out ingredientMesh);
                }
                else
                {
                    api.Tesselator.TesselateBlock(stack.Block, out ingredientMesh);
                }

                craftingMeshRefs[index] = api.Render.UploadMultiTextureMesh(ingredientMesh);
                craftingOffsets[index] = GetCraftingOffset(index, steps.Length);
            }

            ContentStack = steps[0];
        }

        public void SetContents(ItemStack newContentStack, ModelTransform transform)
        {
            contentStackRenderer?.Dispose();
            contentStackRenderer = null;
            ClearCraftingMeshes();

            this.transform = transform;
            if (transform == null) this.transform = defaultTransform;
            this.transform.EnsureDefaultValues();

            meshref?.Dispose();
            meshref = null;
            
            if (newContentStack == null || newContentStack.Class == EnumItemClass.Block)
            {
                this.ContentStack = null;
                return;
            }

            MeshData ingredientMesh;
            if (newContentStack.Class == EnumItemClass.Item)
            {
                api.Tesselator.TesselateItem(newContentStack.Item, out ingredientMesh);
                textureId = api.ItemTextureAtlas.Positions[newContentStack.Item.FirstTexture.Baked.TextureSubId].atlasTextureId;
            }
            else
            {
                api.Tesselator.TesselateBlock(newContentStack.Block, out ingredientMesh);
                textureId = api.ItemTextureAtlas.Positions[newContentStack.Block.Textures.FirstOrDefault().Value.Baked.TextureSubId].atlasTextureId;
            }

            meshref = api.Render.UploadMultiTextureMesh(ingredientMesh);
            this.ContentStack = newContentStack;
        }

        void ClearCraftingMeshes()
        {
            if (craftingMeshRefs == null) return;

            for (int i = 0; i < craftingMeshRefs.Length; i++)
            {
                craftingMeshRefs[i]?.Dispose();
            }

            craftingMeshRefs = null;
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

            if (meshref == null && (craftingMeshRefs == null || craftingMeshRefs.Length == 0)) return;
            
            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;

            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);

            if (meshref != null)
            {
                int temp = (int)ContentStack.Collectible.GetTemperature(api.World, ContentStack);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
                lightrgbs[0] += glowColor[0];
                lightrgbs[1] += glowColor[1];
                lightrgbs[2] += glowColor[2];

                prog.RgbaLightIn = lightrgbs;
                prog.ExtraGlow = (int)GameMath.Clamp((temp - 500) / 4, 0, 255);

                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X + transform.Translation.X, pos.Y - camPos.Y + transform.Translation.Y, pos.Z - camPos.Z + transform.Translation.Z)
                    .Translate(transform.Origin.X, 0.6f + transform.Origin.Y, transform.Origin.Z)
                    .RotateX(transform.Rotation.X * GameMath.DEG2RAD)
                    .RotateY(transform.Rotation.Y * GameMath.DEG2RAD)
                    .RotateZ(transform.Rotation.Z * GameMath.DEG2RAD)
                    .Scale(transform.ScaleXYZ.X, transform.ScaleXYZ.Y, transform.ScaleXYZ.Z)
                    .Translate(-transform.Origin.X, -transform.Origin.Y, -transform.Origin.Z)
                    .Values
                ;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(meshref, "tex");
            }

            if (craftingMeshRefs != null)
            {
                for (int i = 0; i < craftingMeshRefs.Length; i++)
                {
                    MultiTextureMeshRef mesh = craftingMeshRefs[i];
                    ItemStack stack = craftingStacks?[i];
                    if (mesh == null || stack == null) continue;

                    int temp = (int)stack.Collectible.GetTemperature(api.World, stack);
                    float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
                    Vec4f stackLight = new Vec4f(lightrgbs[0] + glowColor[0], lightrgbs[1] + glowColor[1], lightrgbs[2] + glowColor[2], lightrgbs[3]);

                    prog.RgbaLightIn = stackLight;
                    prog.ExtraGlow = (int)GameMath.Clamp((temp - 500) / 4, 0, 255);

                    Vec3f offset = craftingOffsets[i];
                    prog.ModelMatrix = ModelMat
                        .Identity()
                        .Translate(pos.X - camPos.X + offset.X, pos.Y - camPos.Y + offset.Y, pos.Z - camPos.Z + offset.Z)
                        .Scale(0.25f, 0.25f, 0.25f)
                        .Values
                    ;

                    prog.ViewMatrix = rpi.CameraMatrixOriginf;
                    prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                    rpi.RenderMultiTextureMesh(mesh, "tex");
                }
            }

            prog.Stop();
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

            meshref?.Dispose();
            ClearCraftingMeshes();
            contentStackRenderer?.Dispose();
        }

    }
}
