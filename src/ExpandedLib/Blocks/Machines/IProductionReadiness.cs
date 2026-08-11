namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// One answer to whether a machine may run its production process, published by whatever knows it: a
/// multiblock's completed pattern, an RCC megablock's finished construction stages, a sub-machine's
/// resolved master. Either the block entity or any behaviour on it may publish one, so an answer that
/// is topology rather than shape has the same standing as one that is form. Read through
/// <see cref="ProductionReadiness"/>, never by naming the type that answers.
/// See docs/design/mechanics/framework-composition.md.
/// </summary>
/// <remarks>
/// <see cref="StopsProductionWhenNotReady"/> carries a default, and a default is silently reached when
/// an implementor declares its answer in a shape the interface map does not see. A per-machine answer
/// must be a plain <c>public</c> class member or an override of one, never a fresh member on a
/// subclass of a class that already lists this interface, which never enters that class's map.
/// </remarks>
public interface IProductionReadiness {
  /// <summary>Whether the machine may run production right now.</summary>
  bool IsReadyToProduce { get; }

  /// <summary>
  /// Whether losing readiness also unregisters the production tick. <c>true</c> by default. A machine
  /// that must keep running while un-ready answers <c>false</c> here rather than widening
  /// <see cref="IsReadyToProduce"/>: the gate is only read by a listener that still exists, so widening
  /// it alone leaves the machine frozen with its state held rather than stopped.
  /// </summary>
  bool StopsProductionWhenNotReady => true;
}
