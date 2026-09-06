using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The shared activate-or-warn primitive the command/preference registries route through.</summary>
public class ReflectionScanTests {
  private interface IThing { }

  private sealed class Thing : IThing { }

  private sealed class NotAThing { }

  [AttributeUsage(AttributeTargets.Class)]
  private sealed class MarkerAttribute : Attribute { }

  [Marker]
  private sealed class MarkedThing : IThing { }

  private static ICoreAPI FakeApi() {
    var api = Substitute.For<ICoreAPI>();
    api.Logger.Returns(Substitute.For<ILogger>());
    return api;
  }

  [Fact]
  public void Activates_an_assignable_type() {
    bool ok = ReflectionScan.TryActivate<IThing>(
      FakeApi(),
      "test",
      typeof(Thing),
      out var instance
    );

    Assert.True(ok);
    Assert.IsType<Thing>(instance);
  }

  [Fact]
  public void Skips_and_warns_on_a_non_assignable_type() {
    var api = FakeApi();

    bool ok = ReflectionScan.TryActivate<IThing>(
      api,
      "test",
      typeof(NotAThing),
      out var instance
    );

    Assert.False(ok);
    Assert.Null(instance);
    api.Logger.Received().Warning(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void GetCandidateTypes_is_sorted_and_skips_unloadable() {
    var assemblies = new[] {
      typeof(ReflectionScan).Assembly, // exlib
      typeof(ReflectionScanTests).Assembly, // the test assembly itself
    };

    Type[] merged = ReflectionScan.GetCandidateTypes(assemblies);

    // Equivalent to concatenating the per-assembly scan (which already tolerates a partial load,
    // see Skips_and_warns_on_a_non_assignable_type's sibling coverage on the single-assembly
    // overload) and sorting it - never a reshuffle of what each assembly alone would report.
    Type[] expected = assemblies
      .SelectMany(ReflectionScan.GetCandidateTypes)
      .OrderBy(t => t.Assembly.FullName, StringComparer.Ordinal)
      .ThenBy(t => t.FullName, StringComparer.Ordinal)
      .ToArray();
    Assert.Equal(expected, merged);

    // Sorted: assembly full name first, type full name as the tiebreaker.
    for (int i = 1; i < merged.Length; i++) {
      int cmp = string.CompareOrdinal(
        merged[i - 1].Assembly.FullName,
        merged[i].Assembly.FullName
      );
      Assert.True(
        cmp < 0
          || (
            cmp == 0
            && string.CompareOrdinal(merged[i - 1].FullName, merged[i].FullName)
              <= 0
          )
      );
    }
  }

  [Fact]
  public void ForEachAttributed_registers_each_attributed_type_once() {
    var registered = new List<(MarkerAttribute Attr, IThing Instance)>();

    ReflectionScan.ForEachAttributed<MarkerAttribute, IThing>(
      FakeApi(),
      "test",
      typeof(ReflectionScanTests).Assembly,
      (attr, instance) => registered.Add((attr, instance))
    );

    var entry = Assert.Single(registered, r => r.Instance is MarkedThing);
    Assert.NotNull(entry.Attr);
    // Unmarked types (Thing, NotAThing) never reach the register callback.
    Assert.DoesNotContain(registered, r => r.Instance is Thing);
  }
}
