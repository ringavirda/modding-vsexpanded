using System;
using System.Collections.Generic;
using ExpandedLib.Blocks;
using ExpandedLib.Registries;
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
/// <see cref="Blocks.BlockFirebox"/> spreads a deposit across every <c>FurnaceCellRoles.Firebox</c> cell of the
/// owning furnace. Keeping the units local means no cell owns the save and an orphaned firebox still holds
/// the fuel it is drawing. See docs/design/machines/firebox.md.
/// </para>
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorFirebox(BlockEntity blockentity)
  : ExBlockEntityBehavior(blockentity) {
  #region Fuel

  /// <summary>
  /// Whether <paramref name="stack"/> burns hot enough to be firebox fuel, read from the item's own
  /// <c>combustibleProps</c> against <see cref="IiexValues.BoilerFuelMinTemp"/>. Read rather than listed
  /// so a fuel another mod ships is admitted by declaring what it already declares - vanilla gives every
  /// coal a burn temperature and a duration, and nothing in this suite read either before. What a given
  /// machine ACCEPTS is narrower and is the machine's own rule; see
  /// <see cref="BlockEntities.BlockEntityFireboxFurnace.AcceptsFireboxFuel"/>.
  /// </summary>
  public static bool IsFuel(ItemStack? stack) =>
    BurnTemperatureOf(stack) >= IiexValues.BoilerFuelMinTemp;

  /// <summary>Burn temperature (°C) of <paramref name="stack"/>, or 0 when it does not burn.</summary>
  public static float BurnTemperatureOf(ItemStack? stack) =>
    stack?.Collectible?.CombustibleProps?.BurnTemperature ?? 0f;

  /// <summary>Burn duration (seconds per unit) of <paramref name="stack"/>, or 0 when it does not burn.</summary>
  public static float BurnDurationOf(ItemStack? stack) =>
    stack?.Collectible?.CombustibleProps?.BurnDuration ?? 0f;

  #endregion

  #region Geometry

  /// <summary>Fuel units one drawn layer holds by default. Config-driven, at least 1.</summary>
  public static int DefaultUnitsPerLayer =>
    Math.Max(1, IiexValues.FireboxUnitsPerLayer);

  /// <summary>Drawn layers in one cell by default, matching the shape's <c>CokeL1</c>..<c>CokeL6</c>
  /// elements.</summary>
  public static int DefaultLayersPerCell =>
    Math.Max(1, IiexValues.FireboxLayersPerCell);

  /// <summary>Units one firebox cell holds when full at the defaults: 6 layers x 2 units. A furnace
  /// whose bed carries no <c>layers</c>/<c>unitsPerLayer</c> properties derives its ignition threshold
  /// from this, so "lit" means the bed is full on a hearth of any size.</summary>
  public static int DefaultCellCapacity =>
    DefaultLayersPerCell * DefaultUnitsPerLayer;

  /// <summary>Element group name a bed with no <c>bedElement</c> property draws its courses under,
  /// matching the shape's own <c>Coke/CokeL1</c>..<c>CokeL6</c> group.</summary>
  public const string DefaultBedElement = "Coke";

  /// <summary>Layer-name prefix a bed with no <c>layerPrefix</c> property draws its courses under.</summary>
  public const string DefaultLayerPrefix = "CokeL";

  private int _layersPerCell = DefaultLayersPerCell;
  private int _unitsPerLayer = DefaultUnitsPerLayer;
  private string _bedElement = DefaultBedElement;
  private string _layerPrefix = DefaultLayerPrefix;

  /// <summary>Drawn layers in this bed, from the behaviour's <c>layers</c> property.</summary>
  public int LayersPerCell => _layersPerCell;

  /// <summary>Fuel units one drawn layer of this bed holds, from <c>unitsPerLayer</c>.</summary>
  public int UnitsPerLayer => _unitsPerLayer;

  /// <summary>Units this bed holds when full.</summary>
  public int CellCapacity => LayersPerCell * UnitsPerLayer;

  /// <summary>Element group this bed's courses draw under, from the behaviour's <c>bedElement</c>
  /// property - the boiler names its own so it need not share the firebox's shape.</summary>
  public string BedElement => _bedElement;

  /// <summary>Layer-name prefix this bed's courses draw under, from <c>layerPrefix</c>.</summary>
  public string LayerPrefix => _layerPrefix;

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    _layersPerCell = Math.Max(
      1,
      properties["layers"].AsInt(DefaultLayersPerCell)
    );
    _unitsPerLayer = Math.Max(
      1,
      properties["unitsPerLayer"].AsInt(DefaultUnitsPerLayer)
    );
    _bedElement = properties["bedElement"].AsString(_bedElement);
    _layerPrefix = properties["layerPrefix"].AsString(_layerPrefix);
  }

  /// <summary>The element paths a bed of <paramref name="layers"/> courses draws, bottom-first, at this
  /// bed's own <see cref="BedElement"/>/<see cref="LayerPrefix"/>.</summary>
  public List<string> ElementsFor(int layers) {
    var keep = new List<string>();
    for (int i = 1; i <= layers; i++)
      keep.Add(_bedElement + "/" + _layerPrefix + i);
    return keep;
  }

  /// <summary>
  /// <paramref name="built"/> with this bed's element group replaced by exactly the courses it is
  /// standing: none while it is empty, <see cref="LayerCount"/> otherwise. For a host whose mesh is
  /// filtered by a construction behaviour rather than pruned per block entity - such a stage can only
  /// name the group whole, which puts a full bed under an empty firebox for the whole burn.
  /// <para>
  /// The group must already be in <paramref name="built"/> before a course is added: raising it is the
  /// stage's business and filling it is this method's. A null list is the tesselator's "no filter" and
  /// passes through. Entries carry the <c>/*</c> subtree marker, since a bare name drops its children.
  /// </para>
  /// </summary>
  public string[]? ComposeOver(string[]? built) {
    if (built == null)
      return null;

    string prefix = _bedElement + "/";
    var kept = new List<string>(built.Length + LayersPerCell);
    bool raised = false;
    foreach (string element in built) {
      if (
        element == _bedElement
        || element.StartsWith(prefix, StringComparison.Ordinal)
      ) {
        raised = true;
        continue;
      }
      kept.Add(element);
    }

    if (raised)
      foreach (string course in ElementsFor(LayerCount))
        kept.Add(course + "/*");
    return [.. kept];
  }

  /// <summary>
  /// <see cref="ElementsFor"/> at the shipped defaults, for a caller with no live bed to ask - what
  /// <see cref="Blocks.BlockFirebox.ElementsFor(BEBehaviorFirebox?, int)"/> falls back to when handed a
  /// null bed.
  /// </summary>
  public static List<string> DefaultElementsFor(int layers) {
    var keep = new List<string>();
    for (int i = 1; i <= layers; i++)
      keep.Add(DefaultBedElement + "/" + DefaultLayerPrefix + i);
    return keep;
  }

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

  // Hand-declared rather than [Persist]: the fuel code is written even when null so that emptying the
  // bed clears the key (a stale code would make the next deposit fail as a fuel mismatch), which is not
  // what ExBlockState.String's skip-on-null does.
  protected override void DeclareState(ExBlockState state) =>
    state
      .Int("fireboxUnits", () => Units, v => Units = v)
      .Tree(
        "fireboxFuel",
        t => t.SetString("fireboxFuel", FuelCode ?? ""),
        (t, _) => {
          string fuel = t.GetString("fireboxFuel") ?? "";
          FuelCode = fuel.Length == 0 ? null : fuel;
        }
      );

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
