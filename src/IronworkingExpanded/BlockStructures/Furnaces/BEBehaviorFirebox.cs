using System;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Furnaces;

/// <summary>
/// A firebox's <b>fuel bed</b> as a composable block-entity behaviour: one cell's worth of a single fuel,
/// drawn as stacked layers and burned down layer by layer.
/// <para>
/// It is the firebox's answer to <c>iwex:chargepile</c>. Modelling the hearth as "whatever coal
/// the player piled into an air cell" - legend <c>@(air|coalpile)</c>, vanilla <c>game:coalpile</c>, free
/// placement - would mean an <em>empty</em> firebox satisfies the structure, the cell's capacity is
/// vanilla's <c>BlockEntityCoalPile.MaxStackSize</c> rather than a number this mod chose, and a box of
/// charcoal and a box of coke look and behave alike. See <c>docs/design/machines/firebox.md</c>.
/// </para>
/// <para>
/// <b>A behaviour rather than a block feature, because the boilers host a firebox internally.</b> Their
/// shapes carry one instead of declaring a separate <c>F</c> cell in a layout, so the pool has to be
/// something a boiler's own block entity can compose. Welding it into <see cref="Blocks.BlockFirebox"/> and
/// retrofitting the boilers afterwards is the expensive order - the same reason
/// <c>BEBehaviorMoltenCell</c> exists.
/// </para>
/// <para>
/// <b>State is per cell; the <em>pool</em> is a distribution rule, not shared storage.</b>
/// <c>firebox.md</c> asks for "fireboxes that belong to one furnace share one pool, filling it costs per
/// cell", and <see cref="Blocks.BlockFirebox"/> gets that by spreading a deposit across every
/// <c>CellRole.Firebox</c> cell of the owning furnace. Keeping the units themselves local is what avoids
/// the two problems shared storage would bring: no cell has to own the save, and an orphaned firebox
/// (its furnace broken out from under it) still holds exactly the fuel it is drawing.
/// </para>
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorFirebox(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity)
{
  #region Fuel list

  /// <summary>
  /// Path fragments of the fuels a firebox burns. Matched as substrings for the reason the cowper stove
  /// already reads its coal that way (<c>BlockEntityCowperStove.cs:121-130</c>): vanilla spells them
  /// across three different code shapes (<c>coke</c>, <c>charcoal</c>, <c>ore-bituminouscoal</c>,
  /// <c>ore-anthracite</c>) and a firebox cares which <em>substance</em> it holds, not which item family
  /// happens to carry it.
  /// <list type="bullet">
  /// <item><b>coke</b> - the metallurgical default.</item>
  /// <item><b>bituminous</b> - historically <em>the</em> reverberatory fuel. Burning raw coal without
  /// contaminating the iron is the entire reason the reverberatory furnace exists.</item>
  /// <item><b>anthracite</b> - the best natural coal for metalwork.</item>
  /// <item><b>charcoal</b> - the pre-coke fuel, and the iwex-tier fallback.</item>
  /// </list>
  /// </summary>
  private static readonly string[] Fuels =
  [
    "coke",
    "bituminous",
    "anthracite",
    "charcoal",
  ];

  /// <summary>
  /// Low-rank, high-moisture, high-ash - it will not carry a metallurgical heat. Checked <b>before</b>
  /// <see cref="Fuels"/> and not merely left out of it: nothing in the accepted list is a substring of
  /// "lignite" today, but a fuel list is exactly the kind of thing that grows, and an exclusion that
  /// depends on the absence of a substring match is an exclusion that can be undone by accident.
  /// </summary>
  private const string Excluded = "lignite";

  /// <summary>Whether <paramref name="stack"/> is a fuel a firebox will take at all, ignoring what is
  /// already in the bed. <see cref="Accepts"/> is the question a deposit actually asks.</summary>
  public static bool IsFuel(ItemStack? stack)
  {
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

  /// <summary>Fuel units one drawn layer holds. Config, but pinned to the shape's six <c>CokeL*</c>
  /// elements through <see cref="Capacity"/>.</summary>
  public static int UnitsPerLayer => Math.Max(1, IwexValues.FireboxUnitsPerLayer);

  /// <summary>Drawn layers in one cell - the shape's <c>CokeL1</c>..<c>CokeL6</c>.</summary>
  public static int LayersPerCell => Math.Max(1, IwexValues.FireboxLayersPerCell);

  /// <summary>Fuel units one firebox cell holds when full: 6 layers x 2 units by default.
  /// <para>
  /// This is the number the ignition threshold is now derived from. <c>ChargeCapacityUnits</c> is
  /// <c>FireboxCellCount x IwexValues.FireboxMixPerCell</c>, and the default 12 is exactly this - so
  /// "lit" means <em>the bed is full</em> on a hearth of any size, rather than being a constant sized for
  /// one firebox and inherited by another.
  /// </para></summary>
  public static int CellCapacity => LayersPerCell * UnitsPerLayer;

  #endregion

  #region State

  /// <summary>Fuel units in this cell, 0..<see cref="CellCapacity"/>.</summary>
  public int Units { get; private set; }

  /// <summary>Code of the fuel this cell holds, or <c>null</c> when empty. <b>One fuel per bed</b>: the
  /// layer texture depicts what the player charged, so a mismatched deposit is refused - the same rule the
  /// tall hopper's tank already enforces.</summary>
  public string? FuelCode { get; private set; }

  /// <summary>How many drawn layers are standing, 0..<see cref="LayersPerCell"/>. A part-filled layer
  /// still draws: a bed one unit into its third course reads as three, so the player sees fuel go in
  /// rather than watching nothing happen until a course completes.</summary>
  public int LayerCount =>
    Math.Min(
      LayersPerCell,
      (Units + UnitsPerLayer - 1) / UnitsPerLayer
    );

  /// <summary>Room left in this cell, in units.</summary>
  public int Free => Math.Max(0, CellCapacity - Units);

  /// <summary>Whether the bed is at capacity - what a furnace means by a firebox being ready to light.</summary>
  public bool IsFull => Units >= CellCapacity;

  #endregion

  /// <summary>
  /// Syncs and redraws, or does nothing at all before <c>Initialize</c> has run.
  /// <para>
  /// The guard is not defensive padding: a bed is legitimately loaded while <see cref="Api"/> is still
  /// null - the engine deserialises before it initialises, and a boiler composing this behaviour may fill
  /// it while standing itself up. There is nothing to sync to at that point, and
  /// <c>BlockEntity.MarkDirty</c> reaches straight through <c>Api.World</c>.
  /// </para>
  /// </summary>
  private void Dirty()
  {
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
  /// many it took. Zero when the bed is full, or when the fuel does not match what is already in it.
  /// <para>
  /// Does <b>not</b> shrink <paramref name="stack"/> - the caller owns the player's slot, and a
  /// behaviour that both mutated the bed and paid for it out of an inventory it was handed would be
  /// impossible to use from the furnace's own code paths.
  /// </para>
  /// </summary>
  public int TryAdd(ItemStack? stack, int maxUnits)
  {
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
  /// Takes one drawn layer back out, as a vanilla coal pile does - a firebox is not a one-way sink. Null
  /// when the bed is empty or the fuel no longer resolves (its mod was removed).
  /// </summary>
  public ItemStack? TryTakeLayer()
  {
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
  /// furnace's per-cycle consumption; the bed empties layer by layer as it goes.</summary>
  public int Consume(int units)
  {
    if (units <= 0 || Units <= 0)
      return 0;
    int burnt = Math.Min(units, Units);
    Units -= burnt;
    if (Units <= 0)
      FuelCode = null;
    Dirty();
    return burnt;
  }

  /// <summary>Everything in the bed as one stack, for a break. A pool holds one fuel, so this is a single
  /// stack rather than one per material - simpler than the charge pile's equivalent for that reason.</summary>
  public ItemStack? Contents() => StackOf(Units);

  /// <summary>Empties the bed without producing anything - used after <see cref="Contents"/> has been
  /// dropped, so the two cannot double up.</summary>
  public void Clear()
  {
    if (Units == 0 && FuelCode == null)
      return;
    Units = 0;
    FuelCode = null;
    Dirty();
  }

  /// <summary>
  /// <paramref name="units"/> of the held fuel as a stack. Items first, then blocks: charcoal, coke and
  /// both coals are items in vanilla, and resolving the block namespace first would find
  /// <c>game:charcoalpile</c> for a bed of charcoal.
  /// </summary>
  private ItemStack? StackOf(int units)
  {
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

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetInt("fireboxUnits", Units);
    // Written even when null, and read back the same way: a bed that has just been emptied has to clear
    // the key or the next fuel is refused as a mismatch against a substance that is no longer there.
    tree.SetString("fireboxFuel", FuelCode ?? "");
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  )
  {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    Units = tree.GetInt("fireboxUnits");
    string fuel = tree.GetString("fireboxFuel") ?? "";
    FuelCode = fuel.Length == 0 ? null : fuel;
  }

  #endregion

  #region HUD

  /// <summary>The bed's own line, appended by whichever block entity hosts the behaviour. Names the fuel
  /// off a real stack rather than off the raw code, so a bed of anthracite reads as anthracite in the
  /// player's language.</summary>
  public string InfoLine() =>
    StackOf(Units) is { } stack
      ? Lang.Get("iwex:firebox-fuel", stack.GetName(), Units, CellCapacity)
      : Lang.Get("iwex:firebox-empty");

  #endregion
}
