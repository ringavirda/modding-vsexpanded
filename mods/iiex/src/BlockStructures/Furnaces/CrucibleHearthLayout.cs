using System;
using System.Collections.Generic;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// What a crucible hearth holds, and which shape elements draw it. Four melting holes in a 2x2, each a
/// clay stand carrying one pot; the coke packs round them and is the same fuel bed every other firebox
/// machine uses. Pure and world-free, like <see cref="HearthRows"/> and <see cref="StackDraught"/>.
/// </summary>
/// <remarks>
/// The holes are fixed at four and live in one cell - the drawn hearth is a single 16-voxel block, not a
/// row of holes - so throughput is bought by building another furnace. See
/// docs/design/machines/crucible-furnace.md.
/// </remarks>
public static class CrucibleHearthLayout {
  /// <summary>
  /// The four holes, named for where they sit in the block and numbered as the shape numbers them:
  /// clockwise from the north-west corner, so <see cref="Hole.NorthWest"/> is <c>Crucible1</c>.
  /// </summary>
  public enum Hole {
    NorthWest,
    NorthEast,
    SouthEast,
    SouthWest,
  }

  /// <summary>Holes in one hearth. Fixed, and the reason the machine is built in banks.</summary>
  public const int Holes = 4;

  /// <summary>The holes in element order, so a consumer can iterate without casting.</summary>
  public static readonly IReadOnlyList<Hole> All =
  [
    Hole.NorthWest,
    Hole.NorthEast,
    Hole.SouthEast,
    Hole.SouthWest,
  ];

  /// <summary>
  /// What stands in one hole. Drawn as four independent groups rather than one state, because a charged
  /// pot under its slag cover and its lid shows all of them at once.
  /// </summary>
  /// <param name="Pot">A pot is seated on the stand.</param>
  /// <param name="Charge">The pot holds its blister-steel charge.</param>
  /// <param name="Slag">The thin slag cover floats on the charge.</param>
  /// <param name="Cover">The hole's lid is on.</param>
  public readonly record struct HoleContents(
    bool Pot,
    bool Charge,
    bool Slag,
    bool Cover
  );

  /// <summary>The structural groups, always drawn: the masonry rim, the firebars and the four stands.</summary>
  public const string BaseElement = BlockFirebox.BaseElement;

  /// <summary>The element drawing the pot seated in <paramref name="hole"/>.</summary>
  public static string Pot(Hole hole) => "Crucibles/Crucible" + Index(hole);

  /// <summary>The element drawing the blister-steel charge in <paramref name="hole"/>'s pot.</summary>
  public static string Charge(Hole hole) =>
    "FillingBlisterSteel/BlisterSteel" + Index(hole);

  /// <summary>The element drawing the slag cover floating on <paramref name="hole"/>'s charge.</summary>
  public static string Slag(Hole hole) => "FillingSlag/Slag" + Index(hole);

  /// <summary>The element drawing the lid over <paramref name="hole"/>.</summary>
  public static string Cover(Hole hole) => "Covers/Cover" + Index(hole);

  /// <summary>
  /// Every element to draw for the given hole contents over <paramref name="bed"/> standing
  /// <paramref name="bedLayers"/> courses: the structure and the bed always, then whatever each hole is
  /// carrying.
  /// </summary>
  /// <remarks>
  /// The bed comes from <see cref="BlockFirebox.ElementsFor(BEBehaviorFirebox?, int)"/> unchanged, because
  /// the drawn hearth names its coke courses exactly as the firebox does - one selector, not a second copy
  /// of the same six names. <paramref name="bed"/> is threaded through rather than defaulted, so a hearth
  /// whose behaviour declares its own <c>bedElement</c>/<c>layerPrefix</c> draws its own names instead of
  /// silently falling back to the shipped ones.
  /// </remarks>
  public static string[] ElementsFor(
    IReadOnlyList<HoleContents> holes,
    BEBehaviorFirebox? bed,
    int bedLayers
  ) {
    List<string> els = BlockFirebox.ElementsFor(bed, bedLayers);
    foreach (Hole hole in All) {
      HoleContents held = holes[(int)hole];
      if (held.Pot)
        els.Add(Pot(hole));
      if (held.Charge)
        els.Add(Charge(hole));
      if (held.Slag)
        els.Add(Slag(hole));
      if (held.Cover)
        els.Add(Cover(hole));
    }
    return [.. els];
  }

  // The shape numbers its groups from one, clockwise from the north-west; the enum is declared in that
  // order so the two never have to be kept in step by hand.
  private static int Index(Hole hole) =>
    (int)hole is >= 0 and < Holes
      ? (int)hole + 1
      : throw new ArgumentOutOfRangeException(nameof(hole));
}
