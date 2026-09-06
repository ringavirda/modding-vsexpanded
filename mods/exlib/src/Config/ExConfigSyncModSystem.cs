using System.ComponentModel;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Config;

/// <summary>
/// Carries every manageable config's live values from the host to each joining client, and any later
/// live edit, so a client's display, handbook and predictions read the server's tunables instead of
/// whatever its own local <c>ex_values.json</c> holds. Moves one <see cref="ConfigSyncPacket"/> per
/// section registered with <see cref="ExConfigProfiles"/> over a dedicated channel; a mod carrying its
/// own transport can skip this and call <see cref="IExConfigAccess.ImportJson"/> directly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExConfigSyncModSystem : ModSystem {
  private const string ChannelName = "exlibConfigSync";

  #region Server
  private IServerNetworkChannel? _serverChannel;

  public override void StartServerSide(ICoreServerAPI api) {
    _serverChannel = api
      .Network.RegisterChannel(ChannelName)
      .RegisterMessageType<ConfigSyncPacket>();
    api.Event.PlayerJoin += SendAllSections;
  }

  /// <summary>Sends every registered section to one player, once their client is ready to receive it.</summary>
  private void SendAllSections(IServerPlayer player) {
    foreach (var code in ExConfigProfiles.Codes)
      if (ExConfigProfiles.TryGet(code, out var config))
        _serverChannel!.SendPacket(ToPacket(config), player);
  }

  /// <summary>Pushes one section's current values to every connected player. Called by
  /// <c>/exmod config set</c> after a successful edit, so a live change reaches players without
  /// requiring a reconnect.</summary>
  public void BroadcastSection(IExConfigAccess config) =>
    _serverChannel?.BroadcastPacket(ToPacket(config));

  private static ConfigSyncPacket ToPacket(IExConfigAccess config) =>
    new() {
      ModId = config.ModId,
      FileName = config.FileName,
      Json = config.ExportJson(),
    };
  #endregion

  #region Client
  public override void StartClientSide(ICoreClientAPI api) {
    api
      .Network.RegisterChannel(ChannelName)
      .RegisterMessageType<ConfigSyncPacket>()
      .SetMessageHandler<ConfigSyncPacket>(packet => HandlePacket(api, packet));
  }

  /// <summary>Imports one section received from the host into its matching registered store, or logs
  /// a warning and drops it if this side has no such section registered. The import never touches this
  /// client's own config file - it only replaces the live, in-memory values every reader goes through.
  /// Internal so the sync tests can drive it directly against a substituted client API.</summary>
  internal static void HandlePacket(ICoreClientAPI api, ConfigSyncPacket packet) {
    if (!ExConfigProfiles.TryGet(packet.ModId, out var config)) {
      api.Logger.Warning(
        "[exlib] config: unknown section '{0}' received from the server; ignored.",
        packet.ModId
      );
      return;
    }

    config.ImportJson(packet.Json);
    api.Logger.Notification(
      "[exlib] config: {0} section received from the server",
      packet.ModId
    );
  }
  #endregion
}
