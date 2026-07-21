namespace SteelmakingExpanded.BlockStructures.Converter;

/// <summary>
/// Player-selected tilt state of the Bessemer converter vessel. The single "pouring" tilt of the old
/// three-state machine is split into two: because slag floats on the steel, a shallow tilt spills the
/// slag off the top first (<see cref="SlagPouring"/>), and only a deeper tilt reaches the steel beneath
/// (<see cref="SteelPouring"/>). Both drain through the same output cell; the state selects which pool
/// feeds it. Normal is reachable from any state; the pour deepens Normal → Slag → Steel.
/// </summary>
public enum ConverterOpState
{
  /// <summary>Upright. Blows/refines the molten charge while blast and power are present.</summary>
  Normal,

  /// <summary>Tilted toward the input tap to draw molten pig iron into the vessel.</summary>
  Filling,

  /// <summary>Shallow tilt: spills the floating slag off the top through the output cell.</summary>
  SlagPouring,

  /// <summary>Deep tilt: pours the steel beneath the slag out through the output cell.</summary>
  SteelPouring,
}
