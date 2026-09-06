using ExpandedLib.Helpers;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The <c>Api.Side == EnumAppSide.X</c> / <c>World.Side == EnumAppSide.X</c> check every
/// machine writes by hand.</summary>
public class ExSideTests {
  [Fact]
  public void Api_IsServer_reads_the_side() {
    var api = Substitute.For<ICoreAPI>();
    api.Side.Returns(EnumAppSide.Server);

    Assert.True(api.IsServer());
    Assert.False(api.IsClient());
  }

  [Fact]
  public void Api_IsClient_reads_the_side() {
    var api = Substitute.For<ICoreAPI>();
    api.Side.Returns(EnumAppSide.Client);

    Assert.True(api.IsClient());
    Assert.False(api.IsServer());
  }

  [Fact]
  public void World_IsServer_reads_the_side() {
    var world = Substitute.For<IWorldAccessor>();
    world.Side.Returns(EnumAppSide.Server);

    Assert.True(world.IsServer());
    Assert.False(world.IsClient());
  }

  [Fact]
  public void World_IsClient_reads_the_side() {
    var world = Substitute.For<IWorldAccessor>();
    world.Side.Returns(EnumAppSide.Client);

    Assert.True(world.IsClient());
    Assert.False(world.IsServer());
  }
}
