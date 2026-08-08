using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Behaviors;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Tests;

/// <summary>
/// A world holding one block per declared orientation state, plus a player who can be stood on any side
/// of the placement cell - what <see cref="BlockBehaviorExOrientable"/> needs and nothing else. The
/// behaviour is driven directly rather than through <c>Block.TryPlaceBlock</c>, whose pipeline wants an
/// inventory, a selection ray and an item-stack round-trip the behaviour never reads; each call below
/// passes the arguments the engine passes.
/// </summary>
public sealed class ExOrientableRig {
  /// <summary>Where the block goes. Away from the origin so a sign error in the look math cannot
  /// coincide with the right answer.</summary>
  public static readonly BlockPos Pos = new(64, 16, 64, 0);

  private readonly Block _placer;
  private readonly BlockBehaviorExOrientable _behaviour;
  private readonly string _variantKey;

  private ExOrientableRig(
    TestWorld world,
    Block placer,
    BlockBehaviorExOrientable behaviour,
    string variantKey
  ) {
    World = world;
    _placer = placer;
    _behaviour = behaviour;
    _variantKey = variantKey;
  }

  public TestWorld World { get; }

  /// <summary>The code of whatever now stands at <see cref="Pos"/>, or null for an empty cell.</summary>
  public string? PlacedCode {
    get {
      Block at = World.GetBlock(Pos);
      return at.BlockId == 0 ? null : at.Code?.ToString();
    }
  }

  /// <summary>Refusal code the last <see cref="PlaceLooking"/> handed back, or null if it placed.</summary>
  public string? FailureCode { get; private set; }

  /// <summary>Every <c>Logger.Error</c> the behaviour emitted, already formatted.</summary>
  public List<string> LoggedErrors { get; } = [];

  #region Building one

  /// <summary>
  /// A block declaring <paramref name="states"/> in the <paramref name="variantKey"/> group, one
  /// registered <see cref="Block"/> per state, all sharing one behaviour instance's configuration.
  /// </summary>
  /// <param name="fixedGroups">Variant groups that precede the orientation one and never move. Pass
  /// at least one to exercise a multi-segment code.</param>
  public static ExOrientableRig WithVariants(
    string baseCode,
    string variantKey,
    string[] states,
    string mode = "horizontal",
    string? scheme = null,
    params (string key, string value)[] fixedGroups
  ) {
    var world = new TestWorld();
    var loc = new AssetLocation(baseCode);

    Block Build(string state, int id) {
      // CodeWithVariant walks Variant in insertion order to rebuild the path, so the orientation
      // group must be declared last, as the definition DSL renders it.
      (string, string)[] variants =
      [
        .. fixedGroups.Select(g => (g.key, g.value)),
        (variantKey, state),
      ];

      string path =
        loc.Path + string.Concat(variants.Select(v => "-" + v.Item2));

      return TestBlocks.Configure(
        new Block(),
        $"{loc.Domain}:{path}",
        id,
        variants
      );
    }

    Block? placer = null;
    for (int i = 0; i < states.Length; i++) {
      Block b = Build(states[i], i + 1);
      world.Register(b);
      placer ??= b;
    }

    var properties = new JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse(
        scheme == null
          ? $$"""{"mode":"{{mode}}"}"""
          : $$"""{"mode":"{{mode}}","scheme":"{{scheme}}"}"""
      )
    );

    var behaviour = new BlockBehaviorExOrientable(placer!);
    behaviour.Initialize(properties);
    placer!.BlockBehaviors = [behaviour];

    return new ExOrientableRig(world, placer, behaviour, variantKey);
  }

  #endregion

  #region Driving it

  /// <summary>
  /// Places the block with the player looking <paramref name="lookDirection"/>, standing on the
  /// opposite side of the cell. The placed block wears <paramref name="lookDirection"/> and so faces
  /// away from the player, which is vanilla's convention (<c>SuggestedHVOrientation[0]</c> takes the
  /// atan2 of eye-minus-hit and adds a quarter turn) and the basis of every multiblock angle here.
  /// </summary>
  /// <param name="selectedFace">Face of the neighbouring block the player clicked. Defaults to
  /// <c>up</c>, placing on the ground. Not inert on an omni block: a vertical selected face is what
  /// makes one point up or down, so an omni case left at the default never reaches the horizontal
  /// branch.</param>
  public bool PlaceLooking(string lookDirection, string selectedFace = "up") {
    BlockFacing look =
      BlockFacing.FromCode(lookDirection)
      ?? throw new ArgumentException(
        $"'{lookDirection}' is not a facing code",
        nameof(lookDirection)
      );
    BlockFacing face =
      BlockFacing.FromCode(selectedFace)
      ?? throw new ArgumentException(
        $"'{selectedFace}' is not a facing code",
        nameof(selectedFace)
      );

    // Stand 5 blocks back along the look axis, on the far side of the cell from the look direction.
    IPlayer player = PlayerAt(
      Pos.X + 0.5 - (look.Normali.X * 5),
      Pos.Y + 0.5 - (look.Normali.Y * 5),
      Pos.Z + 0.5 - (look.Normali.Z * 5)
    );

    var selection = new BlockSelection {
      Position = Pos.Copy(),
      Face = face,
      HitPosition = new Vec3d(0.5, 0.5, 0.5),
      DidOffset = false,
    };

    var handling = EnumHandling.PassThrough;
    string failure = "";
    bool placed = _behaviour.TryPlaceBlock(
      World.World,
      player,
      new ItemStack(_placer),
      selection,
      ref handling,
      ref failure
    );

    FailureCode = placed ? null : failure;
    CaptureLoggedErrors();
    return placed;
  }

  /// <summary>What breaking the placed block hands back.</summary>
  public string? DropCode {
    get {
      float multiplier = 1f;
      var handling = EnumHandling.PassThrough;
      return _behaviour
        .GetDrops(World.World, Pos, null!, ref multiplier, ref handling)
        ?.FirstOrDefault()
        ?.Collectible?.Code?.ToString();
    }
  }

  /// <summary>What middle-clicking the placed block hands back.</summary>
  public string? PickCode {
    get {
      var handling = EnumHandling.PassThrough;
      return _behaviour
        .OnPickBlock(World.World, Pos, ref handling)
        ?.Collectible?.Code?.ToString();
    }
  }

  /// <summary>Drives the network route: apply <paramref name="token"/> to whatever stands at
  /// <see cref="Pos"/>, returning whether the block was swapped.</summary>
  public bool ApplyOrientation(string token) =>
    World
      .GetBlock(Pos)
      .GetBehavior<BlockBehaviorExOrientable>()
      ?.ApplyOrientation(World.World, Pos, token)
    ?? _behaviour.ApplyOrientation(World.World, Pos, token);

  /// <summary>The variant group this rig's blocks carry their orientation in.</summary>
  public string VariantKey => _variantKey;

  #endregion

  #region Wiring

  private static IPlayer PlayerAt(double x, double y, double z) {
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.Pos.SetPos(x, y, z);
    // Zeroed rather than the real ~1.7 m: SuggestedHVOrientation adds LocalEyePos to the entity
    // position before taking the angle, so eye height tilts the vertical component only and zeroing
    // it keeps the horizontal arithmetic exact.
    entity.LocalEyePos = new Vec3d(0, 0, 0);
    player.Entity.Returns(entity);
    return player;
  }

  private void CaptureLoggedErrors() {
    LoggedErrors.Clear();
    foreach (
      var call in World
        .World.Logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Error))
    ) {
      object?[] args = call.GetArguments();
      if (args.Length == 0 || args[0] is not string format)
        continue;
      object?[] rest =
        args.Length > 1 && args[1] is object?[] varargs
          ? varargs
          : [.. args.Skip(1)];
      LoggedErrors.Add(rest.Length == 0 ? format : string.Format(format, rest));
    }
  }

  #endregion
}
