namespace ExpandedLib.Networks;

/// <summary>
/// Marker for a pipe-network node whose open top connector may be drawn through by a vanilla
/// chimney, which acts as a sink rather than a leak. The chimney-vent strategy matches on this
/// interface rather than on concrete block types, so a content mod opts a fitting in by
/// implementing it (lpex's passthrough and outlet do).
/// </summary>
public interface IChimneyVentable { }
