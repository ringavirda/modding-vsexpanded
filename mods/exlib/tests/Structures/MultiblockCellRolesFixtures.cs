using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Tests;

/// <summary>
/// Shared layouts and helpers for the cell-role suite: what a layout cell is for, independent of the
/// block that may occupy it. A role attaches to a glyph rather than to a code, because several glyphs
/// may carry one code - <c>game:air</c> is the vent shaft, the flue and the tap alcove across shipped
/// layouts. The fixture turns on that: <c>a</c> and <c>v</c> are both <c>game:air</c> and only
/// <c>v</c> is the flue. Its footprint is chiral (the flue is an L congruent to none of its own
/// rotations), so a rotation that turns the wrong way shows in the drawing.
/// </summary>
internal static class MultiblockCellRolesFixtures {
  public static readonly CellRole Chargeable = CellRole.Of("Chargeable");
  public static readonly CellRole Firebox = CellRole.Of("Firebox");
  public static readonly CellRole Tuyere = CellRole.Of("Tuyere");
  public static readonly CellRole MetalTap = CellRole.Of(
    "MetalTap",
    single: true
  );
  public static readonly CellRole SlagTap = CellRole.Of(
    "SlagTap",
    single: true
  );
  public static readonly CellRole Flue = CellRole.Of("Flue");
  public static readonly CellRole Damper = CellRole.Of("Damper");

  public static readonly BlockPos Anchor = new(0, 10, 0);

  /// <summary>
  /// The chiral flue - an L in <c>(x, z)</c> plus one cell a level up, so a rotation that leaked into
  /// <c>y</c> shows too. Drawn with the glyph <c>v</c>.
  /// </summary>
  public static readonly Vec3i[] FlueCells =
  [
    new(2, 0, 0),
    new(3, 0, 0),
    new(2, 0, 1),
    new(2, 1, 0),
  ];

  /// <summary>The two tuyeres, drawn with two glyphs sharing one code and one role - the layouts' own
  /// <c>T</c>/<c>Y</c> idiom, so a role is not one glyph per role.</summary>
  public static readonly Vec3i[] TuyereCells = [new(0, 0, 1), new(1, 1, 1)];

  /// <summary>
  /// The layout under test.
  /// <list type="bullet">
  /// <item><c>a</c> and <c>v</c> are both <c>game:air</c>; only <c>v</c> is the flue.</item>
  /// <item><c>t</c> and <c>y</c> are both <c>exlib:testtuyere*</c>, and both are tuyeres.</item>
  /// </list>
  /// </summary>
  public static ExBlockDef RoledDef() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "exlib:testmega*")
          .Legend('#', "exlib:testbrick*")
          .Legend('a', "game:air")
          .Legend('v', "game:air")
          .Legend('t', "exlib:testtuyere*")
          .Legend('y', "exlib:testtuyere*")
          .Role('v', Flue)
          .Role('t', Tuyere)
          .Role('y', Tuyere)
          .Layer(
            0,
            """
            C a v v
            t . v #
            """
          )
          .Layer(
            1,
            """
            . . v .
            . y . .
            """
          )
      );

  /// <summary>The same drawing with every <c>Role</c> call removed - the control for the additive
  /// guarantee.</summary>
  public static ExBlockDef UnroledDef() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "exlib:testmega*")
          .Legend('#', "exlib:testbrick*")
          .Legend('a', "game:air")
          .Legend('v', "game:air")
          .Legend('t', "exlib:testtuyere*")
          .Legend('y', "exlib:testtuyere*")
          .Layer(
            0,
            """
            C a v v
            t . v #
            """
          )
          .Layer(
            1,
            """
            . . v .
            . y . .
            """
          )
      );

  /// <summary>
  /// A drawing whose <c>v</c> glyph carries two roles over the same cell, with an <c>a</c> cell of the
  /// identical code beside it carrying neither. The shipped case is the shaft furnaces' crucible floor,
  /// which is burden column and metal pool at once.
  /// </summary>
  public static ExBlockDef OverlappingDef() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "exlib:testmega*")
          .Legend('a', "game:air")
          .Legend('v', "game:air")
          .Role('v', Flue)
          .Role('v', Damper)
          .Layer(0, "C a v")
      );

  public static JObject Attributes(ExBlockDef def) =>
    (JObject)def.ToJson()["attributes"]!;

  public static (TestWorld world, TestMegablock machine) Stand(int angle = 0) {
    var world = new TestWorld();
    var machine = new TestMegablock { Angle = angle };
    world.Place(
      Anchor,
      TestBlocks.Configure(new Block(), "exlib:testmega-n", 1),
      machine
    );
    world.Attach(machine);
    return (world, machine);
  }

  /// <summary>Where <paramref name="authored"/> lands in the world at <paramref name="angle"/>, computed
  /// through the shared rotation helper. An independent route to the machine's own answer, which comes off
  /// vanilla's <c>InitForUse</c> offset table.</summary>
  public static string ExpectedAt(IEnumerable<Vec3i> authored, int angle) =>
    Render(
      authored.Select(c => {
        Vec3i r = ExOrientation.RotateOffset(c, angle);
        return Anchor.AddCopy(r.X, r.Y, r.Z);
      })
    );

  public static string Render(IEnumerable<BlockPos> cells) =>
    string.Join(
      ", ",
      cells
        .Select(p => $"({p.X},{p.Y},{p.Z})")
        .OrderBy(s => s, StringComparer.Ordinal)
    );
}
