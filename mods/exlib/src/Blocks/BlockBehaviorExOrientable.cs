using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks;

/// <summary>
/// Places a block wearing the <c>side</c> variant the player's look implies; the family's replacement
/// for vanilla's <c>HorizontalOrientable</c>. Resolves through <c>CodeWithVariant("side", …)</c> rather
/// than <c>CodeWithParts</c>, which keeps only the first dash-segment and so cannot address a code with
/// several segments or variant groups. The facing is
/// <c>SuggestedHVOrientation(byPlayer, blockSel)[0]</c>, as in vanilla.
/// <para>
/// Declared as <c>{ "name": "ExOrientable" }</c>, with <c>"properties": { "mode": "omni" }</c> for a
/// block that may also point up or down and <c>{ "mode": "network", "scheme": "Axis" }</c> for one the
/// network orients - which every <c>BlockNetworkNode</c> def declares through
/// <c>ExBlockDef.NetworkOriented</c>, so the scheme is read off the block's own states and there is no
/// name left to misspell. See docs/design/mechanics/orientation-schemes.md.
/// </para>
/// </summary>
[BlockBehaviorRegister("ExOrientable", PrefixModId = false)]
public class BlockBehaviorExOrientable : BlockBehavior {
  /// <summary>The variant group a player-oriented block writes its facing to.</summary>
  public const string SideVariant = "side";

  /// <summary>The variant group a network-oriented block writes its token to. Kept distinct from
  /// <see cref="SideVariant"/> because a multi-face token (<c>nswe</c>, <c>uns</c>) is not a side.
  /// </summary>
  public const string OrientationVariant = "orientation";

  private ExOrientationScheme _scheme = ExOrientations.Face;

  public BlockBehaviorExOrientable(Block block)
    : base(block) { }

  /// <summary>
  /// Whether this block also accepts the vertical faces. Horizontal (the default) is the four-token
  /// <see cref="ExOrientations.Face"/>; omni is the six-token <see cref="ExOrientations.FaceAll"/>.
  /// </summary>
  public bool IsOmni { get; private set; }

  /// <summary>Whether the network decides this block's orientation rather than the player. Such a
  /// block re-picks its token whenever a neighbour changes, so a multiblock layout must not pin one -
  /// which is now unrepresentable: <c>MultiblockLayoutBuilder.Legend</c> refuses a multi-letter token
  /// outright, and a per-mod <c>PinnedNetworkNodes</c> check catches the single-letter cases the builder
  /// cannot tell from a player-oriented block's. A layout states what it wants with <c>Connector</c>
  /// instead.</summary>
  public bool IsNetworkOriented { get; private set; }

  /// <summary>The variant group this block's orientation lives in, by mode.</summary>
  public string VariantKey =>
    IsNetworkOriented ? OrientationVariant : SideVariant;

  /// <summary>The token vocabulary this block declares.</summary>
  public ExOrientationScheme Scheme => _scheme;

  /// <summary>The <c>scheme</c> name this block declared that names no scheme, or null when it named
  /// one or named none. A code-first def cannot get here - <c>ExBlockDef.NetworkOriented</c> writes the
  /// name it resolved off the block's own states - so this is the JSON-authored case, where nothing
  /// checks the spelling at build.</summary>
  public string? UnresolvedScheme { get; private set; }

  /// <inheritdoc/>
  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);

    // `mode` rather than a bare bool, so a further arrangement is additive.
    string mode = properties?["mode"].AsString("horizontal") ?? "horizontal";
    IsOmni = mode == "omni";
    IsNetworkOriented = mode == "network";

    if (IsNetworkOriented) {
      // The token set is not derivable from the mode: a straight pipe, a bend, a tee and a cross are
      // all "network" and declare different sets, so the scheme is named explicitly.
      string? schemeName = properties?["scheme"].AsString();
      ExOrientationScheme? named = ExOrientations.All.FirstOrDefault(s =>
        s.Name == schemeName
      );
      // A name that resolves to nothing falls back rather than throwing, so one misspelling in a JSON
      // asset does not take a world down - but Axis rejects every token outside [ns,we,ud], which on a
      // bend stops the node re-orienting with no exception and no log line. Recorded so a test can see
      // the difference between "named nothing" and "named something wrong".
      UnresolvedScheme = named == null ? schemeName : null;
      _scheme = named ?? ExOrientations.Axis;
    } else
      _scheme = IsOmni ? ExOrientations.FaceAll : ExOrientations.Face;
  }

  /// <summary>
  /// Swaps the block at <paramref name="pos"/> to the variant wearing <paramref name="token"/>,
  /// returning whether it did. The player path passes the look-derived token, the network path whatever
  /// its connector scan chose. Returns false when the token is outside the block's scheme or resolves
  /// to no other block.
  /// <para>
  /// Two of <c>BlockNetworkNode</c>'s four orientation-rewrite sites call this: <c>Rotate</c> and
  /// <c>RecalculateAndSyncOrientations</c>. The other two neither can nor should - <c>TryPlaceBlock</c>
  /// resolves a code before any block stands at the position, and <c>GetDrops</c> answers the def's own
  /// first-listed state, which is deliberately not the scheme's first token.
  /// </para>
  /// </summary>
  /// <remarks>The mesh update belongs here rather than at the call sites: every swap needs it, and a
  /// headless test cannot see a missing one - the block changes, every server-side assertion passes,
  /// and the player keeps looking at the old shape. <c>NetworkNodeOrientationTests</c> asserts the mark
  /// explicitly for that reason.</remarks>
  public bool ApplyOrientation(IWorldAccessor world, BlockPos pos, string token) {
    if (!_scheme.Contains(token))
      return false;

    Block? oriented = world.BlockAccessor.GetBlock(
      block.CodeWithVariant(VariantKey, token)
    );
    if (oriented == null || oriented.BlockId == block.BlockId)
      return false;

    world.BlockAccessor.ExchangeBlock(oriented.BlockId, pos);
    world.BlockAccessor.MarkBlockDirty(pos);
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
  ) {
    // A network block goes down in whatever variant the stack carries; its own connector scan
    // re-orients it on the next neighbour notification, so deciding a facing here would be overwritten.
    if (IsNetworkOriented)
      return true;

    handling = EnumHandling.PreventDefault;

    string token = TokenFor(byPlayer, blockSel);
    Block? oriented = world.BlockAccessor.GetBlock(
      block.CodeWithVariant(VariantKey, token)
    );

    // A block whose variant group is missing a state is an authoring mistake: refuse the placement and
    // log it rather than dereference the null.
    if (oriented == null) {
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

    world.BlockAccessor.SetBlock(
      oriented.BlockId,
      blockSel.Position,
      itemstack
    );
    return true;
  }

  /// <summary>
  /// The token the placement should wear. Horizontal blocks take the player's look direction; an omni
  /// block takes the selected face first, so pointing at a ceiling or floor gives <c>u</c>/<c>d</c> and
  /// anything else falls back to the same horizontal choice.
  /// </summary>
  private string TokenFor(IPlayer byPlayer, BlockSelection blockSel) {
    if (IsOmni && blockSel.Face is { IsVertical: true } face)
      return face == BlockFacing.UP ? "u" : "d";

    BlockFacing horizontal = Block.SuggestedHVOrientation(byPlayer, blockSel)[
      0
    ];
    return ExOrientation.TokenOf(horizontal, asLetter: true);
  }

  /// <inheritdoc/>
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    ref float dropChanceMultiplier,
    ref EnumHandling handling
  ) {
    // One item for every facing, so drops stack in the inventory and satisfy a grid recipe. The
    // canonical token is the scheme's first, so each mode answers its own vocabulary's base state.
    handling = EnumHandling.PreventDefault;
    return [CanonicalStack(world)];
  }

  /// <inheritdoc/>
  public override ItemStack OnPickBlock(
    IWorldAccessor world,
    BlockPos pos,
    ref EnumHandling handling
  ) {
    // The same canonical stack GetDrops returns, so a broken and a picked block stack together.
    handling = EnumHandling.PreventDefault;
    return CanonicalStack(world);
  }

  /// <summary>The base-token stack for this block, returned by both the drop and the middle-click so
  /// the two stack together in the inventory and in a grid recipe.</summary>
  /// <remarks>The scheme's first token, which for a network node is not the same answer as its own
  /// first-listed state - a tuyere declares <c>[s,n,w,e]</c> and drops <c>-s</c> where
  /// <see cref="ExOrientations.Face"/> starts at <c>n</c>. <c>BlockNetworkNode</c> overrides both drop
  /// and pick without calling base, so this never runs for a node; pointing either of its overrides
  /// here would silently change what four shipped blocks drop.</remarks>
  public ItemStack CanonicalStack(IWorldAccessor world) {
    Block canonical =
      world.BlockAccessor.GetBlock(
        block.CodeWithVariant(VariantKey, _scheme.Tokens[0])
      ) ?? block;
    return new ItemStack(canonical);
  }
}
