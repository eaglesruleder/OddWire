using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace OddWire.Renderers;

public class BlockTint
{
    public MultiTextureMeshRef MeshRef = null;
    public Vec4f Rgba = ColorUtil.WhiteArgbVec;

    public Vec3f Translate = null;
    public Vec3f Origin = null;
    public Vec3f RotationRad = null;
    public Vec3f Scale = new Vec3f(1, 1, 1);

    public int ExtraGlow = 0;
    public bool NormalShaded = true;
    public int RenderRange = 128;

    public bool Enabled = true;
}

public interface IBlockTint
{
    BlockTint BlockTint { get; }
}

public class BlockTintRenderer : IRenderer
{
    protected readonly ICoreClientAPI _api;
    protected readonly BlockPos _pos;
    protected readonly IBlockTint _source;
    protected readonly EnumRenderStage _renderStage;

    public Matrixf ModelMat = new Matrixf();

    public double RenderOrder => 0.5;
    public int RenderRange => Math.Max(_source.BlockTint?.RenderRange ?? 128, 24);

    public BlockTintRenderer(ICoreClientAPI api, BlockEntity blockEntity, EnumRenderStage renderStage = EnumRenderStage.Opaque)
    {
        _api = api;
        _pos = blockEntity.Pos;
        _renderStage = renderStage;

        _source = blockEntity as IBlockTint
    ??  throw new ArgumentException($"{blockEntity.GetType().Name} must implement {nameof(IBlockTint)}", nameof(blockEntity));

        api.Event.RegisterRenderer(this, renderStage);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        BlockTint tint = _source.BlockTint;
        
        if (tint?.Enabled != true)
            return;

        MultiTextureMeshRef meshRef = tint.MeshRef;
        if (meshRef == null
        ||  meshRef.Disposed
            )
            return;

        IRenderAPI rpi = _api.Render;
        Vec3d camPos = _api.World.Player.Entity.CameraPos;

        rpi.GlDisableCullFace();
        rpi.GlToggleBlend(true);

        IStandardShaderProgram prog = null;
        try
        {
            prog = rpi.PreparedStandardShader(_pos.X, _pos.Y, _pos.Z);

            prog.RgbaTint = tint.Rgba ?? ColorUtil.WhiteArgbVec;
            prog.RgbaLightIn = _api.World.BlockAccessor.GetLightRGBs(_pos.X, _pos.Y, _pos.Z);
            prog.ExtraGlow = GameMath.Clamp(tint.ExtraGlow, 0, 255);
            prog.NormalShaded = tint.NormalShaded ? 1 : 0;

            prog.ModelMatrix = GetModelMatrix(camPos, tint);
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMultiTextureMesh(meshRef, "tex");
        }
        finally
        {
            prog?.Stop();
            rpi.GlToggleBlend(false);
            rpi.GlEnableCullFace();
        }
    }

    protected virtual float[] GetModelMatrix(Vec3d camPos, BlockTint tint)
    {
        ModelMat
            .Identity()
            .Translate(_pos.X - camPos.X, _pos.Y - camPos.Y, _pos.Z - camPos.Z);

        Vec3f translate = tint.Translate;
        if (translate is not null)
            ModelMat.Translate(translate.X, translate.Y, translate.Z);

        Vec3f origin = tint.Origin;
        if (origin is not null)
            ModelMat.Translate(origin.X, origin.Y, origin.Z);

        Vec3f rotation = tint.RotationRad;
        if (rotation is not null)
        {
            if (rotation.X != 0)
                ModelMat.RotateX(rotation.X);
            if (rotation.Y != 0)
                ModelMat.RotateY(rotation.Y);
            if (rotation.Z != 0)
                ModelMat.RotateZ(rotation.Z);
        }

        Vec3f scale = tint.Scale;
        if (scale is not null
        &&!(scale.X.Approx(1) && scale.Y.Approx(1) && scale.Z.Approx(1))
            )
            ModelMat.Scale(scale.X, scale.Y, scale.Z);

        if (origin is not null)
            ModelMat.Translate(-origin.X, -origin.Y, -origin.Z);

        return ModelMat.Values;
    }

    public void Dispose() =>
        _api.Event.UnregisterRenderer(this, _renderStage);
}