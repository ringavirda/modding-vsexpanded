namespace ExpandedLib.Networks;

/// <summary>
/// Marker for a pipe-network node whose open top connector may be drawn through by a vanilla
/// chimney (a sink, not a leak). The generic chimney-vent strategy matches on this interface rather
/// than on concrete block types, so the (exlib) network core and the (iwex) vent strategy stay free
/// of any content mod's specific fitting classes - a content mod opts a fitting in simply by
/// implementing this marker (e.g. lpex's passthrough / outlet).
/// </summary>
public interface IChimneyVentable { }
