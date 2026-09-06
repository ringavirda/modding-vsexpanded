using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The save shape of every block entity iiex declares, pinned before any hand-written
/// <c>ToTreeAttributes</c>/<c>FromTreeAttributes</c> pair is converted to <see cref="ExpandedLib.Blocks.PersistAttribute"/>
/// and <c>Persisted</c> calls. A converted class that still writes the same keys leaves its golden
/// untouched; one that does not is a save-format regression, caught here rather than in a save file.
/// <para>
/// A type that cannot be built through its parameterless constructor and a fresh <see cref="TestWorld"/>
/// alone is named in <see cref="AllowListed"/> with the reason, rather than coaxed into constructing.
/// </para>
/// </summary>
public class TreeKeyGoldenTests {
  private static readonly Assembly Mod = typeof(FurnaceCellRoles).Assembly;

  // Types this suite cannot construct headlessly (they need a live megablock host, a running network,
  // or another piece of world state a bare BlockEntity + TestWorld does not supply) and so are exempt
  // from the tree-key golden. The hand-written pair on each stays as it is.
  private static readonly HashSet<Type> AllowListed = [];

  /// <summary>Every concrete block entity iiex declares, one theory case per type name.</summary>
  public static IEnumerable<object[]> Cases() =>
    Types().Select(t => new object[] { t.FullName! });

  private static Type[] Types() =>
    ReflectionScan
      .GetCandidateTypes(Mod)
      .Where(t => typeof(BlockEntity).IsAssignableFrom(t))
      .Where(t => !AllowListed.Contains(t))
      .OrderBy(t => t.FullName, StringComparer.Ordinal)
      .ToArray();

  [Theory]
  [MemberData(nameof(Cases))]
  public void Matches_its_golden(string typeName) {
    Type type = Mod.GetType(typeName)!;
    var be = (BlockEntity)Activator.CreateInstance(type)!;
    be.Pos = new BlockPos(0, 1, 0);
    be.Block = TestBlocks.Configure(new Block(), "iiex:probe", 1);
    new TestWorld().Attach(be);

    TreeKeys.AssertGolden(be, "iiex");
  }
}
