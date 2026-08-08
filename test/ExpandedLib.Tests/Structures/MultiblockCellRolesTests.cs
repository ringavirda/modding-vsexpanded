using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Cell <b>roles</b> - what a layout cell is <em>for</em>, as opposed to what block may occupy it. Seven
/// semantic cell sets are hand-maintained as C# offset lists today and have to agree with a drawing by eye;
/// one of them has already drifted onto a coordinate outside its own structure. A role puts the fact in the
/// drawing, so there is nothing left to drift from.
/// <para>
/// The design constraint everything here turns on: <b>a role attaches to a glyph, and several glyphs may
/// carry one code</b>. <c>game:air</c> is simultaneously the vent shaft, the flue and the tap alcove in
/// shipped layouts, so "the air cells are the flue" is not a statement the DSL can make. The fixture below is
/// built on exactly that - <c>a</c> and <c>v</c> are both <c>game:air</c> and only one of them is the flue -
/// because a design that could not express it would look correct on every layout in the repo and fail on the
/// first one that needed it.
/// </para>
/// <para>
/// The footprint is deliberately <b>chiral</b>: the flue is an L hanging off one side, congruent to none of
/// its own rotations. The asymmetry has to be in the <em>drawing</em>: iwex's three asymmetric furnace
/// fixtures used to vary <c>ShaftMin</c>/<c>ShaftMax</c> on a subclass, which had no influence on a layout at
/// all and so could see no layout-derived rotation bug. Since 2.6.5 they are asymmetric <em>drawings</em>
/// (<c>FurnaceLayoutRig.ShaftBoxDef</c>), for exactly this reason.
/// </para>
/// </summary>
public class MultiblockCellRolesTests
{
  #region Fixture

  private static readonly BlockPos Anchor = new(0, 10, 0);

  /// <summary>
  /// The chiral flue - an L in <c>(x, z)</c> plus one cell a level up, so a rotation that leaked into
  /// <c>y</c> shows too. Drawn with the glyph <c>v</c>.
  /// </summary>
  private static readonly Vec3i[] FlueCells =
  [
    new(2, 0, 0),
    new(3, 0, 0),
    new(2, 0, 1),
    new(2, 1, 0),
  ];

  /// <summary>The two tuyeres, drawn with <b>two different glyphs sharing one code and one role</b> - the
  /// layouts' own <c>T</c>/<c>Y</c> idiom, here proving a role is not one-glyph-per-role.</summary>
  private static readonly Vec3i[] TuyereCells = [new(0, 0, 1), new(1, 1, 1)];

  /// <summary>
  /// The layout under test.
  /// <list type="bullet">
  /// <item><c>a</c> and <c>v</c> are both <c>game:air</c>; only <c>v</c> is the flue.</item>
  /// <item><c>t</c> and <c>y</c> are both <c>exlib:testtuyere*</c>, and both are tuyeres.</item>
  /// </list>
  /// </summary>
  private static ExBlockDef RoledDef() =>
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
          .Role('v', CellRole.Flue)
          .Role('t', CellRole.Tuyere)
          .Role('y', CellRole.Tuyere)
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
  private static ExBlockDef UnroledDef() =>
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
  /// A drawing whose <c>v</c> glyph carries <b>two</b> roles over the same cell, with an <c>a</c> cell of
  /// the identical code beside it carrying neither. The shipped shape of this is the shaft furnaces'
  /// crucible floor, which is burden column and metal pool at once.
  /// </summary>
  private static ExBlockDef OverlappingDef() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "exlib:testmega*")
          .Legend('a', "game:air")
          .Legend('v', "game:air")
          .Role('v', CellRole.Flue)
          .Role('v', CellRole.Damper)
          .Layer(0, "C a v")
      );

  private static JObject Attributes(ExBlockDef def) =>
    (JObject)def.ToJson()["attributes"]!;

  private static (TestWorld world, TestMegablock machine) Stand(int angle = 0)
  {
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
  /// through the shared rotation helper - an independent route to the machine's own answer, which comes off
  /// vanilla's <c>InitForUse</c> offset table.</summary>
  private static string ExpectedAt(IEnumerable<Vec3i> authored, int angle) =>
    Render(
      authored.Select(c =>
      {
        Vec3i r = ExOrientation.RotateOffset(c, angle);
        return Anchor.AddCopy(r.X, r.Y, r.Z);
      })
    );

  private static string Render(IEnumerable<BlockPos> cells) =>
    string.Join(
      ", ",
      cells
        .Select(p => $"({p.X},{p.Y},{p.Z})")
        .OrderBy(s => s, StringComparer.Ordinal)
    );

  #endregion

  #region Serialization

  [Fact]
  public void Roles_serialise_as_the_authored_offsets_of_each_role()
  {
    // The whole shape, pinned literally: authored (north-frame) offsets, roles in enum order, cells in
    // drawing order. Anything looser would let the emitted table churn without a golden noticing.
    Assert.Equal(
      JObject
        .Parse(
          """
          {
            "Tuyere": [ {"x":0,"y":0,"z":1}, {"x":1,"y":1,"z":1} ],
            "Flue":   [ {"x":2,"y":0,"z":0}, {"x":3,"y":0,"z":0},
                        {"x":2,"y":0,"z":1}, {"x":2,"y":1,"z":0} ]
          }
          """
        )
        .ToString(Newtonsoft.Json.Formatting.None),
      Attributes(RoledDef())["multiblockRoles"]!.ToString(
        Newtonsoft.Json.Formatting.None
      )
    );
  }

  [Fact]
  public void Roles_ride_in_a_sibling_attribute_and_never_inside_multiblockStructure()
  {
    // multiblockStructure is deserialised by vanilla's own MultiblockStructure and must stay exactly its
    // schema - the same reason multiblockFacings is a sibling.
    JObject attrs = Attributes(RoledDef());
    Assert.NotNull(attrs["multiblockRoles"]);
    Assert.Null(attrs["multiblockStructure"]!["roles"]);
    Assert.Null(attrs["multiblockStructure"]!["multiblockRoles"]);
  }

  [Fact]
  public void A_serialised_role_table_reads_back_to_the_offsets_it_was_written_from()
  {
    MultiblockCellRoles roles = MultiblockCellRoles.FromAttributes(
      new Vintagestory.API.Datastructures.JsonObject(Attributes(RoledDef()))
    );

    Assert.False(roles.IsEmpty);
    Assert.Equal(
      FlueCells.Select(c => (c.X, c.Y, c.Z)).OrderBy(t => t).ToList(),
      roles.CellsOf(CellRole.Flue).OrderBy(t => t).ToList()
    );
    Assert.Equal(
      TuyereCells.Select(c => (c.X, c.Y, c.Z)).OrderBy(t => t).ToList(),
      roles.CellsOf(CellRole.Tuyere).OrderBy(t => t).ToList()
    );
    // A role the layout never mentions is empty rather than absent, so no caller has to null-check.
    Assert.Empty(roles.CellsOf(CellRole.MetalTap));
  }

  #endregion

  #region A hand-edited attribute

  /// <summary>
  /// Reads a raw <c>multiblockRoles</c> body the way a hand-edited JSON patch would present it - the only
  /// route by which anything malformed can reach the reader at all, since the builder refuses every way this
  /// can be got wrong at the author.
  /// </summary>
  private static MultiblockCellRoles ReadRoles(string rolesBody) =>
    MultiblockCellRoles.FromAttributes(
      new Vintagestory.API.Datastructures.JsonObject(
        JObject.Parse("{\"multiblockRoles\":" + rolesBody + "}")
      )
    );

  [Fact]
  public void A_cell_whose_coordinate_is_not_an_int_is_skipped_rather_than_thrown_on()
  {
    // Where this read happens is the whole argument: SetStructureAngle, i.e. the server monitor tick and a
    // client GetBlockInfo. A throw there is a repeating exception on a live block entity mid-session, which
    // is strictly worse than the refusal-to-load the skip contract was written to avoid. Newtonsoft's (int)
    // cast throws FormatException on a string and ArgumentException on an object, a bool and a null.
    MultiblockCellRoles roles = ReadRoles(
      """
      {
        "Flue": [ {"x":"two","y":0,"z":0},
                  {"x":{"nested":1},"y":0,"z":0},
                  {"x":true,"y":0,"z":0},
                  {"x":null,"y":0,"z":0},
                  {"x":1.5,"y":0,"z":0},
                  {"x":9999999999999,"y":0,"z":0},
                  {"y":0,"z":0},
                  {"x":2,"y":0,"z":1} ]
      }
      """
    );

    // Seven unreadable cells dropped, the one well-formed cell kept - degraded, not fatal.
    Assert.Equal((2, 0, 1), Assert.Single(roles.CellsOf(CellRole.Flue)));
  }

  [Fact]
  public void A_role_whose_every_cell_is_malformed_reads_as_no_roles_at_all()
  {
    // The degenerate end of the same skip: nothing readable means None, not a role present-but-empty.
    Assert.True(
      ReadRoles("""{ "Flue": [ {"x":"two","y":0,"z":0} ] }""").IsEmpty
    );
  }

  [Fact]
  public void A_key_that_is_not_a_declared_role_is_skipped()
  {
    // Enum.TryParse alone accepts both of the first two: "99" becomes (CellRole)99 and "Flue, Damper"
    // becomes (CellRole)15 (Flue|Damper). No caller can ask for either, so they would sit in the map
    // answering nothing while making IsEmpty claim the layout has roles.
    Assert.True(ReadRoles("""{ "99": [ {"x":0,"y":0,"z":0} ] }""").IsEmpty);
    Assert.True(
      ReadRoles("""{ "Flue, Damper": [ {"x":0,"y":0,"z":0} ] }""").IsEmpty
    );
    Assert.True(
      ReadRoles("""{ "Nonsense": [ {"x":0,"y":0,"z":0} ] }""").IsEmpty
    );

    // ...and a real name still reads case-insensitively, so the guard tightened nothing it should not have.
    Assert.NotEmpty(
      ReadRoles("""{ "flue": [ {"x":0,"y":0,"z":0} ] }""")
        .CellsOf(CellRole.Flue)
    );
  }

  #endregion

  #region Many glyphs, one code

  [Fact]
  public void Two_glyphs_on_one_code_share_a_block_number_and_emit_one_entry()
  {
    // This is the fix that made roles expressible at all. `blockNumbers` is a JSON object keyed by code,
    // so numbering per glyph emitted two numbers and one entry - the first was overwritten, and every cell
    // holding it silently stopped being required (vanilla's own InCompleteBlockCount indexes BlockCodes[w]
    // with no TryGetValue and throws outright). Six glyphs, four codes, four numbers.
    var numbers = (JObject)
      Attributes(RoledDef())["multiblockStructure"]!["blockNumbers"]!;

    Assert.Equal(
      """{"exlib:testmega*":1,"exlib:testbrick*":2,"game:air":3,"exlib:testtuyere*":4}""",
      numbers.ToString(Newtonsoft.Json.Formatting.None)
    );

    // ...and every offset resolves to one of those four, so no cell is orphaned.
    var offsets = (JArray)
      Attributes(RoledDef())["multiblockStructure"]!["offsets"]!;
    Assert.NotEmpty(offsets);
    Assert.All(
      offsets,
      o => Assert.InRange((int)o["w"]!, 1, numbers.Properties().Count())
    );
  }

  [Fact]
  public void One_code_two_glyphs_one_role_answers_only_the_role_glyphs_cells()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, RoledDef()).Complete();

    // The single most important assertion in this file. Both `a` and `v` are `game:air`, so asking the
    // layout "which cells take an air block" answers five - and asking "which cells are the flue" answers
    // the four that were drawn as flue. A role keyed by code, or derived from the block occupying the cell,
    // could not tell those apart; that is precisely why roles attach to glyphs.
    Assert.Equal(
      5,
      machine.CellsAccepting(new AssetLocation("game:air")).Count
    );
    Assert.Equal(
      ExpectedAt(FlueCells, 0),
      Render(machine.CellsWithRole(CellRole.Flue))
    );
    Assert.DoesNotContain(
      Anchor.AddCopy(1, 0, 0), // the `a` cell - air, not flue
      machine.CellsWithRole(CellRole.Flue)
    );
  }

  [Fact]
  public void Two_glyphs_sharing_a_role_both_contribute_their_cells()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, RoledDef()).Complete();

    // The layouts' own T/Y idiom: two glyphs for the two tuyere facings, one role. A role is a set, not a
    // glyph alias, so both cells come back.
    Assert.Equal(
      ExpectedAt(TuyereCells, 0),
      Render(machine.CellsWithRole(CellRole.Tuyere))
    );
  }

  [Fact]
  public void A_cell_that_is_two_things_at_once_answers_to_both_roles_at_runtime()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, OverlappingDef()).Complete();

    // The shipped shape of this is the shaft furnaces' crucible: Chargeable and Pool over the same cells.
    // Both roles answer, both answer the same cell, and the two are cached independently rather than one
    // shadowing the other.
    Assert.Equal(
      Render([Anchor.AddCopy(2, 0, 0)]),
      Render(machine.CellsWithRole(CellRole.Flue))
    );
    Assert.Equal(
      Render(machine.CellsWithRole(CellRole.Flue)),
      Render(machine.CellsWithRole(CellRole.Damper))
    );
    Assert.NotSame(
      machine.CellsWithRole(CellRole.Flue),
      machine.CellsWithRole(CellRole.Damper)
    );

    // ...and the overlap is a property of the glyph, not of its code: the neighbouring `a` cell is the
    // same game:air and carries neither role.
    Assert.Equal(
      2,
      machine.CellsAccepting(new AssetLocation("game:air")).Count
    );
  }

  #endregion

  #region Rotation

  [Theory]
  [InlineData(0)]
  [InlineData(90)]
  [InlineData(180)]
  [InlineData(270)]
  public void Every_facing_puts_the_role_cells_where_that_rotation_says(
    int angle
  )
  {
    var (world, machine) = Stand(angle);
    StructureRig.Around(world, machine, RoledDef(), angle).Complete();

    // On a square or single-cell role set this passes for a mapping that turns the wrong way, or not at
    // all. On the chiral L it does not: turning -90 instead of +90 lands the whole set in the opposite
    // quadrant. All four are enumerated because a mutation that is the identity at 180 (or at 0) survives
    // any subset of them.
    Assert.Equal(
      ExpectedAt(FlueCells, angle),
      Render(machine.CellsWithRole(CellRole.Flue))
    );
    Assert.Equal(
      ExpectedAt(TuyereCells, angle),
      Render(machine.CellsWithRole(CellRole.Tuyere))
    );
  }

  [Fact]
  public void The_four_facings_are_four_different_role_footprints()
  {
    var sets = new List<string>();
    foreach (int angle in new[] { 0, 90, 180, 270 })
    {
      var (world, machine) = Stand(angle);
      StructureRig.Around(world, machine, RoledDef(), angle).Complete();
      sets.Add(Render(machine.CellsWithRole(CellRole.Flue)));
    }

    // The guard the per-facing theory cannot state: a mapping that ignored the angle entirely still agrees
    // with itself at every facing. Four distinct sets is the fact that rules it out.
    Assert.Equal(4, sets.Distinct().Count());
  }

  [Fact]
  public void A_role_cell_is_always_a_cell_the_structure_owns()
  {
    foreach (int angle in new[] { 0, 90, 180, 270 })
    {
      var (world, machine) = Stand(angle);
      StructureRig.Around(world, machine, RoledDef(), angle).Complete();

      // Roles are resolved by index against vanilla's own transformed offset table, so a role cell cannot
      // land outside the footprint - which is the failure the drifted MetalTapCell hand-list actually has
      // (column 8 of an 8-wide grid). Stated as an invariant rather than as coordinates.
      //
      // Which makes the loop below true by construction, so on its own it states nothing at all: it
      // passes even for `OwnsCell => true`, and it passed for every mutation of the role mapping this was
      // checked against. The negative control is what turns it into a claim - that OwnsCell discriminates,
      // and that it discriminates against the transformed footprint (point it at Offsets rather than
      // TransformedOffsets and this test fails at three facings of four).
      // This is the only assertion in the tree that OwnsCell ever answers false. Downstream furnace
      // suites repeat the positive loop; none of them repeated this half until now.
      Assert.False(
        machine.OwnsCell(Anchor.AddCopy(0, -1, 0)),
        $"the cell under the anchor is below layer 0, so it is not owned at {angle} deg"
      );
      Assert.All(
        machine
          .CellsWithRole(CellRole.Flue)
          .Concat(machine.CellsWithRole(CellRole.Tuyere)),
        cell => Assert.True(machine.OwnsCell(cell), $"{cell} at {angle} deg")
      );
    }
  }

  [Fact]
  public void Turning_the_structure_moves_the_role_cells_rather_than_answering_out_of_the_old_facing()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, RoledDef()).Complete();
    Assert.Equal(
      ExpectedAt(FlueCells, 0),
      Render(machine.CellsWithRole(CellRole.Flue))
    );

    // A wrench turn: the machine re-derives its angle and reloads on the next monitor tick. The cells are
    // cached in world space, so a cache surviving that would keep answering north.
    machine.Angle = 90;
    world.AdvanceBlockEntityTime(3000);

    Assert.Equal(
      ExpectedAt(FlueCells, 90),
      Render(machine.CellsWithRole(CellRole.Flue))
    );
  }

  #endregion

  #region The additive guarantee

  [Fact]
  public void A_layout_with_no_roles_emits_no_roles_attribute()
  {
    // Every structure shipped before this feature is of this shape, and none of their goldens may move.
    Assert.Null(Attributes(UnroledDef())["multiblockRoles"]);
  }

  [Fact]
  public void Adding_roles_changes_nothing_about_the_structure_a_layout_emits()
  {
    // The additive guarantee stated at its source: the same drawing with and without Role() calls emits
    // byte-identical multiblockStructure JSON. Roles are metadata beside the layout, never part of it.
    Assert.Equal(
      Attributes(UnroledDef())["multiblockStructure"]!.ToString(
        Newtonsoft.Json.Formatting.None
      ),
      Attributes(RoledDef())["multiblockStructure"]!.ToString(
        Newtonsoft.Json.Formatting.None
      )
    );
  }

  [Fact]
  public void Numbers_are_still_handed_out_in_legend_declaration_order()
  {
    // Numbering moved from per-glyph to per-code. For a layout with one glyph per code - which is every
    // layout in the repo - that has to be the identity, or all ten shipped goldens churn.
    var numbers = (JObject)
      Attributes(
        ExBlockDef
          .Create("exlib", "testmega")
          .MultiblockLayout(s =>
            s.Legend('C', "exlib:testmega*")
              .Legend('#', "exlib:testbrick*")
              .Legend('a', "game:air")
              .Layer(0, "C # a")
          )
      )["multiblockStructure"]!["blockNumbers"]!;

    Assert.Equal(
      """{"exlib:testmega*":1,"exlib:testbrick*":2,"game:air":3}""",
      numbers.ToString(Newtonsoft.Json.Formatting.None)
    );
  }

  [Fact]
  public void A_structure_whose_layout_declares_no_roles_answers_every_role_empty()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, UnroledDef()).Complete();

    foreach (CellRole role in Enum.GetValues<CellRole>())
      Assert.Empty(machine.CellsWithRole(role));
    // ...and the footprint is demonstrably non-empty beside it, so this cannot pass by the machine having
    // failed to load a layout at all.
    Assert.NotEmpty(machine.CellsAccepting(new AssetLocation("game:air")));
  }

  #endregion

  #region Build-time failures

  [Fact]
  public void A_role_on_a_glyph_the_legend_does_not_define_fails_the_build()
  {
    // The silent empty set roles exist to remove: the glyph is not in the drawing's alphabet, so the role
    // would answer nothing for ever with no error anywhere.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Role('q', CellRole.Flue)
            .Layer(0, "C")
        )
    );
    Assert.Contains("'q'", ex.Message, StringComparison.Ordinal);
    Assert.Contains("Flue", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void A_role_on_a_glyph_the_drawing_never_uses_fails_the_build()
  {
    // The same silent empty set by the other route - legend entry present, glyph never drawn.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('v', "game:air")
            .Role('v', CellRole.Flue)
            .Layer(0, "C")
        )
    );
    Assert.Contains("'v'", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void A_role_glyph_that_is_only_drawn_under_a_second_glyph_still_fails()
  {
    // A role two glyphs share is satisfied only when each is drawn: counting the role's cells would let a
    // drawing that lost one facing look complete.
    Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('t', "exlib:testtuyere*")
            .Legend('y', "exlib:testtuyere*")
            .Role('t', CellRole.Tuyere)
            .Role('y', CellRole.Tuyere)
            .Layer(0, "C t")
        )
    );
  }

  [Fact]
  public void One_glyph_can_carry_two_roles_and_its_cells_answer_to_both()
  {
    // A cell can genuinely be two things at once, and nothing else in the DSL can say so: a cell holds
    // exactly one glyph, so two overlapping roles cannot be split across two of them. The shaft furnaces'
    // crucible is the shipped case - burden column and metal pool are the same cells today - and this is
    // the mechanism that lets one drawing state both.
    JObject roles = (JObject)Attributes(OverlappingDef())["multiblockRoles"]!;

    // Both roles are emitted, in enum order, and each holds the same single cell - the `v` at (2,0,0).
    // The neighbouring `a` is the identical code and carries neither, so this is a statement about the
    // glyph rather than about game:air.
    Assert.Equal(["Flue", "Damper"], roles.Properties().Select(p => p.Name));
    foreach (string role in new[] { "Flue", "Damper" })
    {
      JToken cell = Assert.Single((JArray)roles[role]!);
      Assert.Equal(
        (2, 0, 0),
        ((int)cell["x"]!, (int)cell["y"]!, (int)cell["z"]!)
      );
    }
  }

  [Fact]
  public void Restating_a_glyphs_own_role_is_harmless()
  {
    // Idempotence: a repeat adds nothing, so a role named twice does not emit its cells twice. That is
    // what makes the accumulation above a set rather than a list.
    ExBlockDef def = ExBlockDef
      .Create("exlib", "testmega")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "exlib:testmega*")
          .Legend('v', "game:air")
          .Role('v', CellRole.Flue)
          .Role('v', CellRole.Flue)
          .Layer(0, "C v v")
      );

    Assert.Equal(
      ["Flue"],
      ((JObject)Attributes(def)["multiblockRoles"]!)
        .Properties()
        .Select(p => p.Name)
    );
    Assert.Equal(
      2,
      ((JArray)Attributes(def)["multiblockRoles"]!["Flue"]!).Count
    );
  }

  [Fact]
  public void One_glyph_cannot_carry_two_codes()
  {
    // A cell gets one block number, so the second code would silently stop being required anywhere. Latent
    // before roles; unacceptable once glyphs are meaningful.
    Assert.Throws<ArgumentException>(() =>
      new MultiblockLayoutBuilder()
        .Legend('a', "game:air")
        .Legend('a', "exlib:testbrick*")
    );
  }

  [Fact]
  public void A_layout_cannot_be_both_a_burden_column_and_a_fuel_bed()
  {
    // The shaft/firebox furnace split, made structural instead of something a downstream test has to
    // notice. A shaft's glyph is `*:@(air|coalpile|furnace-chargepile)`; a firebox's is `@(air|coalpile)`.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('c', "*:@(air|coalpile|furnace-chargepile)")
            .Legend('f', "@(air|coalpile)")
            .Role('c', CellRole.Chargeable)
            .Role('f', CellRole.Firebox)
            .Layer(0, "C c f")
        )
    );
    Assert.Contains("Chargeable", ex.Message, StringComparison.Ordinal);
    Assert.Contains("Firebox", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void Either_of_the_two_fuel_roles_alone_is_fine()
  {
    // The guard above must not have made a plain shaft or a plain hearth unbuildable.
    foreach (CellRole role in new[] { CellRole.Chargeable, CellRole.Firebox })
      Assert.NotNull(
        Attributes(
          ExBlockDef
            .Create("exlib", "testmega")
            .MultiblockLayout(s =>
              s.Legend('C', "exlib:testmega*")
                .Legend('c', "@(air|coalpile)")
                .Role('c', role)
                .Layer(0, "C c")
            )
        )["multiblockRoles"]
      );
  }

  #endregion

  #region Role arity

  [Fact]
  public void Exactly_the_two_taps_are_single_cell_roles()
  {
    // The arity split stated once, so adding a role forces a deliberate answer rather than inheriting the
    // loose default by omission. Everything else is a genuine set: a shaft is a column, a hearth has two
    // tuyeres, and a stack throttled at both ends is one layout with two dampers.
    Assert.Equal(
      new[] { CellRole.MetalTap, CellRole.SlagTap },
      Enum.GetValues<CellRole>().Where(CellRoles.IsSingleCell).ToArray()
    );
  }

  [Fact]
  public void A_single_cell_role_drawn_twice_fails_the_build()
  {
    // What this buys 2.6.4: `MetalTapCell` and `SlagTapCell` are a single Vec3i, so migrating them writes
    // CellsWithRole(MetalTap).Single(). Without a build-time promise that is an InvalidOperationException
    // out of LINQ on a live block entity, on whichever layout first drew the glyph twice.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('m', "game:air")
            .Role('m', CellRole.MetalTap)
            .Layer(0, "C m m")
        )
    );
    Assert.Contains("draws 2 cells", ex.Message, StringComparison.Ordinal);
    Assert.Contains("MetalTap", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void Two_glyphs_cannot_share_a_single_cell_role()
  {
    // Counted over the drawn cells rather than over the role table, so the two-glyph route - the one the
    // layouts' own T/Y idiom makes natural - fails the same way as one glyph drawn twice.
    Assert.Throws<InvalidOperationException>(() =>
      ExBlockDef
        .Create("exlib", "testmega")
        .MultiblockLayout(s =>
          s.Legend('C', "exlib:testmega*")
            .Legend('m', "game:air")
            .Legend('n', "game:air")
            .Role('m', CellRole.SlagTap)
            .Role('n', CellRole.SlagTap)
            .Layer(0, "C m n")
        )
    );
  }

  [Fact]
  public void A_single_cell_role_drawn_once_builds_and_a_set_role_carries_many()
  {
    // Both directions in one layout: one tap, four flue cells. Without the accept half, a guard that
    // rejected every marked role - or every role - would look correct.
    var roles = (JObject)
      Attributes(
        ExBlockDef
          .Create("exlib", "testmega")
          .MultiblockLayout(s =>
            s.Legend('C', "exlib:testmega*")
              .Legend('m', "game:air")
              .Legend('v', "game:air")
              .Role('m', CellRole.MetalTap)
              .Role('v', CellRole.Flue)
              .Layer(0, "C m v v v v")
          )
      )["multiblockRoles"]!;

    Assert.Single((JArray)roles["MetalTap"]!);
    Assert.Equal(4, ((JArray)roles["Flue"]!).Count);
  }

  #endregion

  #region Caching

  [Fact]
  public void The_role_answer_is_computed_once_and_handed_back()
  {
    var (world, machine) = Stand();
    StructureRig.Around(world, machine, RoledDef()).Complete();

    // Consumers read these per state change, so the footprint must not be re-walked per read. Same
    // instance, not merely equal contents.
    Assert.Same(
      machine.CellsWithRole(CellRole.Flue),
      machine.CellsWithRole(CellRole.Flue)
    );
  }

  [Fact]
  public void A_structure_asked_before_its_layout_arrives_answers_properly_afterwards()
  {
    var (world, machine) = Stand();

    // The block carries no attributes yet - the state a client-side block entity is in until the layout
    // loads. Empty is right.
    Assert.Empty(machine.CellsWithRole(CellRole.Flue));

    StructureRig.Around(world, machine, RoledDef()).Complete();

    // ...and the early read did not poison the cache: the late-arriving layout is answered in full.
    // What this does not pin is the "never memoise the not-yet-loaded empty" early return, for the reason
    // task 3.0a established by mutation on the sibling accessor: EnsureStructureLoaded re-enters the reload
    // path while the layout is missing, and that path drops the cache anyway. This pins the behaviour a
    // client depends on, not that branch.
    Assert.Equal(
      ExpectedAt(FlueCells, 0),
      Render(machine.CellsWithRole(CellRole.Flue))
    );
  }

  #endregion
}
