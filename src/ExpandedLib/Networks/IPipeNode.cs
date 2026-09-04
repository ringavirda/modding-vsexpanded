namespace ExpandedLib.Networks;

/// <summary>
/// A block entity that participates in the pipe network as an addressable node: gas or liquid can be
/// injected (<see cref="TryProduce"/>) or withdrawn (<see cref="TryConsume"/>) at its position, and
/// the network's medium, temperature, pressure and volume can be read. Implementing this and
/// delegating to the <c>PipeNetwork</c> at the block's position gives a network-compatible block
/// without inheriting from <c>BlockEntityPipe</c>.
/// </summary>
public interface IPipeNode {
  /// <summary>Injects <paramref name="volume"/> litres of <paramref name="gasType"/> at <paramref name="temperature"/> °C into the network. Returns <c>true</c> if any was accepted.</summary>
  bool TryProduce(
    float volume,
    float temperature,
    string gasType = "Air",
    float maxOutputPressure = 1.0f,
    bool bypassLeakCap = false
  );

  /// <summary>Withdraws up to <paramref name="requestedVolume"/> litres from the network. Returns the volume actually consumed.</summary>
  float TryConsume(float requestedVolume);

  /// <summary>Temperature (°C) of this node's network. Uniform across the run, with no gradient.</summary>
  float Temperature { get; }

  /// <summary>Current medium of this node's network ("Air"/"Steam"/"Exhaust"/"Water", or "" when empty).</summary>
  string Medium { get; }

  /// <summary>Whether this node's network currently carries water rather than a gas.</summary>
  bool IsLiquid { get; }

  /// <summary>Pressure (atm) of this node's network: the gas volume ratio, or the pump-set water pressure.</summary>
  float Pressure { get; }

  /// <summary>Current gas or liquid volume (L) in this node's network.</summary>
  float Volume { get; }

  /// <summary>Maximum volume (L) this node's network can hold at its current node count.</summary>
  float MaxVolume { get; }
}
