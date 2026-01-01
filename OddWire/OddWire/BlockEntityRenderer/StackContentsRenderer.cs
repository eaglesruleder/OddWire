using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace OddWire.GameContent
{
    public class StackContentsRenderer : IRenderer
    {
        readonly ICoreClientAPI api;
        readonly BlockPos pos;
        readonly Matrixf modelMat = new Matrixf();
        readonly ModelTransform defaultTransform = new ModelTransform().EnsureDefaultValues();

        MultiTextureMeshRef[] meshRefs;
        ItemStack[] stacks;
        Vec3f[] offsets;
        ModelTransform[] transforms;

        public double RenderOrder => 0.5;

        public int RenderRange => 48;

        public StackContentsRenderer(ICoreClientAPI api, BlockPos pos)
        {
            this.api = api;
            this.pos = pos;
        }

        public void SetInventory(InventoryBase inventory, Func<int, Vec3f> offsetSelector, ModelTransform transform = null)
        {
            if (inventory == null)
            {
                SetStacks(null, transform, null);
                return;
            }

            ItemStack[] inventoryStacks = new ItemStack[inventory.Count];
            Vec3f[] inventoryOffsets = new Vec3f[inventory.Count];

            for (int i = 0; i < inventory.Count; i++)
            {
                inventoryStacks[i] = inventory[i].Itemstack;
                inventoryOffsets[i] = offsetSelector?.Invoke(i);
            }

            SetStacks(inventoryStacks, transform, inventoryOffsets);
        }

        public void SetSlots(ItemSlot[] slots, Func<int, Vec3f> offsetSelector, ModelTransform transform = null)
        {
            if (slots == null)
            {
                SetStacks(null, transform, null);
                return;
            }

            ItemStack[] slotStacks = new ItemStack[slots.Length];
            Vec3f[] slotOffsets = new Vec3f[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                slotStacks[i] = slots[i]?.Itemstack;
                slotOffsets[i] = offsetSelector?.Invoke(i);
            }

            SetStacks(slotStacks, transform, slotOffsets);
        }

        public void SetStacks(ItemStack[] stacks, ModelTransform transform, Vec3f[] offsets)
        {
            if (stacks == null)
            {
                SetStacks(null, (ModelTransform[])null, offsets);
                return;
            }

            ModelTransform[] sharedTransforms = null;
            if (transform != null)
            {
                sharedTransforms = new ModelTransform[stacks.Length];
                for (int i = 0; i < stacks.Length; i++)
                {
                    sharedTransforms[i] = transform;
                }
            }

            SetStacks(stacks, sharedTransforms, offsets);
        }

        public void SetStacks(ItemStack[] stacks, ModelTransform[] transforms, Vec3f[] offsets)
        {
            ClearMeshes();

            if (stacks == null || stacks.Length == 0)
            {
                this.stacks = null;
                this.offsets = null;
                this.transforms = null;
                return;
            }

            this.stacks = stacks;
            this.offsets = offsets;
            this.transforms = NormalizeTransforms(stacks.Length, transforms);
            meshRefs = new MultiTextureMeshRef[stacks.Length];

            for (int index = 0; index < stacks.Length; index++)
            {
                ItemStack stack = stacks[index];
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

                meshRefs[index] = api.Render.UploadMultiTextureMesh(ingredientMesh);
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshRefs == null || meshRefs.Length == 0) return;

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

            for (int i = 0; i < meshRefs.Length; i++)
            {
                MultiTextureMeshRef mesh = meshRefs[i];
                ItemStack stack = stacks?[i];
                if (mesh == null || stack == null) continue;

                int temp = (int)stack.Collectible.GetTemperature(api.World, stack);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
                Vec4f stackLight = new Vec4f(lightrgbs[0] + glowColor[0], lightrgbs[1] + glowColor[1], lightrgbs[2] + glowColor[2], lightrgbs[3]);

                prog.RgbaLightIn = stackLight;
                prog.ExtraGlow = (int)GameMath.Clamp((temp - 500) / 4, 0, 255);

                Vec3f offset = GetOffset(i);
                ModelTransform transform = GetTransform(i);

                prog.ModelMatrix = modelMat
                    .Identity()
                    .Translate(pos.X - camPos.X + offset.X + transform.Translation.X, pos.Y - camPos.Y + offset.Y + transform.Translation.Y, pos.Z - camPos.Z + offset.Z + transform.Translation.Z)
                    .Translate(transform.Origin.X, transform.Origin.Y, transform.Origin.Z)
                    .RotateX(transform.Rotation.X * GameMath.DEG2RAD)
                    .RotateY(transform.Rotation.Y * GameMath.DEG2RAD)
                    .RotateZ(transform.Rotation.Z * GameMath.DEG2RAD)
                    .Scale(transform.ScaleXYZ.X, transform.ScaleXYZ.Y, transform.ScaleXYZ.Z)
                    .Translate(-transform.Origin.X, -transform.Origin.Y, -transform.Origin.Z)
                    .Values
                ;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(mesh, "tex");
            }

            prog.Stop();
        }

        Vec3f GetOffset(int index)
        {
            if (offsets == null || index >= offsets.Length || offsets[index] == null)
            {
                return new Vec3f();
            }

            return offsets[index];
        }

        ModelTransform GetTransform(int index)
        {
            if (transforms == null || index >= transforms.Length || transforms[index] == null)
            {
                return defaultTransform;
            }

            return transforms[index];
        }

        ModelTransform[] NormalizeTransforms(int count, ModelTransform[] transforms)
        {
            if (transforms == null || transforms.Length == 0) return null;

            ModelTransform[] result = new ModelTransform[count];
            for (int i = 0; i < count; i++)
            {
                ModelTransform transform = i < transforms.Length ? transforms[i] : null;
                if (transform == null) transform = defaultTransform;
                transform.EnsureDefaultValues();
                result[i] = transform;
            }

            return result;
        }

        void ClearMeshes()
        {
            if (meshRefs == null) return;

            for (int i = 0; i < meshRefs.Length; i++)
            {
                meshRefs[i]?.Dispose();
            }

            meshRefs = null;
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            ClearMeshes();
        }
    }
}
