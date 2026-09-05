using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Ties the sand casting bed's drawn mesh to the volume it reserves, at every orientation it can be laid
/// at, and then cell by cell: the element a slot draws must land in the cell that slot carves. The whole-box
/// rule is <see cref="MegablockFrames"/>'s, same as the Cornish boiler's and the long cell's guards. The
/// per-slot rule is what the box rule cannot see - the bed's footprint is three cells wide and symmetric,
/// so a mesh that lands the rows right can still draw every flank on the wrong side of the spine.
/// </summary>
public class CastingBedFootprintGuards {
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_drawn_mesh_lands_inside_the_reserved_footprint(string side) {
    JObject json = BlockSandCastingBed.Definitions("iiex").Single().ToJson();
    BlockSandCastingBed block = Bed(json, side);

    JToken shape = json["shape"]!;
    string? misfit = MegablockFrames.Misfit(
      MegablockFrames.ShapeFile((string)shape["base"]!),
      (int)shape["rotateYByType"]![$"*-{side[0]}"]!,
      block,
      block.StructureAngle
    );

    Assert.True(misfit == null, $"A '{side}' casting bed {misfit}");
  }

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void Every_slot_is_drawn_in_the_cell_that_carves_it(string side) {
    JObject json = BlockSandCastingBed.Definitions("iiex").Single().ToJson();
    BlockSandCastingBed block = Bed(json, side);

    JToken shape = json["shape"]!;
    string file = MegablockFrames.ShapeFile((string)shape["base"]!);
    int spin = (int)shape["rotateYByType"]![$"*-{side[0]}"]!;

    foreach (BedSlot slot in SandBedLayout.Slots) {
      (int dx, int dz) = SandBedLayout.OffsetOf(slot);
      Vec3i cell = ExOrientation.RotateOffset(dx, 0, dz, block.StructureAngle);

      foreach (BedSlotState state in States(slot)) {
        string element = SandBedLayout.ElementFor(slot, state);
        (float x, float z) = DrawnCentre(file, element);
        (double rx, double rz) = ExOrientation.RotateXZ(x, z, spin);

        Assert.True(
          Math.Abs(rx - cell.X) < 0.5 && Math.Abs(rz - cell.Z) < 0.5,
          $"A '{side}' bed draws {slot} as {state} with '{element}', "
            + $"centred ({rx:0.##}, {rz:0.##}) blocks from the principal, but "
            + $"carves that slot at cell ({cell.X}, {cell.Z}). The shape spins "
            + $"by {spin}deg and the footprint by {block.StructureAngle}deg."
        );
      }
    }
  }

  private static BlockSandCastingBed Bed(JObject json, string side) {
    var block = TestBlocks.Configure(
      new BlockSandCastingBed(),
      $"iiex:casting-sandbed-cream-{side[0]}",
      220,
      ("brick", "cream"),
      ("side", side)
    );
    block.Attributes = new JsonObject((JObject)json["attributes"]!);
    return block;
  }

  /// <summary>Every state <paramref name="slot"/> can hold, each of which draws its own element.</summary>
  private static IEnumerable<BedSlotState> States(BedSlot slot) =>
    new[] { BedSlotState.Sand, BedSlotState.Runner, BedSlotState.Mold }.Where(
      slot.Accepts
    );

  /// <summary>
  /// Where <paramref name="element"/> is drawn, in blocks from the principal cell's centre before the
  /// block's own spin. <see cref="ShapeExtents.Bounds"/> measures an element in its own frame, so the
  /// groups it hangs under are added back on.
  /// </summary>
  private static (float X, float Z) DrawnCentre(
    string shapeFile,
    string element
  ) {
    (float[] min, float[] max) = ShapeExtents.Bounds(shapeFile, element);
    (float gx, float gz) = GroupOffset(shapeFile, element);
    return (
      ((min[0] + max[0]) / 2f + gx - 8f) / 16f,
      ((min[2] + max[2]) / 2f + gz - 8f) / 16f
    );
  }

  /// <summary>
  /// The summed <c>from</c> of every group <paramref name="element"/> hangs under, in voxels. The bed's
  /// groups only translate; one that turns fails here rather than being placed as if it did not.
  /// </summary>
  private static (float X, float Z) GroupOffset(
    string shapeFile,
    string element
  ) {
    var groups = new List<JToken>();
    Assert.True(
      Chain(
        JObject.Parse(File.ReadAllText(shapeFile))["elements"],
        element,
        groups
      ),
      $"{shapeFile} has no element '{element}'"
    );

    float x = 0f;
    float z = 0f;
    foreach (JToken group in groups) {
      foreach (string axis in new[] { "rotationX", "rotationY", "rotationZ" })
        Assert.True(
          (float?)group[axis] is null or 0f,
          $"group '{(string?)group["name"]}' above '{element}' is turned on "
            + $"{axis}, so its children cannot be placed by offset alone"
        );
      x += (float?)group["from"]?[0] ?? 0f;
      z += (float?)group["from"]?[2] ?? 0f;
    }
    return (x, z);
  }

  /// <summary>
  /// Fills <paramref name="groups"/> with the ancestors of <paramref name="element"/>, outermost first,
  /// and reports whether it was found at all.
  /// </summary>
  private static bool Chain(
    JToken? elements,
    string element,
    List<JToken> groups
  ) {
    foreach (JToken el in elements ?? new JArray()) {
      if ((string?)el["name"] == element)
        return true;
      groups.Add(el);
      if (Chain(el["children"], element, groups))
        return true;
      groups.RemoveAt(groups.Count - 1);
    }
    return false;
  }
}
