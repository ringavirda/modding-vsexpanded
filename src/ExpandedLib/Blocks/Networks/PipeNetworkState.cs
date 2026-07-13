namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Live state of a pipe run. A network carries exactly ONE medium - a gas (Air/Steam/Exhaust)
/// or a liquid (Water), never both - claimed by the first producer and held until empty. A
/// gas's <see cref="Pressure"/> is the volume ratio <c>Volume / MaxVolume</c> (uncapped -
/// producers overflow up to their own choke); a liquid's is set by the pump. Temperature is a
/// single network-wide value.
/// </summary>
public class PipeNetworkState
{
  /// <summary>Content currently held by the network, in litres (gas or water).</summary>
  public float Volume { get; set; }

  /// <summary>Maximum the network can hold at 1 atm (<see cref="ExlibValues.LitresPerPipe"/> per pipe node).</summary>
  public float MaxVolume { get; set; }

  /// <summary>Temperature (°C) of the content, injected by the producing source.</summary>
  public float Temperature { get; set; } = 20f;

  /// <summary>Current medium: "Air", "Steam", "Exhaust", "Water", or "" when empty.</summary>
  public string MediumType { get; set; } = "";

  /// <summary>Pressure in atm - for a gas, <c>Volume / MaxVolume</c> (uncapped); for a
  /// liquid, the fill ratio while below capacity, jumping to <see cref="FeedPressure"/> once
  /// brim-full (a liquid can't be packed past <see cref="MaxVolume"/>).</summary>
  public float Pressure { get; set; }

  /// <summary>Pump-commanded feed pressure (atm) for a liquid run - the engine inlet steam
  /// pressure scaled by efficiency. Realised as the run's <see cref="Pressure"/> only once the
  /// line is brim-full; below capacity the pressure tracks the fill ratio. Unused for gas.</summary>
  public float FeedPressure { get; set; }

  /// <summary>Number of open-ended connectors (leaks) on the network.</summary>
  public int OpeningsCount { get; set; } = 0;

  /// <summary>
  /// Throughput in L/s - the volume moved over the last second (max of produced and consumed),
  /// computed once per second by the pipe network's tick. Marks a live line even when the run
  /// sits near 0 L (a producer feeds and a consumer drains at the same rate).
  /// </summary>
  public float FlowRate { get; set; } = 0f;

  /// <summary>Whether the network currently carries a liquid (water) rather than a gas.</summary>
  public bool IsLiquid => MediumType == "Water";

  /// <summary>Whether the network has any open-ended connectors.</summary>
  public bool IsLeaking => OpeningsCount > 0;

  /// <summary>Whether <paramref name="medium"/> can be produced into a run currently
  /// carrying <paramref name="current"/> - same family only (gases mix; water is its own
  /// medium), or an empty run that hasn't claimed a medium yet.</summary>
  public static bool MediaCompatible(string current, string medium) =>
    current.Length == 0 || (current == "Water") == (medium == "Water");

  /// <summary>
  /// Returns the higher-priority gas of two types when gas runs merge
  /// (Exhaust &gt; Air). Steam ranks with Air (it is just hot Air-pool content).
  /// </summary>
  public static string GetHigherPriorityGas(string type1, string type2)
  {
    if (type1 == "Exhaust" || type2 == "Exhaust")
      return "Exhaust";
    if (type1 == "Steam" || type2 == "Steam")
      return "Steam";
    return "Air";
  }

  /// <summary>Gas pressure (atm) for a given pool state.</summary>
  public static float ComputeGasPressure(float currentVolume, float maxVolume) =>
    maxVolume > 0f ? currentVolume / maxVolume : 0f;

  /// <summary>Liquid pressure (atm): the fill ratio (<c>Volume / MaxVolume</c>, like a gas)
  /// while below capacity, jumping to the pump-set <paramref name="feedPressure"/> once the run
  /// is brim-full - a liquid can't be packed past <see cref="MaxVolume"/>, so a full line carries
  /// whatever pressure the pump drives it to.</summary>
  public static float ComputeLiquidPressure(
    float currentVolume,
    float maxVolume,
    float feedPressure
  ) =>
    maxVolume <= 0f ? 0f
    : currentVolume >= maxVolume - 0.001f ? feedPressure
    : currentVolume / maxVolume;
}
