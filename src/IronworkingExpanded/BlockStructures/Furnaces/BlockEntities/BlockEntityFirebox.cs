using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The standalone firebox block's entity: a furnace part that hosts a <see cref="BEBehaviorFirebox"/> fuel
/// bed and draws it.
/// <para>
/// <b>Thin on purpose.</b> Everything about the fuel lives in the behaviour, because the boilers take a
/// firebox <em>internally</em> - their own block entities will host the same behaviour with no block of
/// this type anywhere near them. What is left here is the two things that genuinely belong to a block:
/// drawing the bed, and telling the player what is in it.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityFirebox : BlockEntityFurnacePart
{
  /// <summary>The fuel bed. Null only if the blocktype forgot to declare the behaviour.</summary>
  public BEBehaviorFirebox? Bed => GetBehavior<BEBehaviorFirebox>();

  #region Charging

  /// <summary>
  /// Charges this bed and every other firebox cell of the owning furnace from <paramref name="stack"/>,
  /// returning the total units taken.
  /// <para>
  /// This is what <c>firebox.md</c>'s "fireboxes that belong to one furnace share one pool" means in
  /// practice: <b>one interaction fills the whole bed, and the cost scales with the cell count</b>. A
  /// two-cell reheat firebox is twice the fuel of a one-cell puddling firebox for the same visible fill.
  /// </para>
  /// <para>
  /// <b>Grouped by role, never by adjacency.</b> Two furnaces built back to back would merge their fuel
  /// if this walked neighbours; asking the owning core for its <c>CellRole.Firebox</c> cells cannot. A
  /// firebox with no core fills only itself - the honest answer for a bed standing in a field, which is
  /// allowed to burn and simply wastes the coal.
  /// </para>
  /// <para>
  /// Fills this cell first so a single-unit deposit visibly lands where the player clicked, then the rest
  /// in the layout's own cell order - deterministic, so two identical furnaces charge identically.
  /// </para>
  /// </summary>
  public int Charge(ItemStack? stack)
  {
    if (stack == null || Bed is not { } here)
      return 0;

    int remaining = stack.StackSize;
    int taken = here.TryAdd(stack, remaining);
    remaining -= taken;

    if (Core is not { } core)
      return taken;

    foreach (BlockPos cell in core.FireboxCells)
    {
      if (remaining <= 0)
        break;
      if (
        cell.Equals(Pos)
        || Api.World.BlockAccessor.GetBlockEntity(cell) is not BlockEntityFirebox other
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

  // The tesselation thread reads these and nothing else - not the behaviour, not Core. Snapshotted on
  // every state change for the reason BlockEntityChargePile spells out at length: resolving the anchor
  // writes to the link's cache, and a draw that started on one state must finish on it rather than half
  // on each.
  private int _renderLayers;
  private string _renderTexture = BlockFirebox.FuelTexture;

  private void Snapshot()
  {
    _renderLayers = Bed?.LayerCount ?? 0;
    _renderTexture = BlockFirebox.TextureKeyOf(Bed?.FuelCode);
  }

  /// <summary>
  /// Draws the rim and bars always, and one <c>CokeL</c><i>n</i> course per standing layer, repointed at
  /// whichever fuel was charged.
  /// <para>
  /// Returns <c>true</c> whatever it drew: the block's own default mesh is a <b>full</b> bed of coke, which
  /// is never what an empty or part-filled firebox looks like.
  /// </para>
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  )
  {
    if (Api is not ICoreClientAPI capi)
      return true;

    // Read once into locals; the fields can be replaced by the main thread mid-draw.
    int layers = _renderLayers;
    string texture = _renderTexture;

    Shape? shape = capi
      .Assets.TryGet(
        Block
          .Shape.Base.Clone()
          .WithPathPrefixOnce("shapes/")
          .WithPathAppendixOnce(".json")
      )
      ?.ToObject<Shape>();
    if (shape == null)
      return true;

    Shape drawn = ExShapeElements.Pruned(shape, BlockFirebox.ElementsFor(layers));
    if (texture != BlockFirebox.FuelTexture)
      drawn = ExShapeElements.Retextured(drawn, BlockFirebox.FuelTexture, texture);

    tesselator.TesselateShape(
      "firebox-" + layers + "-" + texture,
      drawn,
      out MeshData mesh,
      capi.Tesselator.GetTextureSource(Block),
      new Vec3f(0, Block.Shape.rotateY, 0)
    );
    mesher.AddMeshData(mesh);
    return true;
  }

  #endregion

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    Snapshot();
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    // Api is null here at chunk load (the engine deserialises before Initialize), which is why the
    // snapshot is taken in Initialize as well - this branch only covers the live sync.
    if (Api?.Side != EnumAppSide.Client)
      return;
    Snapshot();
    Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    if (Bed is { } bed)
      dsc.AppendLine(bed.InfoLine());
    if (Core is null)
      dsc.AppendLine(Lang.Get("iwex:furnacepart-nofurnace"));
  }

  #endregion
}
