using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Behaviors;

/// <summary>
/// Places a block wearing the <c>side</c> variant the player's look implies - the family's replacement
/// for vanilla's <c>HorizontalOrientable</c>.
/// <para>
/// <b>Why this exists rather than the vanilla behaviour.</b> Vanilla builds the rotated code with
/// <c>CodeWithParts(facing)</c>, which keeps only the <b>first</b> dash-segment of the path and replaces
/// everything after it. That works only for a block whose code is a single segment and whose sole
/// variant group is <c>side</c>. Ours are neither: <c>iwex:crafting-designtable-n</c> became
/// <c>iwex:crafting-w</c>, which matches no block, and vanilla dereferences the null - so <b>placing the
/// block crashed the client</b>. Every furnace part, both hoppers, the mixer, the bunker and both
/// casting blocks were affected. Resolving through <c>CodeWithVariant("side", …)</c> addresses the
/// variant by name and is indifferent to how many groups or dashes precede it.
/// </para>
/// <para>
/// The facing convention is unchanged from vanilla's: <c>side</c> is
/// <c>SuggestedHVOrientation(byPlayer, blockSel)[0]</c>, the same call vanilla makes. Nothing in the
/// multiblock angle math moves as a result of this swap.
/// </para>
/// <para>
/// Add via <c>{ "name": "ExOrientable" }</c>; <c>"properties": { "mode": "omni" }</c> for a block that
/// may also point up or down; <c>{ "mode": "network", "scheme": "Axis" }</c> for a block whose
/// orientation the <em>network</em> decides.
/// </para>
/// <para>
/// <b>Only <c>horizontal</c> is in production use.</b> <c>omni</c> and <c>network</c> are implemented
/// and covered by <c>ExOrientableTests</c>, but <b>no block in any of the five mods declares either</b> -
/// a block has to opt in before the mode does anything at all. Both are therefore load-bearing for the
/// design and inert in the world; treat a change to either as unexercised by the game.
/// </para>
/// <para>
/// <b>The three modes differ in who decides, and that is the distinction worth making explicit.</b>
/// <c>horizontal</c> and <c>omni</c> are the player's choice, fixed at placement. <c>network</c> is not
/// a choice at all: a pipe, junction or axle picks whichever declared token best matches the connectors
/// around it and <b>re-picks whenever a neighbour changes</b>. Both routes want the same
/// <em>mechanism</em> - resolve a token to a block code and swap - which is what
/// <see cref="ApplyOrientation"/> is; only the source of the token differs.
/// </para>
/// <para>
/// <b><see cref="IsNetworkOriented"/> is the machine-readable answer to "may a multiblock layout pin
/// this block?" - and the answer is no.</b> A layout that pins a self-orienting node is a trap with two
/// failure modes: the structure can never be completed (the node takes its orientation from the
/// network, so placing it "the other way round" does not stick), or a complete structure silently comes
/// apart later - the player plumbs something unrelated nearby, the node re-orients, the cell stops
/// matching, and for a furnace incomplete means <em>extinguish</em>. The rule is stated in
/// <c>docs/design/mechanics/orientation-schemes.md</c>; declaring the mode is meant to let
/// <c>MultiblockLayoutBuilder</c> refuse such a layout at build time.
/// </para>
/// <para>
/// <b>Caution: that check is not built yet.</b> <c>MultiblockLayoutBuilder</c> has no such check,
/// and no block declares <c>mode: "network"</c>, so <see cref="IsNetworkOriented"/> is never true and the
/// rule above is still enforced by nothing. A layout can pin a self-orienting node today exactly
/// as it always could; until both halves land, the paragraph above describes the intent, not the
/// guarantee.
/// </para>
/// </summary>
[BlockBehaviorRegister("ExOrientable", PrefixModId = false)]
public class BlockBehaviorExOrientable : BlockBehavior
{
  /// <summary>The variant group a player-oriented block writes its facing to.</summary>
  public const string SideVariant = "side";

  /// <summary>The variant group a network-oriented block writes its token to.
  /// <para>
  /// Deliberately still a different key from <see cref="SideVariant"/>, even though the mechanism is
  /// now shared. A multi-face token (<c>nswe</c>, <c>uns</c>) is not a "side" in any sense a reader
  /// would accept, and collapsing the two keys is a second mass code change with none of the benefit -
  /// the single source of truth this class provides is the <em>mechanism</em> and the
  /// <see cref="ExOrientations"/> vocabulary, neither of which needs one key name.
  /// </para></summary>
  public const string OrientationVariant = "orientation";

  private ExOrientationScheme _scheme = ExOrientations.Face;

  public BlockBehaviorExOrientable(Block block)
    : base(block) { }

  /// <summary>
  /// Whether this block also accepts the vertical faces. Horizontal (the default) is the four-token
  /// <see cref="ExOrientations.Face"/>; omni is the six-token <see cref="ExOrientations.FaceAll"/>.
  /// </summary>
  public bool IsOmni { get; private set; }

  /// <summary>Whether the network decides this block's orientation rather than the player. See the
  /// remarks on this class for why nothing may pin such a block in a multiblock layout.</summary>
  public bool IsNetworkOriented { get; private set; }

  /// <summary>The variant group this block's orientation lives in, by mode.</summary>
  public string VariantKey => IsNetworkOriented ? OrientationVariant : SideVariant;

  /// <summary>The token vocabulary this block declares.</summary>
  public ExOrientationScheme Scheme => _scheme;

  /// <inheritdoc/>
  public override void Initialize(JsonObject properties)
  {
    base.Initialize(properties);

    // `mode` rather than a bare bool, so a third arrangement is additive rather than a breaking
    // rename - which is exactly what `network` turned out to be.
    string mode = properties?["mode"].AsString("horizontal") ?? "horizontal";
    IsOmni = mode == "omni";
    IsNetworkOriented = mode == "network";

    if (IsNetworkOriented)
    {
      // A network block's vocabulary is not derivable from the mode - a straight pipe, a bend, a tee
      // and a cross are all "network" and declare four different token sets - so it must be named.
      string? schemeName = properties?["scheme"].AsString();
      _scheme =
        ExOrientations.All.FirstOrDefault(s => s.Name == schemeName)
        ?? ExOrientations.Axis;
    }
    else
      _scheme = IsOmni ? ExOrientations.FaceAll : ExOrientations.Face;
  }

  /// <summary>
  /// <b>The one mechanism, shared by both routes.</b> Swaps the block at <paramref name="pos"/> to the
  /// variant wearing <paramref name="token"/>, returning whether it did. The player path calls it with
  /// the look-derived token; the network path calls it with whatever its connector scan chose.
  /// <para>
  /// <b>Intended, not yet wired.</b> The design is that this lets <c>BlockNetworkNode</c> stop
  /// rewriting codes itself and go back to being about the connectivity graph - it decides the token,
  /// this applies it. Today <c>BlockNetworkNode</c> still does its own <c>CodeWithVariant</c> +
  /// <c>ExchangeBlock</c> at <b>four</b> sites, and <b>no production code calls this method at all</b>;
  /// its only callers are <c>ExOrientableTests</c>.
  /// </para>
  /// </summary>
  public bool ApplyOrientation(IWorldAccessor world, BlockPos pos, string token)
  {
    if (!_scheme.Contains(token))
      return false;

    Block? oriented = world.BlockAccessor.GetBlock(
      block.CodeWithVariant(VariantKey, token)
    );
    if (oriented == null || oriented.BlockId == block.BlockId)
      return false;

    world.BlockAccessor.ExchangeBlock(oriented.BlockId, pos);
    return true;
  }

  /// <inheritdoc/>
  public override bool TryPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    ItemStack itemstack,
    BlockSelection blockSel,
    ref EnumHandling handling,
    ref string failureCode
  )
  {
    // A network block's placement is not ours to decide. It goes down in whatever variant the stack
    // carries and the node's own connector scan re-orients it on the next neighbour notification, so
    // taking over here would fight that and produce a placement the network immediately overwrites.
    if (IsNetworkOriented)
      return true;

    handling = EnumHandling.PreventDefault;

    string token = TokenFor(byPlayer, blockSel);
    Block? oriented = world.BlockAccessor.GetBlock(
      block.CodeWithVariant(VariantKey, token)
    );

    // Reported, not thrown. Vanilla's equivalent dereferenced the null and took the client down with
    // it; a block whose variant group is missing a state is an authoring mistake, and the player should
    // get a refusal and a log line rather than a crash report.
    if (oriented == null)
    {
      world.Logger.Error(
        "[exlib] {0} declares ExOrientable but has no '{1}' state '{2}' - "
          + "placement refused. Declare the variant group from ExOrientations.{3}.",
        block.Code,
        VariantKey,
        token,
        _scheme.Name
      );
      failureCode = "cantplace";
      return false;
    }

    world.BlockAccessor.SetBlock(oriented.BlockId, blockSel.Position, itemstack);
    return true;
  }

  /// <summary>
  /// The token the placement should wear. Horizontal blocks take the player's look direction; an omni
  /// block takes the selected face first, so pointing at a ceiling or floor gives <c>u</c>/<c>d</c> and
  /// anything else falls back to the same horizontal choice.
  /// </summary>
  private string TokenFor(IPlayer byPlayer, BlockSelection blockSel)
  {
    if (IsOmni && blockSel.Face is { IsVertical: true } face)
      return face == BlockFacing.UP ? "u" : "d";

    BlockFacing horizontal = Block.SuggestedHVOrientation(byPlayer, blockSel)[0];
    return ExOrientation.TokenOf(horizontal, asLetter: true);
  }

  /// <inheritdoc/>
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    ref float dropChanceMultiplier,
    ref EnumHandling handling
  )
  {
    // One item for every facing, so a stack of them is fungible in the inventory and in a grid recipe.
    // The canonical drop is the scheme's first token rather than a hard-coded "n", so an omni block,
    // a horizontal one and a network one all answer their own vocabulary's base state.
    handling = EnumHandling.PreventDefault;
    return [CanonicalStack(world)];
  }

  /// <inheritdoc/>
  public override ItemStack OnPickBlock(
    IWorldAccessor world,
    BlockPos pos,
    ref EnumHandling handling
  )
  {
    // The same canonical stack GetDrops hands back. Without this the two disagreed: breaking a
    // west-facing block gave the north variant and middle-clicking it gave the west one, so the two
    // would not stack in the inventory and only one of them satisfied a grid recipe.
    handling = EnumHandling.PreventDefault;
    return CanonicalStack(world);
  }

  /// <summary>The canonical (base-token) stack for this block - what a drop and a middle-click should
  /// both hand back, so a stack of them is fungible in the inventory and in a grid recipe.</summary>
  public ItemStack CanonicalStack(IWorldAccessor world)
  {
    Block canonical =
      world.BlockAccessor.GetBlock(
        block.CodeWithVariant(VariantKey, _scheme.Tokens[0])
      ) ?? block;
    return new ItemStack(canonical);
  }
}
