using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// Builds the two pots a crucible hearth hands back: the empty fired one a player put in, and the pourable
/// one a finished heat makes. Its own type rather than the hearth's own methods, because a block entity
/// that writes an <see cref="ItemStack"/> into a tree owes a collectible-mapping pair - and nothing here is
/// ever stored, only handed over.
/// </summary>
public static class CruciblePot {
  /// <summary>
  /// The two stack attributes vanilla's smelted container carries: what metal, and how much of it.
  /// </summary>
  /// <remarks>
  /// Named here because they are a contract with code this mod does not own - <c>BlockSmeltedContainer</c>
  /// reads both on every pour - and because they are written by hand rather than through vanilla's
  /// <c>SetContents</c>, which is not public in every version the mod builds against.
  /// </remarks>
  public const string ContentsKey = "output";

  public const string UnitsKey = "units";

  /// <summary>What a finished pot holds: the metal def's own <c>moltenItem</c>, which is what every other
  /// consumer of crucible steel reads.</summary>
  public const string MoltenItemCode = "iiex:ingot-cruciblesteel";

  /// <summary>The empty fired pot, at <paramref name="firings"/> heats already given.</summary>
  public static ItemStack? Burned(IWorldAccessor world, int firings) {
    Block? pot = world.GetBlock(
      new AssetLocation(
        IiexBlocks.Steelcrucible.WithType(IiexBlocks.Steelcrucible.Type.Burned)
      )
    );
    if (pot == null)
      return null;

    var stack = new ItemStack(pot);
    CrucibleFiring.Set(stack, firings);
    return stack;
  }

  /// <summary>
  /// The pourable pot: vanilla's own smelted-container contract, so the molten path accepts it without
  /// knowing anything about the furnace that filled it.
  /// </summary>
  /// <remarks>
  /// The firing count goes up by one here, because the heat this pot is carrying is the one it just gave.
  /// A pot handed back at its old age would never wear out.
  /// </remarks>
  public static ItemStack? Molten(IWorldAccessor world, int firings, int units) {
    if (
      world.GetBlock(
        new AssetLocation(
          IiexBlocks.Steelcrucible.WithType(
            IiexBlocks.Steelcrucible.Type.Smelted
          )
        )
      )
      is not BlockSmeltedContainer pourable
    )
      return null;

    Item? ingot = world.GetItem(new AssetLocation(MoltenItemCode));
    if (ingot == null)
      return null;

    var stack = new ItemStack(pourable);
    stack.Attributes.SetItemstack(ContentsKey, new ItemStack(ingot));
    stack.Attributes.SetInt(UnitsKey, units);
    stack.Collectible.SetTemperature(
      world,
      stack,
      IiexValues.CrucibleMeltingPointC
    );
    CrucibleFiring.Set(stack, firings + 1);
    return stack;
  }
}
