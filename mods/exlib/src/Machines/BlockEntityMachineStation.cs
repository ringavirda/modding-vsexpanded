using System.Collections.Generic;
using ExpandedLib.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ExpandedLib.Machines;

/// <summary>
/// Base block entity for a machine the player works through a window: a container whose slots are
/// declared as <see cref="MachineSlotSpec"/>s, plus the open/close handshake that keeps the server
/// inventory in step with what the player sees.
/// <para>
/// <see cref="BlockEntityContainer"/> routes none of the window packets itself, so every machine that
/// opens a dialog re-implements the same handshake, the same claim check and the same dialog disposal.
/// This carries all three once. A machine adds its own actions by overriding
/// <see cref="OnStationPacket"/>, whose ids start at <see cref="FirstMachinePacketId"/>.
/// </para>
/// </summary>
public abstract class BlockEntityMachineStation : BlockEntityContainer {
  // 1000/1001 are the vanilla openable-container open/close literals, and anything below 1000 is a
  // slot move the inventory's own network util handles. A machine's own actions start above them.
  private const int PacketIdOpen = 1000;
  private const int PacketIdClose = 1001;

  /// <summary>First packet id free for a machine's own actions. Ids below this are the container
  /// protocol.</summary>
  public const int FirstMachinePacketId = 1002;

  private MachineStationInventory? _inventory;
  private ExBlockState? _state;

  /// <summary>The slots this machine offers, in window order. Read once, when the inventory is first
  /// built.</summary>
  protected abstract MachineSlotSpec[] SlotSpecs { get; }

  /// <summary>This station's declared fields, built on first use.</summary>
  protected ExBlockState Persisted =>
    BlockEntityStateHost.GetOrCreate(this, ref _state, DeclareState);

  /// <summary>Declares the fields this station persists beyond its inventory. Called once, lazily.
  /// Default: nothing.</summary>
  protected virtual void DeclareState(ExBlockState state) { }

  public override InventoryBase Inventory =>
    _inventory ??= new MachineStationInventory(SlotSpecs);

  // The open window. Client-side only; null when closed.
  private GuiDialogBlockEntity? _dialog;

  /// <summary>Whether this machine's window is open on this client.</summary>
  protected bool WindowOpen => _dialog != null;

  /// <summary>
  /// Builds this machine's window. Client-side; called on each open, so it may read whatever state
  /// the window should open on. Returning null leaves the machine windowless - a station whose
  /// slots exist but whose interactions are physical, which is what the rolling mill is until the
  /// machining line gives it a face.
  /// </summary>
  protected virtual GuiDialogBlockEntity? CreateDialog(ICoreClientAPI capi) =>
    null;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    Inventory.LateInitialize($"{InventoryClassName}-{Pos}", api);
  }

  #region Declared state

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    Persisted.ToTree(tree);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    Persisted.FromTree(tree, worldForResolving);
  }

  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
    base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
    Persisted.StoreCollectibleMappings(Api.World, blockIdMapping, itemIdMapping);
  }

  public override void OnLoadCollectibleMappings(
    IWorldAccessor worldForResolve,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping,
    int schematicSeed,
    bool resolveImports
  ) {
    base.OnLoadCollectibleMappings(
      worldForResolve,
      oldBlockIdMapping,
      oldItemIdMapping,
      schematicSeed,
      resolveImports
    );
    Persisted.LoadCollectibleMappings(
      worldForResolve,
      oldBlockIdMapping,
      oldItemIdMapping
    );
  }

  #endregion

  #region The window

  /// <summary>Right-click entry point: toggles the window. Does nothing on the server, so a caller
  /// need not test the side itself.</summary>
  public void ToggleWindow(IPlayer byPlayer) {
    if (Api.Side == EnumAppSide.Client)
      ToggleDialog((ICoreClientAPI)Api, byPlayer);
  }

  private void ToggleDialog(ICoreClientAPI capi, IPlayer byPlayer) {
    if (_dialog != null) {
      _dialog.TryClose();
      return;
    }

    // A windowless station opens nothing rather than opening an empty frame, and must not send the
    // open packet either: the server would put the player into an inventory they cannot see or close.
    _dialog = CreateDialog(capi);
    if (_dialog == null)
      return;

    _dialog.OnClosed += () => {
      _dialog = null;
      capi.Network.SendBlockEntityPacket(Pos, PacketIdClose);
    };
    _dialog.TryOpen();

    capi.Network.SendPacketClient(Inventory.Open(byPlayer));
    capi.Network.SendBlockEntityPacket(Pos, PacketIdOpen);
  }

  /// <summary>Closes and disposes the window, so a broken or unloaded machine cannot leave its GUI
  /// bound to a dead block entity. Mirrors vanilla's <c>BEOpenableContainer.Dispose</c>.</summary>
  protected virtual void CloseWindow() {
    if (_dialog?.IsOpened() == true)
      _dialog.TryClose();
    _dialog?.Dispose();
    _dialog = null;
  }

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    CloseWindow();
  }

  public override void OnBlockUnloaded() {
    base.OnBlockUnloaded();
    CloseWindow();
  }

  #endregion

  #region Packet handshake

  /// <summary>
  /// Server-side routing for the window packets: the container protocol, the access check, then this
  /// machine's own actions. Sealed - a machine extends it through <see cref="OnStationPacket"/>, so
  /// no override can drop the access check by forgetting to call base.
  /// </summary>
  public sealed override void OnReceivedClientPacket(
    IPlayer player,
    int packetid,
    byte[] data
  ) {
    // Closing needs no check at all: a player who has walked out of a claim, or out of reach, must
    // still be able to shut the window they already have open. Vanilla closes unconditionally too.
    if (packetid == PacketIdClose) {
      player.InventoryManager?.CloseInventory(Inventory);
      return;
    }

    if (!MayUse(player)) {
      // A refused slot move leaves the client's open window showing the item where the server says it
      // is not, and the two disagree until the player reopens it. Vanilla answers a rejected container
      // packet with a rollback rather than a bare return.
      if (packetid < PacketIdOpen && player is IServerPlayer serverPlayer)
        SendRollback(serverPlayer, packetid, data);
      return;
    }

    if (packetid < PacketIdOpen) {
      Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);
      // Vanilla's comment on the same call: "Tell server to save this chunk to disk again". A slot move
      // that is not followed by some other write is otherwise lost on the next server restart.
      Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
      return;
    }

    if (packetid == PacketIdOpen) {
      player.InventoryManager?.OpenInventory(Inventory);
      return;
    }

    if (OnStationPacket(player, packetid, data))
      return;

    base.OnReceivedClientPacket(player, packetid, data);
  }

  /// <summary>
  /// This machine's own window actions, run server-side after the access check. Ids start at
  /// <see cref="FirstMachinePacketId"/>. Return true when the packet was handled; false lets the
  /// container base see it.
  /// </summary>
  protected virtual bool OnStationPacket(
    IPlayer player,
    int packetid,
    byte[] data
  ) => false;

  #endregion

  #region Access

  // Whether the engine's interaction-range test runs as part of the access check. Internal and not part
  // of exlib's API: the only thing that turns it off is a headless test, whose player is a substitute
  // the engine will never place in range, and which would otherwise be unable to exercise any packet
  // route at all. The claim check is not behind it and cannot be switched off.
  internal virtual bool ValidatePickRange => true;

  /// <summary>
  /// Whether <paramref name="player"/> may act on this machine: claim access, and on 1.22 and later
  /// the engine's own interaction-range test as well. Without the range test a client can move slots
  /// in any unclaimed station in any loaded chunk from arbitrary distance.
  /// </summary>
  private bool MayUse(IPlayer player) {
#if GAME_GE_1_22
    // CachedAccessPerms is the only public way to the range test - the block-position overload of
    // IPlayer.IsInInteractionRangeOf that it calls is internal to the engine. It also runs the claim
    // check and audits either failure, so it replaces the hand-written pair outright.
#pragma warning disable CS0618 // The ctor is marked obsolete ahead of a 1.23 signature change. Vanilla's
    // own BEOpenableContainer calls it exactly like this, and there is no other entry point; when 1.23
    // lands this gains a GAME_GE_1_23 branch like every other threshold in the tree.
    var perms = new CachedAccessPerms(Api.World, Pos, player);
#pragma warning restore CS0618
    return perms.IsInteractingPlayerAllowedTo(
      EnumBlockAccessFlags.Use,
      ValidatePickRange,
      "machine station"
    );
#else
    // 1.20/1.21 have no public reach test. Hand-rolling one would measure a different notion of reach
    // than the server uses and reject legitimate interactions, so those builds keep the claim check
    // alone - the same protection they had before.
    if (Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.Use))
      return true;

    Api.World.Logger.Audit(
      "Player {0} sent a machine-station packet to {1} without claim access. Rejected.",
      player.PlayerName,
      Pos
    );
    return false;
#endif
  }

  // Rolls the client's view of the inventory back to the server's after a refused slot move.
  // SendInventoryRollback arrived in 1.22; on the legacy builds the client corrects itself when the
  // window is reopened, which is the behaviour those versions have always had.
  private void SendRollback(IServerPlayer player, int packetid, byte[] data) {
#if GAME_GE_1_22
    Inventory.InvNetworkUtil.SendInventoryRollback(player, packetid, data);
#endif
  }

  #endregion
}
