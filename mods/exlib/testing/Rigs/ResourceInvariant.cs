using System;
using System.Collections.Generic;

namespace ExpandedLib.Testing;

/// <summary>
/// A randomised-operation invariant check: builds a fresh <typeparamref name="TState"/>, applies a
/// random sequence of the given moves to it, and asserts the invariant after every move. Where a
/// resource can be split, merged, drained or refilled in any order, this is cheaper than enumerating
/// every ordering by hand and it reports the exact sequence that broke it, not just that one did.
/// </summary>
public sealed class ResourceInvariant<TState>(
  Func<TState> fresh,
  IReadOnlyList<Action<TState>> moves,
  Action<TState> assert
) {
  /// <summary>
  /// Runs <paramref name="sequences"/> independent runs of <paramref name="movesPerSequence"/> random
  /// moves each, asserting after every move. <paramref name="seed"/> makes a failure reproducible: the
  /// same seed replays the same sequences.
  /// </summary>
  /// <exception cref="InvalidOperationException">The invariant failed; the message names the sequence
  /// number, the move index within it, and the move indices applied so far.</exception>
  public void Run(int sequences = 5, int movesPerSequence = 50, int seed = 1) {
    var rng = new Random(seed);

    for (int s = 0; s < sequences; s++) {
      TState state = fresh();
      var applied = new List<int>(movesPerSequence);

      for (int m = 0; m < movesPerSequence; m++) {
        int move = rng.Next(moves.Count);
        applied.Add(move);
        moves[move](state);

        try {
          assert(state);
        } catch (Exception ex) {
          throw new InvalidOperationException(
            $"sequence {s} (seed {seed}) failed on move {m} of {movesPerSequence} "
              + $"(move index {move}); moves applied so far: [{string.Join(", ", applied)}]",
            ex
          );
        }
      }
    }
  }
}
