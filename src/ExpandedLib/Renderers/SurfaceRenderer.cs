using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ExpandedLib.Renderers;

/// <summary>
/// Base for renderers that draw a flat, textured horizontal surface (a liquid line) inside a block:
/// boiler water, molten metal in canals, taps and molds. It owns the quad geometry built from a set of
/// footprint boxes and runs the shared standard-shader plumbing; subclasses supply the visibility gate,
/// the surface height, the tint and glow, and the texture binding.
/// <para>
/// One quad is built per footprint box (0-16 pixel space). With <c>combine: true</c> all boxes are
/// merged into a single uploaded mesh that is always drawn whole. With <c>combine: false</c> each box is
/// uploaded separately and <see cref="SelectMeshIndex"/> picks the one to draw, so a multi-level cavity
/// can show the cross-section at the current fill height instead of the union of every level.
/// </para>
/// </summary>
public abstract class SurfaceRenderer : IRenderer {
  protected readonly ICoreClientAPI Api;
  protected readonly BlockPos Pos;
  protected readonly float RotationY;

  // One uploaded quad per footprint box, or a single combined mesh when combine == true.
  protected readonly MeshRef[] MeshRefs;

  public Matrixf ModelMat = new();

  public abstract double RenderOrder { get; }
  public virtual int RenderRange => 24;

  /// <param name="footprintBoxes">Surface footprint boxes in 0-16 pixel space (not 0-1).</param>
  /// <param name="rotationY">Y rotation (radians) matching the block's visual shape.</param>
  /// <param name="combine">
  /// Merge all boxes into one always-drawn mesh (<c>true</c>) or keep one mesh per box and draw the
  /// one chosen by <see cref="SelectMeshIndex"/> (<c>false</c>).
  /// </param>
  protected SurfaceRenderer(
    BlockPos pos,
    ICoreClientAPI api,
    Cuboidf[] footprintBoxes,
    float rotationY,
    bool combine
  ) {
    Pos = pos;
    Api = api;
    RotationY = rotationY;

    if (combine) {
      MeshData merged = new(
        4 * footprintBoxes.Length,
        6 * footprintBoxes.Length
      );
      foreach (Cuboidf box in footprintBoxes)
        merged.AddMeshData(BuildQuad(box));

      MeshRefs = [api.Render.UploadMesh(merged)];
    } else {
      MeshRefs = new MeshRef[footprintBoxes.Length];
      for (int i = 0; i < footprintBoxes.Length; i++)
        MeshRefs[i] = api.Render.UploadMesh(BuildQuad(footprintBoxes[i]));
    }
  }

  /// <summary>
  /// Builds one flat quad for a footprint box: a top-down UV projection plus a transform that scales the
  /// unit quad onto the box and lays it flat. Matrixf post-multiplies, so the rightmost call applies
  /// first: Scale, RotateX, Translate.
  /// </summary>
  private static MeshData BuildQuad(Cuboidf box) {
    MeshData quad = QuadMeshUtil.GetQuad();
    quad.Rgba = new byte[16];
    quad.Rgba.Fill(byte.MaxValue);
    quad.Flags = new int[4];

    // UV mapped from the top-down projection of the box (0-16 px → 0-1 UV).
    quad.Uv =
    [
      box.X2 / 16f,
      box.Z2 / 16f,
      box.X1 / 16f,
      box.Z2 / 16f,
      box.X1 / 16f,
      box.Z1 / 16f,
      box.X2 / 16f,
      box.Z1 / 16f,
    ];

    // GetQuad() is a ±1 unit square at the origin. Scaling by 1/32 (16×2) maps the ±1 extent to width
    // W/16; the quad is then laid flat and translated to the box mid-point.
    float[] matrix = new Matrixf()
      .Translate((box.X1 + box.X2) / 32f, 0f, (box.Z1 + box.Z2) / 32f)
      .RotateX((float)Math.PI / 2f)
      .Scale((box.X2 - box.X1) / 32f, (box.Z2 - box.Z1) / 32f, 1f)
      .Values;

    quad.MatrixTransform(matrix);
    return quad;
  }

  public void OnRenderFrame(float deltaTime, EnumRenderStage stage) {
    if (!ShouldRender || MeshRefs.Length == 0)
      return;

    IRenderAPI render = Api.Render;
    Vec3d camPos = Api.World.Player.Entity.CameraPos;

    render.GlDisableCullFace();
    if (UseBlend)
      render.GlToggleBlend(true);

    IStandardShaderProgram shader = render.StandardShader;
    shader.Use();

    shader.RgbaAmbientIn = render.AmbientColor;
    shader.RgbaFogIn = render.FogColor;
    shader.FogMinIn = render.FogMin;
    shader.FogDensityIn = render.FogDensity;
    shader.DontWarpVertices = 0;
    shader.AddRenderFlags = 0;
    shader.ExtraGodray = 0f;
    shader.NormalShaded = 0;
    shader.RgbaLightIn = Api.World.BlockAccessor.GetLightRGBs(
      Pos.X,
      Pos.Y,
      Pos.Z
    );

    // Subclass-specific tint, glow and temp-glow mode.
    ConfigureShader(shader, render);

    // The subclass binds its texture; a failure aborts the frame so nothing is drawn untextured.
    if (!BindSurfaceTexture(render)) {
      shader.Stop();
      if (UseBlend)
        render.GlToggleBlend(false);
      render.GlEnableCullFace();
      return;
    }

    shader.ModelMatrix = ModelMat
      .Identity()
      .Translate(Pos.X - camPos.X, Pos.Y - camPos.Y, Pos.Z - camPos.Z)
      .Translate(0.5f, 0f, 0.5f)
      .RotateY(RotationY)
      .Translate(-0.5f, 0f, -0.5f)
      .Translate(0f, SurfaceY, 0f)
      .Values;

    shader.ViewMatrix = render.CameraMatrixOriginf;
    shader.ProjectionMatrix = render.CurrentProjectionMatrix;

    int index = GameMath.Clamp(SelectMeshIndex(), 0, MeshRefs.Length - 1);
    render.RenderMesh(MeshRefs[index]);
    shader.Stop();

    if (UseBlend)
      render.GlToggleBlend(false);
    render.GlEnableCullFace();
  }

  /// <summary>Whether the surface is drawn this frame (for example, only when it holds content).</summary>
  protected abstract bool ShouldRender { get; }

  /// <summary>Absolute surface height in block units above the block's base cell.</summary>
  protected abstract float SurfaceY { get; }

  /// <summary>Tint, glow and temp-glow setup, which differs per surface (water against molten metal).</summary>
  protected abstract void ConfigureShader(
    IStandardShaderProgram shader,
    IRenderAPI render
  );

  /// <summary>
  /// Binds the surface texture. Returning <c>false</c> skips the draw for this frame (a texture that
  /// failed to load); the shared plumbing then unwinds the GL state.
  /// </summary>
  protected abstract bool BindSurfaceTexture(IRenderAPI render);

  /// <summary>
  /// Which mesh to draw from <see cref="MeshRefs"/>. The default of 0 suits a combined mesh or a
  /// single-box footprint; override to pick a cross-section by fill level.
  /// </summary>
  protected virtual int SelectMeshIndex() => 0;

  /// <summary>Whether to wrap the draw in alpha blending (translucent surfaces like water).</summary>
  protected virtual bool UseBlend => false;

  public virtual void Dispose() {
    Api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
    foreach (MeshRef meshRef in MeshRefs)
      meshRef?.Dispose();
  }
}
