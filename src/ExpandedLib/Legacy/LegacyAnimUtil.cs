// Legacy shim for BlockEntityAnimationUtil.CreateMesh. The 1.22 overload takes a
// TesselationMetaData metaOverride so callers can pass per-call SelectiveElements; the 1.20/1.21
// CreateMesh has no such parameter and reads SelectiveElements off the block's CompositeShape.
// This reproduces the 5-arg signature: it resolves the block shape and tesselates with the override
// applied per field (`metaOverride?.X ?? block.Shape.X`), mutating no shared state.
#if !GAME_GE_1_22
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ExpandedLib.Legacy;

public static class LegacyAnimUtil {
  private static readonly FieldInfo BeField =
    typeof(BlockEntityAnimationUtil).GetField(
      "be",
      BindingFlags.NonPublic | BindingFlags.Instance
    )!;

  extension(BlockEntityAnimationUtil util) {
    public MeshData CreateMesh(
      string nameForLogging,
      Shape? shape,
      out Shape resultingShape,
      ITexPositionSource? texSource,
      TesselationMetaData? metaOverride
    ) {
      var be = (BlockEntity)BeField.GetValue(util)!;
      var api = be.Api;
      var capi = (ICoreClientAPI)api;
      Block block = api.World.BlockAccessor.GetBlock(be.Pos);

      texSource ??= capi.Tesselator.GetTextureSource(
        block,
        0,
        returnNullWhenMissing: false
      );

      if (shape == null) {
        AssetLocation loc = block
          .Shape.Base.Clone()
          .WithPathPrefixOnce("shapes/")
          .WithPathAppendixOnce(".json");
        shape = Shape.TryGet(api, loc);
        if (shape == null) {
          api.World.Logger.Error(
            "Shape for block {0} not found at {1}; block animations not loaded.",
            block.Code,
            loc
          );
          resultingShape = null!;
          return new MeshData(initialiseArrays: true);
        }
      }

      // Resolve through the API present on the legacy floor (1.20.0 / 1.21.0): void
      // ResolveReferences plus the params-only ResolveAndFindJoints. The Dictionary-returning
      // CollectAndResolveReferences / ResolveAndFindJoints overloads exist only on 1.21 and later
      // 1.20.x patches, so they would break on a clean 1.20.0.
      shape.ResolveReferences(api.World.Logger, nameForLogging);
      shape.CacheInvTransforms();
      shape.ResolveAndFindJoints(
        api.World.Logger,
        nameForLogging,
        Array.Empty<string>()
      );

      var meta = new TesselationMetaData {
        QuantityElements =
          metaOverride?.QuantityElements ?? block.Shape.QuantityElements,
        SelectiveElements =
          metaOverride?.SelectiveElements ?? block.Shape.SelectiveElements,
        IgnoreElements =
          metaOverride?.IgnoreElements ?? block.Shape.IgnoreElements,
        TexSource = texSource,
        WithJointIds = true,
        WithDamageEffect = true,
        TypeForLogging = nameForLogging,
      };

      capi.Tesselator.TesselateShape(meta, shape, out MeshData mesh);
      util.OnAfterTesselate?.Invoke(mesh);
      resultingShape = shape;
      return mesh;
    }
  }
}
#endif
