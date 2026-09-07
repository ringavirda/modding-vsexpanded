namespace ExpandedLib.Verify;

/// <summary>How serious a <see cref="Finding"/> is - the tool's own two-tier severity, distinct
/// from <c>ExpandedLib.Checks.CheckResult</c>'s error-only shape.</summary>
public enum FindingLevel {
  /// <summary>Something JSON-only modding can never make reliable to check further, or a soft
  /// dependency this run has no way to resolve. Never fails the run unless <c>--strict</c>.</summary>
  Info,

  /// <summary>A defect the game itself would hit: a patch that cannot apply, a dangling code, a
  /// missing lang key. Always fails the run (exit code 1).</summary>
  Error,
}

/// <summary>One thing <c>exlib-verify</c> found: which check raised it, where (when known), and a
/// human-readable message. <see cref="File"/>/<see cref="Line"/> are null when the finding is not
/// anchored to one file or the check has no line information to offer.</summary>
public sealed record Finding(
  FindingLevel Level,
  string Check,
  string? File,
  int? Line,
  string Message
) {
  /// <summary>One readable line: <c>LEVEL check file:line message</c>, the file/line segment
  /// omitted when either is unknown.</summary>
  public override string ToString() {
    string location =
      File == null ? ""
      : Line == null ? $"{File}: "
      : $"{File}:{Line}: ";
    return $"{Level.ToString().ToUpperInvariant(),-5} [{Check}] {location}{Message}";
  }
}
