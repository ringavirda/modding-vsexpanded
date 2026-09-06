using System.Runtime.CompilerServices;

// ExpandedLib.Tests drives the asset-free internal seams directly (MetalCatalogueLoader.Populate,
// MetalFamilyEmitter.TextureOf, MetalToolEmitter, BlockPipe.Asset) rather than only through the
// AssetsFinalize/AssetsLoaded entry points, the same reason exlib's own InternalsVisibleTo exists.
[assembly: InternalsVisibleTo("ExpandedLib.Tests")]
