namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>Operating state of a furnace core.</summary>
public enum FurnaceState {
  /// <summary>Not lit.</summary>
  Idle,

  /// <summary>Lit and heating up, but not yet hot enough to melt.</summary>
  Firing,

  /// <summary>Hot enough to melt; producing molten product.</summary>
  Melting,
}
