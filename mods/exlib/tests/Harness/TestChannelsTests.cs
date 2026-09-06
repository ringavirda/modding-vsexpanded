using System;
using ExpandedLib.Testing;
using ProtoBuf;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="TestChannels"/>: a registered packet round-trips through the game's own serialiser to
/// the handler on the other side, and an unregistered type throws naming it.
/// <see cref="ExConfigSyncModSystem"/>'s join push, observed through <c>SentToClients</c>
/// (<c>ConfigSyncTests.PlayerJoin_sends_one_packet_per_registered_section</c>), is the production
/// proof of the pair.
/// </summary>
public class TestChannelsTests {
  [ProtoContract]
  private sealed class TestPacket {
    [ProtoMember(1)]
    public int Value;

    [ProtoMember(2)]
    public string Text = string.Empty;
  }

  [Fact]
  public void A_registered_packet_round_trips_through_the_wire() {
    using var world = new TestWorld();
    var channels = TestChannels.Create(world, "exlibtest.channels");
    channels.Server.RegisterMessageType<TestPacket>();
    channels.Client.RegisterMessageType<TestPacket>();

    TestPacket? received = null;
    channels.Client.SetMessageHandler<TestPacket>(p => received = p);

    var sent = new TestPacket { Value = 5, Text = "hi" };
    channels.Server.SendPacket(sent, channels.Sender);

    Assert.NotNull(received);
    Assert.NotSame(sent, received); // came back through the wire, not the same instance
    Assert.Equal(5, received!.Value);
    Assert.Equal("hi", received.Text);
    Assert.Single(channels.SentToClients);
  }

  [Fact]
  public void An_unregistered_type_throws_naming_it() {
    using var world = new TestWorld();
    var channels = TestChannels.Create(world, "exlibtest.channels-unregistered");

    var ex = Assert.Throws<InvalidOperationException>(
      () => channels.Client.SendPacket(new TestPacket())
    );
    Assert.Contains(nameof(TestPacket), ex.Message);
  }
}
