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
}
