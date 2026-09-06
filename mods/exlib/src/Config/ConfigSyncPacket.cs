using ProtoBuf;

namespace ExpandedLib.Config;

/// <summary>One mod's config section, carried server to client by <c>ExConfigSyncModSystem</c>: the
/// host's <see cref="IExConfigAccess.ExportJson"/> output, addressed by mod id so the receiving side
/// can find the matching registered store.</summary>
[ProtoContract]
public sealed class ConfigSyncPacket {
  /// <summary>The section's owning mod id (see <see cref="IExConfigAccess.ModId"/>).</summary>
  [ProtoMember(1)]
  public string ModId = string.Empty;

  /// <summary>The config file this section belongs to, for logging (see
  /// <see cref="IExConfigAccess.FileName"/>).</summary>
  [ProtoMember(2)]
  public string FileName = string.Empty;

  /// <summary>The section's live values, as <see cref="IExConfigAccess.ExportJson"/> produced them.</summary>
  [ProtoMember(3)]
  public string Json = string.Empty;
}
