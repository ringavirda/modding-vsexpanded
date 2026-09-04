using ExpandedLib.Renderers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.OreProcessing;

/// <summary>
/// Draws the flat top surface of the granular charge (crushed ore, flux, coke or finished burden) heaped
/// inside a burdenmaker's hoppers or basin. One opaque, ore-textured quad spans the vessel's interior
/// footprint; its height interpolates with <see cref="Fill"/> between an empty floor line
/// (<see cref="_yMin"/>) and a brim-full line (<see cref="_yMax"/>). A <see cref="Fill"/> of zero hides
/// the surface.
/// </summary>
public class OreSurfaceRenderer : SurfaceRenderer {
  private readonly int _textureId;
  private readonly float _yMin;
  private readonly float _yMax;

  /// <summary>Fill fraction 0..1 of the vessel; drives the surface height (0 hides it).</summary>
  public float Fill;

  // Drawn in the opaque pass with the rest of the block: depth-tested, no blend.
  public override double RenderOrder => 0.5;

  /// <param name="footprintBoxes">Interior surface footprint in 0-16 pixel space (block-local).</param>
  /// <param name="rotationY">Y rotation (radians) matching the block's structure orientation.</param>
  /// <param name="yMin">Surface height (block units) of an almost-empty vessel.</param>
  /// <param name="yMax">Surface height (block units) of a brim-full vessel.</param>
  /// <param name="texture">The granular surface texture (an ore-mix png).</param>
  public OreSurfaceRenderer(
    BlockPos pos,
    ICoreClientAPI api,
    Cuboidf[] footprintBoxes,
    float rotationY,
    float yMin,
    float yMax,
    AssetLocation texture
  )
    : base(pos, api, footprintBoxes, rotationY, combine: true) {
    _yMin = yMin;
    _yMax = yMax;
    _textureId = api.Render.GetOrLoadTexture(texture);
  }

  protected override bool ShouldRender => Fill > 0f && _textureId != 0;

  protected override float SurfaceY =>
    _yMin + GameMath.Clamp(Fill, 0f, 1f) * (_yMax - _yMin);

  protected override void ConfigureShader(
    IStandardShaderProgram shader,
    IRenderAPI render
  ) {
    // Opaque, untinted granular surface lit by the block's own light: no glow, no blend.
    shader.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
    shader.TempGlowMode = 0;
    shader.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
    shader.ExtraGlow = 0;
  }

  protected override bool BindSurfaceTexture(IRenderAPI render) {
    render.BindTexture2d(_textureId);
    return true;
  }
}
