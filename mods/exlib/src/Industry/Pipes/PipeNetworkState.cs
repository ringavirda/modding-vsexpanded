using ExpandedLib.Catalogues;

namespace ExpandedLib.Industry.Pipes;

/// <summary>
/// Live state of a pipe run. A network carries exactly one medium, either a gas (Air, Steam,
/// Exhaust) or a liquid (Water), claimed by the first producer and held until the run empties. A
/// gas's <see cref="Pressure"/> is the uncapped volume ratio <c>Volume / MaxVolume</c>, so producers
/// overflow up to their own choke; a liquid's is set by the pump. Temperature is network-wide.
/// </summary>
public class PipeNetworkState {
  /// <summary>Content currently held by the network, in litres (gas or water).</summary>
  public float Volume { get; set; }

  /// <summary>Maximum the network can hold at 1 atm (<see cref="ExlibValues.LitresPerPipe"/> per pipe node).</summary>
  public float MaxVolume { get; set; }

  /// <summary>Temperature (°C) of the content, injected by the producing source.</summary>
  public float Temperature { get; set; } = 20f;

  /// <summary>Current medium: "Air", "Steam", "Exhaust", "Water", or "" when empty.</summary>
  public string MediumType { get; set; } = "";

  /// <summary>Pressure in atm. For a gas, the uncapped <c>Volume / MaxVolume</c>. For a liquid, the
  /// fill ratio while below capacity, jumping to <see cref="FeedPressure"/> once brim-full, since a
  /// liquid cannot be packed past <see cref="MaxVolume"/>.</summary>
  public float Pressure { get; set; }

  /// <summary>Pump-commanded feed pressure (atm) for a liquid run: the engine inlet steam pressure
  /// scaled by efficiency. Realised as the run's <see cref="Pressure"/> only once the line is
  /// brim-full; below capacity the pressure tracks the fill ratio. Unused for gas.</summary>
  public float FeedPressure { get; set; }

  /// <summary>Number of open-ended connectors (leaks) on the network.</summary>
  public int OpeningsCount { get; set; } = 0;

  /// <summary>
  /// Throughput in L/s: the volume moved over the last second, taken as the greater of produced and
  /// consumed and computed once per second by the pipe network's tick. Marks a line as live even when
  /// the run sits near 0 L because a producer feeds and a consumer drains at the same rate.
  /// </summary>
  public float FlowRate { get; set; } = 0f;

  /// <summary>Whether the network currently carries a liquid rather than a gas. Resolved through the
  /// shared <see cref="ExLiquids.Taxonomy"/>, which always seeds the four built-ins, so a mod-added
  /// liquid reads correctly.</summary>
  public bool IsLiquid => ExLiquids.Taxonomy.IsLiquid(MediumType);

  /// <summary>Whether the network has any open-ended connectors.</summary>
  public bool IsLeaking => OpeningsCount > 0;

  /// <summary>Gas pressure (atm) for a given pool state.</summary>
  public static float ComputeGasPressure(
    float currentVolume,
    float maxVolume
  ) => maxVolume > 0f ? currentVolume / maxVolume : 0f;

  /// <summary>Liquid pressure (atm): the fill ratio <c>Volume / MaxVolume</c> while below capacity,
  /// jumping to the pump-set <paramref name="feedPressure"/> once the run is brim-full. A liquid
  /// cannot be packed past <see cref="MaxVolume"/>, so a full line carries whatever the pump drives.</summary>
  public static float ComputeLiquidPressure(
    float currentVolume,
    float maxVolume,
    float feedPressure
  ) =>
    maxVolume <= 0f ? 0f
    : currentVolume >= maxVolume - 0.001f ? feedPressure
    : currentVolume / maxVolume;
}
