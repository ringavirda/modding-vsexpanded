using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace HelloModule;

/// <summary>
/// Sends the player a greeting on interact - the whole point of the module. Any mod's block picks
/// this up with <c>.Behavior&lt;BlockBehaviorGreeter&gt;()</c>, which resolves to
/// <c>hellomodule.BlockBehaviorGreeter</c> through this assembly's own <c>[assembly: ExDomain]</c>
/// rather than the calling block's - the cross-assembly key path this sample exists to prove.
/// Handling stays <see cref="EnumHandling.PassThrough"/>, so the block's own click handler still runs.
/// </summary>
[BlockBehaviorRegister]
public class BlockBehaviorGreeter : BlockBehavior {
  public BlockBehaviorGreeter(Block block)
    : base(block) { }

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref EnumHandling handling
  ) {
    handling = EnumHandling.PassThrough;

    if (
      ExInteraction.Of(world, byPlayer, blockSel).IsServer
      && byPlayer is IServerPlayer serverPlayer
    )
      Greet(serverPlayer);

    return false;
  }

  private static void Greet(IServerPlayer player) {
    foreach (
      GreetingDef greeting in Greetings.All.Take(HelloModuleValues.GreetingsPerClick)
    )
      player.SendMessage(
        GlobalConstants.GeneralChatGroup,
        greeting.Text,
        EnumChatType.Notification
      );
  }
}
