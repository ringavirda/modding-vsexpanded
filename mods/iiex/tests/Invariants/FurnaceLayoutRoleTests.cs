using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// A law about the furnace family's own roles, not about the multiblock mechanism: a layout is a shaft
/// or a firebox, never both - <see cref="FurnaceCellRoles.Chargeable"/> claims a burden column,
/// <see cref="FurnaceCellRoles.Firebox"/> a plain fuel bed, and one layout claiming both would be
/// asking for a column and a bed on the same cells. exlib's <c>MultiblockLayoutBuilder</c> no longer
/// knows what either role means, so this - not a build-time guard in exlib - is what stands in its
/// place. Scans every iiex block def with <see cref="DefinitionGoldens.Collect"/> rather than picking
/// specific furnace blocks by name, so a new furnace core is covered on arrival.
/// </summary>
public class FurnaceLayoutRoleTests {
  private static readonly Assembly Mod = typeof(FurnaceCellRoles).Assembly;

  [Fact]
  public void No_iiex_layout_marks_both_chargeable_and_firebox() {
    foreach (IExDef def in DefinitionGoldens.Collect("iiex", Mod)) {
      // Only a blocktype def carries attributes; a recipe file's ToJson() is an array/object of a
      // different shape entirely.
      if (def.ToJson() is not JObject json)
        continue;
      if (json["attributes"]?["multiblockRoles"] is not JObject roles)
        continue;

      IEnumerable<string> names = roles.Properties().Select(p => p.Name);
      Assert.False(
        names.Contains(FurnaceCellRoles.Chargeable.ToString())
          && names.Contains(FurnaceCellRoles.Firebox.ToString()),
        $"{def.Location} marks both Chargeable and Firebox; a furnace holds a burden column or a fuel "
          + "bed, never both."
      );
    }
  }

  /// <summary>
  /// Only <see cref="FurnaceCellRoles.MetalTap"/> and <see cref="FurnaceCellRoles.SlagTap"/> are
  /// single-cell - the tap alcove holds one cell of liquid, everything else names a set. Enumerates
  /// the static <see cref="CellRole"/> fields by reflection, so a role added later is checked without
  /// this test needing an edit.
  /// </summary>
  [Fact]
  public void Only_the_taps_are_single_cell_roles() {
    var singleByName = new[]
    {
      nameof(FurnaceCellRoles.MetalTap),
      nameof(FurnaceCellRoles.SlagTap),
    };
    foreach (
      FieldInfo field in typeof(FurnaceCellRoles).GetFields(
        BindingFlags.Public | BindingFlags.Static
      )
    ) {
      if (field.FieldType != typeof(CellRole))
        continue;
      var role = (CellRole)field.GetValue(null)!;
      bool wantSingle = singleByName.Contains(field.Name);
      Assert.True(
        role.IsSingle == wantSingle,
        $"FurnaceCellRoles.{field.Name}.IsSingle is {role.IsSingle}, expected {wantSingle}."
      );
    }
  }
}
