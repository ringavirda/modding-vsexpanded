using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using SteelIndustryExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The save shape of every block entity siex declares, pinned before any hand-written
/// <c>ToTreeAttributes</c>/<c>FromTreeAttributes</c> pair is converted to <see cref="ExpandedLib.Blocks.PersistAttribute"/>
/// and <c>Persisted</c> calls. See <c>IronIndustryExpanded.Tests.TreeKeyGoldenTests</c> for the rationale;
/// this is the same check run against siex's own assembly.
/// </summary>
public class TreeKeyGoldenTests {
  private static readonly Assembly Mod = typeof(BlockEntityConverterBessemer).Assembly;

  // Types this suite cannot construct headlessly and so are exempt from the tree-key golden. The
  // hand-written pair on each stays as it is.
  private static readonly HashSet<Type> AllowListed = [];

  /// <summary>Every concrete block entity siex declares, one theory case per type name.</summary>
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
    be.Block = TestBlocks.Configure(new Block(), "siex:probe", 1);
    new TestWorld().Attach(be);

    TreeKeys.AssertGolden(be, "siex");
  }
}
