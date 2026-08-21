using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The standalone firebox block's entity: a furnace part that hosts a <see cref="BEBehaviorFirebox"/> fuel
/// bed and draws it. All fuel state lives in the behaviour, because the boilers host the same behaviour
/// internally with no block of this type involved; what is left here is drawing the bed and reporting its
/// contents. See docs/design/machines/firebox.md.
/// </summary>
[BlockEntityRegister]
public class BlockEntityFirebox : BlockEntityFurnacePart {
  /// <summary>The fuel bed. Null only if the blocktype forgot to declare the behaviour.</summary>
  public BEBehaviorFirebox? Bed => GetBehavior<BEBehaviorFirebox>();

  #region Charging

  /// <summary>
  /// Charges this bed and every other firebox cell of the owning furnace from <paramref name="stack"/>,
  /// returning the total units taken: fireboxes of one furnace share one pool, so one interaction fills the
  /// whole bed and the fuel cost scales with the cell count. Cells come from the owning core's
  /// <c>CellRole.Firebox</c> list rather than by adjacency, so two furnaces built back to back do not merge
  /// their fuel, and a firebox with no core fills only itself. This cell is filled first so a single-unit
  /// deposit lands where the player clicked, then the rest in the layout's cell order.
  /// See docs/design/machines/firebox.md.
  /// </summary>
  /// <summary>
  /// Whether this cell would take <paramref name="stack"/>, by the owning machine's rule rather than the
  /// bed's. A firebox with no owning furnace falls back to the plain fuel test, which is also what it
  /// charges by.
  /// </summary>
  public bool Accepts(ItemStack? stack) =>
    Core is BlockEntityFireboxFurnace furnace
      ? furnace.AcceptsFireboxFuel(stack)
      : BEBehaviorFirebox.IsFuel(stack);

  public int Charge(ItemStack? stack) {
    if (stack == null || Bed is not { } here)
      return 0;

    int remaining = stack.StackSize;
    int taken = here.TryAdd(stack, remaining);
    remaining -= taken;

    if (Core is not { } core)
      return taken;

    foreach (BlockPos cell in core.FireboxCells) {
      if (remaining <= 0)
        break;
      if (
        cell.Equals(Pos)
        || Api.World.BlockAccessor.GetBlockEntity(cell)
          is not BlockEntityFirebox other
        || other.Bed is not { } bed
      )
        continue;
      int more = bed.TryAdd(stack, remaining);
      remaining -= more;
      taken += more;
    }
    return taken;
  }

  #endregion

  #region Render

  // The tesselation thread reads these fields and nothing else - not the behaviour, not Core - because
  // resolving the anchor writes to the link's cache. Snapshotted on every state change so a draw that
  // started on one state finishes on it.
  private int _renderLayers;
  private string _renderTexture = BlockFirebox.FuelTexture;

  private void Snapshot() {
    _renderLayers = Bed?.LayerCount ?? 0;
    _renderTexture = BlockFirebox.TextureKeyOf(Bed?.FuelCode);
  }

  /// <summary>
  /// Draws the rim and bars always, plus one <c>CokeL</c> course per standing layer, repointed at whichever
  /// fuel was charged. Returns true in every case, including failure: the block's own default mesh is a
  /// full bed of coke, which is never what an empty or part-filled firebox looks like.
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (Api is not ICoreClientAPI capi)
      return true;

    // Read once into locals; the fields can be replaced by the main thread mid-draw.
    int layers = _renderLayers;
    string texture = _renderTexture;

    // Layer count and fuel texture are the only things that change the mesh, so they are the whole key.
    // Cached across every firebox in the world: without it this re-read the shape JSON, deep-cloned it and
    // re-tesselated on every chunk re-tesselation, on the tesselation thread.
    MeshData? mesh = ExMeshCache.GetOrCreate(
      capi,
      Block,
      $"{layers}|{texture}",
      () => {
        Shape? shape = ExMeshCache.LoadShape(
          capi,
          ExMeshCache.ShapePathOf(Block)
        );
        if (shape == null)
          return null;

        Shape drawn = ExShapeElements.Pruned(
          shape,
          BlockFirebox.ElementsFor(layers)
        );
        if (texture != BlockFirebox.FuelTexture)
          drawn = ExShapeElements.Retextured(
            drawn,
            BlockFirebox.FuelTexture,
            texture
          );

        tesselator.TesselateShape(
          "firebox",
          drawn,
          out MeshData built,
          capi.Tesselator.GetTextureSource(Block),
          new Vec3f(0, Block.Shape.rotateY, 0)
        );
        return built;
      }
    );
    if (mesh == null)
      return true;
    mesher.AddMeshData(mesh);
    return true;
  }

  #endregion

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    Snapshot();
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    // Api is null at chunk load (the engine deserialises before Initialize), which is why the snapshot is
    // taken in Initialize as well; this branch covers only the live sync.
    if (Api?.Side != EnumAppSide.Client)
      return;
    Snapshot();
    Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if (Bed is { } bed)
      dsc.AppendLine(bed.InfoLine());
    if (Core is null)
      dsc.AppendLine(Lang.Get("iiex:furnacepart-nofurnace"));
  }

  #endregion
}
