using System;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// A firebox's fuel bed as a composable block-entity behaviour: one cell's worth of a single fuel, drawn as
/// stacked layers and burned down layer by layer. A behaviour rather than a block feature because the
/// boilers carry a firebox inside their own shape and compose it from their own block entity.
/// <para>
/// State is per cell; the shared pool a furnace presents is a distribution rule, not shared storage -
/// <see cref="Blocks.BlockFirebox"/> spreads a deposit across every <c>CellRole.Firebox</c> cell of the
/// owning furnace. Keeping the units local means no cell owns the save and an orphaned firebox still holds
/// the fuel it is drawing. See docs/design/machines/firebox.md.
/// </para>
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorFirebox(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity) {
  #region Fuel list

  /// <summary>
  /// Path fragments of the fuels a firebox burns. Matched as substrings, as the cowper stove matches its
  /// coal, because vanilla spells them across several code shapes (<c>coke</c>, <c>charcoal</c>,
  /// <c>ore-bituminouscoal</c>, <c>ore-anthracite</c>) and only the substance matters here.
  /// </summary>
  private static readonly string[] Fuels =
  [
    "coke",
    "bituminous",
    "anthracite",
    "charcoal",
  ];

  /// <summary>
  /// Low-rank, high-moisture, high-ash coal, which will not carry a metallurgical heat. Checked before
  /// <see cref="Fuels"/> rather than merely omitted from it, so that adding a fuel whose fragment happens to
  /// occur in "lignite" cannot re-admit it.
  /// </summary>
  private const string Excluded = "lignite";

  /// <summary>Whether <paramref name="stack"/> is a fuel a firebox burns at all, ignoring what is already in
  /// the bed. A deposit asks <see cref="Accepts"/> instead.</summary>
  public static bool IsFuel(ItemStack? stack) {
    string? path = stack?.Collectible?.Code?.Path;
    if (path == null || path.Contains(Excluded, StringComparison.Ordinal))
      return false;
    foreach (string fuel in Fuels)
      if (path.Contains(fuel, StringComparison.Ordinal))
        return true;
    return false;
  }

  #endregion

  #region Geometry

  /// <summary>Fuel units one drawn layer holds. Config-driven, at least 1.</summary>
  public static int UnitsPerLayer =>
    Math.Max(1, IiexValues.FireboxUnitsPerLayer);

  /// <summary>Drawn layers in one cell, matching the shape's <c>CokeL1</c>..<c>CokeL6</c> elements.</summary>
  public static int LayersPerCell =>
    Math.Max(1, IiexValues.FireboxLayersPerCell);

  /// <summary>Fuel units one firebox cell holds when full: 6 layers x 2 units by default. A furnace derives
  /// its ignition threshold from this, so "lit" means the bed is full on a hearth of any size.</summary>
  public static int CellCapacity => LayersPerCell * UnitsPerLayer;

  #endregion

  #region State

  /// <summary>Fuel units in this cell, 0..<see cref="CellCapacity"/>.</summary>
  public int Units { get; private set; }

  /// <summary>Code of the fuel this cell holds, or <c>null</c> when empty. One fuel per bed: the layer
  /// texture depicts what was charged, so a deposit of a different fuel is refused.</summary>
  public string? FuelCode { get; private set; }

  /// <summary>How many drawn layers are standing, 0..<see cref="LayersPerCell"/>. Rounds up, so a
  /// part-filled layer still draws and fuel going in is visible before the course completes.</summary>
  public int LayerCount =>
    Math.Min(LayersPerCell, (Units + UnitsPerLayer - 1) / UnitsPerLayer);

  /// <summary>Room left in this cell, in units.</summary>
  public int Free => Math.Max(0, CellCapacity - Units);

  /// <summary>Whether the bed is at capacity, which is what a furnace treats as ready to light.</summary>
  public bool IsFull => Units >= CellCapacity;

  #endregion

  /// <summary>
  /// Syncs and redraws, or does nothing before <c>Initialize</c> has run. A bed is legitimately filled while
  /// <c>Api</c> is still null - the engine deserialises before it initialises, and a boiler may fill
  /// its firebox while standing itself up - and <c>BlockEntity.MarkDirty</c> dereferences <c>Api.World</c>.
  /// </summary>
  private void Dirty() {
    if (Api != null)
      Blockentity.MarkDirty(true);
  }

  #region Charging

  /// <summary>
  /// Whether this bed would take <paramref name="stack"/>: a fuel, and either the fuel already in it or
  /// any fuel when it is empty.
  /// </summary>
  public bool Accepts(ItemStack? stack) =>
    IsFuel(stack)
    && (FuelCode == null || FuelCode == stack!.Collectible.Code.ToString());

  /// <summary>
  /// Puts up to <paramref name="maxUnits"/> units of <paramref name="stack"/> into the bed and returns how
  /// many it took. Zero when the bed is full or the fuel does not match what is already in it. Does not
  /// shrink <paramref name="stack"/>: the caller owns the slot and must deduct the returned count itself.
  /// </summary>
  public int TryAdd(ItemStack? stack, int maxUnits) {
    if (maxUnits <= 0 || !Accepts(stack))
      return 0;
    int taken = Math.Min(Math.Min(maxUnits, Free), stack!.StackSize);
    if (taken <= 0)
      return 0;

    FuelCode ??= stack.Collectible.Code.ToString();
    Units += taken;
    Dirty();
    return taken;
  }

  /// <summary>
  /// Takes one drawn layer back out, as a vanilla coal pile does. Null when the bed is empty or the held
  /// fuel code no longer resolves to an item or block.
  /// </summary>
  public ItemStack? TryTakeLayer() {
    if (Units <= 0)
      return null;
    int taken = Math.Min(Units, UnitsPerLayer);
    if (StackOf(taken) is not { } stack)
      return null;

    Units -= taken;
    if (Units <= 0)
      FuelCode = null;
    Dirty();
    return stack;
  }

  /// <summary>Burns <paramref name="units"/> off the bed and returns how many were actually there. The
  /// furnace's per-cycle consumption.</summary>
  public int Consume(int units) {
    if (units <= 0 || Units <= 0)
      return 0;
    int burnt = Math.Min(units, Units);
    Units -= burnt;
    if (Units <= 0)
      FuelCode = null;
    Dirty();
    return burnt;
  }

  /// <summary>Everything in the bed as one stack, for a break. A bed holds one fuel, so a single stack
  /// covers it.</summary>
  public ItemStack? Contents() => StackOf(Units);

  /// <summary>Empties the bed without producing anything. Called after <see cref="Contents"/> has been
  /// dropped so the two cannot double up.</summary>
  public void Clear() {
    if (Units == 0 && FuelCode == null)
      return;
    Units = 0;
    FuelCode = null;
    Dirty();
  }

  /// <summary>
  /// <paramref name="units"/> of the held fuel as a stack. Resolves items before blocks: charcoal, coke and
  /// both coals are items in vanilla, and the block namespace would answer a charcoal bed with
  /// <c>game:charcoalpile</c>.
  /// </summary>
  private ItemStack? StackOf(int units) {
    if (units <= 0 || FuelCode == null || Api == null)
      return null;
    var code = new AssetLocation(FuelCode);
    if (Api.World.GetItem(code) is { } item)
      return new ItemStack(item, units);
    if (Api.World.GetBlock(code) is { } block)
      return new ItemStack(block, units);
    return null;
  }

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("fireboxUnits", Units);
    // Written even when null so that emptying the bed clears the key; a stale code would make the next
    // deposit fail as a fuel mismatch.
    tree.SetString("fireboxFuel", FuelCode ?? "");
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  ) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    Units = tree.GetInt("fireboxUnits");
    string fuel = tree.GetString("fireboxFuel") ?? "";
    FuelCode = fuel.Length == 0 ? null : fuel;
  }

  #endregion

  #region HUD

  /// <summary>The bed's HUD line, appended by whichever block entity hosts the behaviour. Names the fuel off
  /// a resolved stack rather than the raw code, so it reads in the player's language.</summary>
  public string InfoLine() =>
    StackOf(Units) is { } stack
      ? Lang.Get("iiex:firebox-fuel", stack.GetName(), Units, CellCapacity)
      : Lang.Get("iiex:firebox-empty");

  #endregion
}
