namespace IronIndustryExpanded.Items;

/// <summary>
/// The shingling chain's shared numbers: a puddled ball goes on the anvil as a block of metal voxels, a
/// second ball is piled on top of the first, and the helve beats the pile down into one shingled bar.
/// </summary>
/// <remarks>
/// Mass is conserved exactly and nothing is shed, which is what makes the target shape's voxel count and
/// the two balls' voxel counts the same number. That is deliberate: the helve's
/// <c>FullyWorkable</c> mode hammers away every metal voxel outside the recipe shape, so a pile that did
/// not fit the target exactly would quietly lose iron on every bar.
/// </remarks>
public static class Shingling {
  /// <summary>
  /// The work item a pile of balls becomes. Its own item rather than vanilla's <c>workitem-iron</c>,
  /// because the helve needs exactly one matching recipe and iron has dozens: with one match the anvil
  /// selects it without a dialog, and that absence of a mode switch is the mechanic - whatever is piled,
  /// a bar is the only thing it can make.
  /// </summary>
  public const string WorkItemCode = "iiex:shingleworkitem-iron";

  /// <summary>The smithing recipe's code, which is also its golden's name.</summary>
  public const string RecipeCode = "iiexshingle";

  /// <summary>
  /// Metal voxels one ball is worth, at the density the pig already uses
  /// (<see cref="ItemPig.PigUnits"/> over <see cref="PigBreaking.PigVoxels"/> = 2.5 u per voxel). Two
  /// balls make <see cref="BarVoxels"/>, which is the bar exactly.
  /// </summary>
  public const int BallVoxels = 80;

  /// <summary>Balls in one bar. The pile the helve beats down.</summary>
  public const int BallsPerBar = 2;

  /// <summary>Metal voxels in the finished bar - the recipe's target shape, filled exactly.</summary>
  public const int BarVoxels = BallVoxels * BallsPerBar;

  /// <summary>Voxels across the anvil the pile occupies, x 0..<see cref="Width"/>-1.</summary>
  public const int Width = 16;

  /// <summary>Voxels deep the pile occupies, z 0..<see cref="Depth"/>-1.</summary>
  public const int Depth = 5;

  /// <summary>Layers the pile stands, y 0..<see cref="Layers"/>-1 - one per ball.</summary>
  public const int Layers = BallsPerBar;
}
