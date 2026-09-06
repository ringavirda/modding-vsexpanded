using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Catalogues;
using IronIndustryExpanded.Items;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// The dies the fastener benches work with. A die names the job, so a bench with no die fitted has
/// no work at all - which is what separates a die from a <see cref="MachineTool"/>, whose tier names only a
/// hardness while the jobs come from a declared table.
/// See docs/design/mechanics/machining-line.md § Tooling.
/// <para>
/// The job travels on the item rather than in a table under our domain, so a third party adds a bench job
/// by shipping a die - no naming blessing, no patch of ours. That seam is
/// <see cref="ItemDie.Itemtype"/>, and these two are its first shipped users.
/// </para>
/// </summary>
public class BenchDieItemDefinitions : IExItemDefProvider {
  /// <summary>The machine key the nail die's job is declared under, and the bench that reads it.</summary>
  public const string NailMachine = "nailcutter";

  /// <summary>The machine key the rivet die's job is declared under.</summary>
  public const string RivetMachine = "riveter";

  /// <summary>Nail bundles one nail plate is cut into.</summary>
  public const int NailsPerPlate = 4;

  // Both benches are gated on drive rather than on temper: a die has no tier of its own, and a press that
  // upsets a head asks more of the run than a cutter that only shears. The riveter's gate is the higher of
  // the two, which is what its drawn gear train is for.
  private const double NailTorque = 0.2;
  private const double RivetTorque = 0.3;

  private const double NailSeconds = 2;
  private const double RivetSeconds = 3;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ItemDie
        .Itemtype(
          domain,
          new Dictionary<string, object>
          {
            // Both are whole-item jobs - no stage, no family - so the blank is consumed and every bundle
            // leaves at once. A crop would leave a remainder; a fastener blank has none.
            ["nail"] = ItemDie.Job(
              NailMachine,
              $"{domain}:nailplate",
              "game:metalnailsandstrips-iron",
              count: NailsPerPlate,
              minTorque: NailTorque,
              seconds: NailSeconds
            ),
            ["rivet"] = ItemDie.Job(
              RivetMachine,
              $"{domain}:rivetrod",
              $"{domain}:{FastenerItemDefinitions.RivetCode}",
              count: FastenerItemDefinitions.RivetsPerRod,
              minTorque: RivetTorque,
              seconds: RivetSeconds
            ),
          },
          // No die is drawn - only the steam hammer's is - so both take the placeholder the roll sets and
          // the undrawn rolled products already use, and the tooling exists before its art does.
          "game:item/ingot"
        )
        .TextureAll("game:block/metal/plate/iron"),
    ];
}
