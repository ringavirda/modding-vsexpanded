// Legacy shim for BlockEntityAnimationUtil.CreateMesh. 1.22 added a TesselationMetaData
// metaOverride parameter so callers can pass per-call SelectiveElements (used by the staged
// construction rendering to draw only the built shape elements). The 1.20/1.21 CreateMesh has no
// such parameter - it instead reads SelectiveElements off the block's CompositeShape.
//
// This reproduces the 5-arg signature on legacy as a faithful copy of 1.22's CreateMesh: it
// resolves the block shape and tesselates with the metaOverride applied directly to the
// TesselationMetaData (exactly `metaOverride?.X ?? block.Shape.X`), without mutating any shared
// state. The body mirrors the decompiled pre-1.22 CreateMesh.
#if !GAME_GE_1_22
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ExpandedLib.Legacy;

public static class LegacyAnimUtil
{
  private static readonly FieldInfo BeField =
    typeof(BlockEntityAnimationUtil).GetField(
      "be",
      BindingFlags.NonPublic | BindingFlags.Instance
    )!;

  extension(BlockEntityAnimationUtil util)
  {
    public MeshData CreateMesh(
      string nameForLogging,
      Shape? shape,
      out Shape resultingShape,
      ITexPositionSource? texSource,
      TesselationMetaData? metaOverride
    )
    {
      var be = (BlockEntity)BeField.GetValue(util)!;
      var api = be.Api;
      var capi = (ICoreClientAPI)api;
      Block block = api.World.BlockAccessor.GetBlock(be.Pos);

      texSource ??= capi.Tesselator.GetTextureSource(
        block,
        0,
        returnNullWhenMissing: false
      );

      if (shape == null)
      {
        AssetLocation loc = block
          .Shape.Base.Clone()
          .WithPathPrefixOnce("shapes/")
          .WithPathAppendixOnce(".json");
        shape = Shape.TryGet(api, loc);
        if (shape == null)
        {
          api.World.Logger.Error(
            "Shape for block {0} not found at {1}; block animations not loaded.",
            block.Code,
            loc
          );
          resultingShape = null!;
          return new MeshData(initialiseArrays: true);
        }
      }

      // Resolve via the API present on the legacy floor (1.20.0 / 1.21.0): the void
      // ResolveReferences + the params-only ResolveAndFindJoints. The Dictionary-returning
      // CollectAndResolveReferences / Dictionary ResolveAndFindJoints overload only arrived in 1.21
      // (and later 1.20.x patches), so using them would break on a clean 1.20.0. This mirrors the
      // 1.20.0 vanilla BlockEntityAnimationUtil.CreateMesh exactly.
      shape.ResolveReferences(api.World.Logger, nameForLogging);
      shape.CacheInvTransforms();
      shape.ResolveAndFindJoints(
        api.World.Logger,
        nameForLogging,
        Array.Empty<string>()
      );

      var meta = new TesselationMetaData
      {
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
