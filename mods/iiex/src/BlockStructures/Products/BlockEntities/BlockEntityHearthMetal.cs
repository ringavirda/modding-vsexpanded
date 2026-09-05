using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Products.BlockEntities;

/// <summary>
/// Block entity for <c>iiex:hearthmetal-*</c>: the crucible floor itself, hosting the two molten cells a
/// shaft furnace pools into - iron and slag, side by side in one block. Metal-neutral: the metal comes
/// from the block's code (the <c>metal</c> variant), so one entity backs both the blast furnace's pig
/// iron and the cupola's cast iron.
/// <para>
/// The cells are live while the furnace runs and latch solid on their own thermal update, so a dead
/// furnace leaves its hearth frozen in place rather than being stamped at shutdown. A hearth block
/// outlives the furnace that made it - the shaft can be broken off around it - which is why the thermal
/// tick is its own rather than the furnace's.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityHearthMetal : BlockEntity, IChiselableMolten {
  /// <summary>Declared key of the iron cell. Two cells on one host cannot be told apart by
  /// <c>GetBehavior&lt;T&gt;()</c>, so both are addressed by key.</summary>
  public const string IronCellKey = "hm_iron_";

  /// <summary>Declared key of the slag cell.</summary>
  public const string SlagCellKey = "hm_slag_";

  /// <summary>How often the cells' thermal state is refreshed, in milliseconds.</summary>
  private const int ThermalIntervalMs = 1000;

  /// <summary>Number of metal bits stored, used to scale the break drop. Persisted for blocks placed by
  /// the pre-cell furnace, whose metal was stamped here rather than held in a cell.</summary>
  public int MetalCount { get; set; } = 2;

  /// <summary>The iron cell, or null on a block whose definition declares none.</summary>
  public BEBehaviorMoltenCell? IronCell => this.MoltenCell(IronCellKey);

  /// <summary>The slag cell, or null on a block whose definition declares none.</summary>
  public BEBehaviorMoltenCell? SlagCell => this.MoltenCell(SlagCellKey);

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // Hosted cells are not auto-ticked - a hosted cell is deliberately outside the shared molten network
    // graph - so the block that hosts them drives their thermal update. The client tick exists so the
    // glow is not stale on a block nobody is standing next to.
    RegisterGameTickListener(OnThermalTick, ThermalIntervalMs);
  }

  private void OnThermalTick(float dt) {
    foreach (BEBehaviorMoltenCell cell in this.MoltenCells()) {
      if (Api.Side == EnumAppSide.Server)
        cell.EnsureMetalStack(Api.World);
      cell.UpdateThermal(Api.World);
    }
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(Lang.Get("iiex:solidifiedmetal-info-count", MetalCount));
  }

  #region Solidified clearing (IChiselableMolten)

  // Same shape as the canal's: a solidified cell is the chiselable target and can be chipped out once it
  // has cooled past the hardened threshold, so "too hot" is the only blocked state.
  bool IChiselableMolten.HasChiselableContent =>
    IronCell?.Solidified == true || SlagCell?.Solidified == true;

  bool IChiselableMolten.CanChiselOut =>
    ((IChiselableMolten)this).HasChiselableContent
    && Solidified(IronCell).Hardened
    && Solidified(SlagCell).Hardened;

  string? IChiselableMolten.ChiselBlockedError => "iiex-hearthtoohot";

  /// <summary>
  /// Chips the hardened hearth out, emptying both cells. The iron is what the player recovers; the slag
  /// is spoil and is cleared with it, which is what chipping out a dead hearth actually yields. Returns
  /// the slag recovery only when there was no iron, so a slag-only cell still gives something back.
  /// </summary>
  ItemStack? IChiselableMolten.ChiselOut() {
    if (Api?.Side != EnumAppSide.Server)
      return null;

    ItemStack? iron = Recover(IronCell, Api.World);
    ItemStack? slag = Recover(SlagCell, Api.World);
    MarkDirty(true);
    return iron ?? slag;
  }

  /// <summary>Empties one solidified cell and returns its recovery, or null when it holds nothing
  /// frozen.</summary>
  private static ItemStack? Recover(
    BEBehaviorMoltenCell? cell,
    IWorldAccessor world
  ) {
    if (cell == null || !cell.Solidified || cell.CellAmount <= 0)
      return null;

    ItemStack? drop = cell.GetRecoveryDrop(world);
    cell.ClearContents();
    return drop;
  }

  /// <summary>Whether a cell is frozen and, if so, cooled far enough to chip. An absent or empty cell is
  /// hardened by default, so one frozen cell is not held back by an empty sibling.</summary>
  private static (bool Frozen, bool Hardened) Solidified(
    BEBehaviorMoltenCell? cell
  ) =>
    cell == null || !cell.Solidified || cell.CellAmount <= 0
      ? (false, true)
      : (true, cell.IsHardened);

  #endregion

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("ironCount", MetalCount);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    // Tree key stays "ironCount": the key is the save contract and must survive property and block
    // renames.
    MetalCount = tree.GetInt("ironCount", 2);
  }
}
