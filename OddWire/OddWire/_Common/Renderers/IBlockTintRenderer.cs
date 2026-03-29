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

    public static Vec4f Rgba(int r, int g, int b, int a = 255) => new Vec4f
        (GameMath.Clamp(r, 0, 255) / 255f
        ,GameMath.Clamp(g, 0, 255) / 255f
        ,GameMath.Clamp(b, 0, 255) / 255f
        ,GameMath.Clamp(a, 0, 255) / 255f
        );

    /// <summary>
    /// hueDeg: 0..360
    /// saturation: 0..1
    /// value: 0..1
    /// alpha: 0..1
    /// </summary>
    public static Vec4f Hsv(float hueDeg, float saturation, float value, float alpha = 1f)
    {
        hueDeg = ((hueDeg % 360f) + 360f) % 360f;
        saturation = GameMath.Clamp(saturation, 0f, 1f);
        value = GameMath.Clamp(value, 0f, 1f);
        alpha = GameMath.Clamp(alpha, 0f, 1f);

        float c = value * saturation;
        float x = c * (1f - Math.Abs((hueDeg / 60f % 2f) - 1f));
        float m = value - c;

        float r = 0f;
        float g = 0f;
        float b = 0f;

        if (hueDeg < 60f)
            { r = c; g = x; b = 0f; }
        else if (hueDeg < 120f)
            { r = x; g = c; b = 0f; }
        else if (hueDeg < 180f)
            { r = 0f; g = c; b = x; }
        else if (hueDeg < 240f)
            { r = 0f; g = x; b = c; }
        else if (hueDeg < 300f)
            { r = x; g = 0f; b = c; }
        else
            { r = c; g = 0f; b = x; }

        return new Vec4f(r + m, g + m, b + m, alpha);
    }
}