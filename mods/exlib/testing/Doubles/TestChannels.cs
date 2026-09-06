using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// A paired client and server channel that serialises through the game's own serialiser
/// (<see cref="SerializerUtil"/>) and delivers synchronously to the handler registered on the other
/// side - no scheduler, no wire. Built by <see cref="TestWorld.Channels"/> so a <c>ModSystem</c> that
/// registers a channel in <c>StartServerSide</c>/<c>StartClientSide</c> runs unchanged under the
/// harness.
/// </summary>
public sealed class TestChannels {
  // Shared by both sides so a type registered on either channel is sendable on either: production
  // code registers the same message types, in the same order, on both sides anyway.
  private readonly HashSet<Type> _registered = [];
  private readonly Dictionary<Type, Delegate> _serverHandlers = [];
  private readonly Dictionary<Type, Delegate> _clientHandlers = [];
  private readonly List<object> _sentToServer = [];
  private readonly List<object> _sentToClients = [];

  private TestChannels(string channelName, IServerPlayer sender) {
    ChannelName = channelName;
    Sender = sender;
    Server = new ServerChannel(this);
    Client = new ClientChannel(this);
  }

  /// <summary>Builds a paired channel standing in for <paramref name="channelName"/>, with a fresh
  /// <see cref="TestPlayer"/> in <paramref name="world"/> as the client's identity on the server side.</summary>
  public static TestChannels Create(TestWorld world, string channelName) =>
    new(channelName, world.Player().ServerPlayer!);

  /// <summary>The channel name both sides were registered with.</summary>
  public string ChannelName { get; }

  /// <summary>The player the client's sent packets arrive as on the server side.</summary>
  public IServerPlayer Sender { get; }

  public IServerNetworkChannel Server { get; }
  public IClientNetworkChannel Client { get; }

  /// <summary>Every packet the client sent, deserialised through a fresh instance - oldest first.</summary>
  public IReadOnlyList<object> SentToServer => _sentToServer;

  /// <summary>Every packet the server sent or broadcast, deserialised through a fresh instance -
  /// oldest first, one entry per targeted player.</summary>
  public IReadOnlyList<object> SentToClients => _sentToClients;

  private void Register(Type type) => _registered.Add(type);

  private void RequireRegistered(Type type) {
    if (!_registered.Contains(type))
      throw new InvalidOperationException(
        $"'{type}' was sent on channel '{ChannelName}' without RegisterMessageType."
      );
  }

  /// <summary>Round-trips <paramref name="message"/> through the real serialiser, records it and
  /// invokes the server-side handler, if one is registered.</summary>
  private void DeliverToServer<T>(T message) {
    RequireRegistered(typeof(T));
    T copy = SerializerUtil.Deserialize<T>(SerializerUtil.Serialize(message));
    _sentToServer.Add(copy!);
    if (_serverHandlers.TryGetValue(typeof(T), out Delegate? handler))
      ((NetworkClientMessageHandler<T>)handler)(Sender, copy);
  }

  /// <summary>Round-trips <paramref name="message"/> through the real serialiser, records it and
  /// invokes the client-side handler, if one is registered.</summary>
  private void DeliverToClient<T>(T message) {
    RequireRegistered(typeof(T));
    T copy = SerializerUtil.Deserialize<T>(SerializerUtil.Serialize(message));
    _sentToClients.Add(copy!);
    if (_clientHandlers.TryGetValue(typeof(T), out Delegate? handler))
      ((NetworkServerMessageHandler<T>)handler)(copy);
  }

  /// <summary>The server side of the pair: sends land in <see cref="SentToClients"/> and dispatch to a
  /// <see cref="ClientChannel"/> handler; received packets are whatever the paired
  /// <see cref="ClientChannel"/> sent.</summary>
  private sealed class ServerChannel(TestChannels hub) : IServerNetworkChannel {
    public string ChannelName => hub.ChannelName;

    public IServerNetworkChannel RegisterMessageType(Type type) {
      hub.Register(type);
      return this;
    }

    public IServerNetworkChannel RegisterMessageType<T>() {
      hub.Register(typeof(T));
      return this;
    }

    public IServerNetworkChannel SetMessageHandler<T>(
      NetworkClientMessageHandler<T> messageHandler
    ) {
      hub._serverHandlers[typeof(T)] = messageHandler;
      return this;
    }

    public void SendPacket<T>(T message, params IServerPlayer[] players) {
      foreach (IServerPlayer player in players)
        if (player == hub.Sender)
          hub.DeliverToClient(message);
    }

    public void SendPacket<T>(T message, byte[] data, params IServerPlayer[] players) =>
      SendPacket(message, players);

    public void BroadcastPacket<T>(T message, params IServerPlayer[] exceptPlayers) {
      if (Array.IndexOf(exceptPlayers, hub.Sender) < 0)
        hub.DeliverToClient(message);
    }

    INetworkChannel INetworkChannel.RegisterMessageType(Type type) => RegisterMessageType(type);

    INetworkChannel INetworkChannel.RegisterMessageType<T>() => RegisterMessageType<T>();
  }

  /// <summary>The client side of the pair: sends land in <see cref="SentToServer"/> and dispatch to a
  /// <see cref="ServerChannel"/> handler as if from <see cref="Sender"/>; received packets are
  /// whatever the paired <see cref="ServerChannel"/> sent.</summary>
  private sealed class ClientChannel(TestChannels hub) : IClientNetworkChannel {
    public string ChannelName => hub.ChannelName;

    /// <summary>Always true: this pair never models a disconnected client.</summary>
    public bool Connected => true;

    public IClientNetworkChannel RegisterMessageType(Type type) {
      hub.Register(type);
      return this;
    }

    public IClientNetworkChannel RegisterMessageType<T>() {
      hub.Register(typeof(T));
      return this;
    }

    public IClientNetworkChannel SetMessageHandler<T>(NetworkServerMessageHandler<T> handler) {
      hub._clientHandlers[typeof(T)] = handler;
      return this;
    }

    public void SendPacket<T>(T message) => hub.DeliverToServer(message);

    INetworkChannel INetworkChannel.RegisterMessageType(Type type) => RegisterMessageType(type);

    INetworkChannel INetworkChannel.RegisterMessageType<T>() => RegisterMessageType<T>();
  }
}
