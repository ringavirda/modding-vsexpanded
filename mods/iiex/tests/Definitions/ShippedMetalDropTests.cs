using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// What a shipped metal hands back when it is chipped, shattered or broken out - and the economic rule
/// that answer has to obey. A mod metal whose solid drop is a vanilla bit launders itself into vanilla's
/// forging tree: twenty bits smelt to a plain iron ingot, so cheap metal bought at the blast furnace
/// becomes anything an expensive one could have made, and the process that was supposed to gate it is
/// skipped entirely. Neither golden catches that, because each side is individually well-formed.
/// See docs/design/items/cast-parts.md and docs/design/mechanics/density-rule.md.
/// </summary>
public class ShippedMetalDropTests {
  private static IEnumerable<(string File, MetalDef Def)> Shipped() {
    string dir = Path.Combine(RepoPaths.Assets("iiex"), "config", "metals");
    foreach (string file in Directory.EnumerateFiles(dir, "*.json")) {
      MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
        File.ReadAllText(file)
      );
      Assert.NotNull(def);
      yield return (Path.GetFileName(file), def!);
    }
  }

  #region The corpus is real

  [Fact]
  public void The_shipped_metal_catalogue_is_not_empty() {
    // Every check below walks the catalogue, so an empty or unreadable directory would pass all of them
    // by having nothing to judge.
    Assert.NotEmpty(Shipped());
  }

  #endregion

  #region A metal hands back its own metal

  [Fact]
  public void A_metal_that_generates_its_own_bits_drops_those_bits() {
    // This is the exact shape of the defect: cast iron generated `iiex:metalbit-castiron`, shipped its
    // lang, and then named `game:metalbit-iron` as the thing a chisel actually recovers - so the item
    // existed, was obtainable nowhere, and the metal paid out in vanilla iron instead.
    var offenders = new List<string>();
    foreach ((string file, MetalDef def) in Shipped()) {
      if (def.ItemForms == null || !def.ItemForms.Contains("bits"))
        continue;

      string own = $"iiex:metalbit-{def.Code}";
      if (def.SolidDrop != own)
        offenders.Add($"{file}: generates {own} but drops '{def.SolidDrop}'");
    }

    Assert.True(
      offenders.Count == 0,
      "metal(s) generate their own bits and hand back something else:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void No_shipped_metal_pays_out_in_vanilla_bits() {
    // The rule the one above is a special case of. A vanilla bit is a vanilla ingot twenty bits later,
    // and from there the whole vanilla tool tree - which is what the puddling route, the converter and
    // the blown-iron ruling all exist to gate.
    var offenders = Shipped()
      .Where(m => m.Def.SolidDrop?.StartsWith("game:") == true)
      .Select(m => $"{m.File}: drops '{m.Def.SolidDrop}'")
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "metal(s) hand back vanilla bits, which smelt to a vanilla ingot and skip the process that "
        + "should have gated them:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void What_a_metal_pays_out_can_be_charged_back_into_the_cupola() {
    // Closing the forging bypass must not open a dead end. The cupola charges only items holding the
    // `scrap` role, so a solid drop outside that list is metal the player recovers and can then do
    // nothing at all with - which is worse than the leak it replaced.
    string roles = File.ReadAllText(
      Path.Combine(RepoPaths.Assets("iiex"), "config", "materialroles.json")
    );

    var offenders = Shipped()
      .Where(m =>
        m.Def.SolidDrop != null && !roles.Contains($"\"{m.Def.SolidDrop}\"")
      )
      .Select(m =>
        $"{m.File}: drops '{m.Def.SolidDrop}', which holds no scrap role"
      )
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "metal(s) hand back something the cupola will not take, so the recovery loop dead-ends:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  #endregion
}
