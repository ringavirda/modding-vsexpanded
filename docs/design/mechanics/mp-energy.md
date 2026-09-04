# MP Energy Network
**Status** live   **Mod** exlib (graph + physics) · iiex (every block)
**Owns** the `"mpenergy"` network: the one-spinning-shaft model (`E = ½Iω²`, `I·dω/dt = τ_drive − τ_load − τ_fric`), the four node contracts (`IMpEnergyProducer` / `IMpEnergyStorage` / `IMpEnergyConsumer` / `IMpEnergyDirection`), merge/split semantics, the vanilla-MP bridge at the flywheel hub and its torque curve, the transmission's two-network gear coupling, the direction flag, `MaxSpeed` and everything derived from it, the animation-speed convention, and every `Mp*` / `Flywheel*` / `ShaftInertia` config key.
**Depends on** [multiblock](multiblock.md) (the fillers the flywheel/transmission/mill reserve their volume with, and why an axle cell is not a filler) · [conventions](../conventions.md) (R7, the network-family list)

---

## Role

Vanilla MP has no storage. This network models energy in a spinning shaft rather than "power available", so
the machines that matter - a rolling pass, a hammer blow - can draw far more torque for two seconds than a
period prime mover produces continuously.

A flywheel is inertia, not a battery: a drive that cannot out-torque the load plus standing friction never
spins the wheel up at all, so a run can never be trickle-charged into a pulse.

At the iron tier the prime mover is a vanilla waterwheel or windmill, bridged into the run through the
flywheel's hub cell. In iiex the player swaps it for a steam engine; the network itself is unchanged.

---

## How it works

### One run = one lumped shaft

Every connected `"mpenergy"` node set is one `MpEnergyNetwork` with one `MpEnergyNetworkState`
(`MpEnergyNetwork.cs:21`, `MpEnergyNetworkState.cs:19`). All quantities are SI: ω rad/s, I kg·m², τ N·m,
E joules, P watts (`MpEnergyNetworkState.cs:15-16`). Display conversion is `ExMeasure`'s job -
`ExMeasure.cs:128` (rpm), `:133` (power), `:141` (energy), `:151` (charge %).

`BlockNetworkModSystem` ticks every live network once a second (`BlockNetworkModSystem.cs:42-45`), which
is the `dt` handed to `OnTick`.

### The tick

`MpEnergyNetwork.OnTick` (`MpEnergyNetwork.cs:53-108`) walks the node set once at the run's current
speed and sums three things plus a flag:

| Sum | From | Line |
|---|---|---|
| `I` = Σ `Inertia` | every `IMpEnergyStorage` | `MpEnergyNetwork.cs:68-69` |
| `τ_drive` = Σ `DriveTorque(ω)` | every `IMpEnergyProducer` | `:70-71` |
| `τ_load` = Σ `LoadTorque(ω)` | every `IMpEnergyConsumer` | `:72-73` |
| `Reversed` | any `IMpEnergyDirection` with `IsReversed` | `:76-77` |

All three sums clamp their per-node contribution at ≥ 0 (`Math.Max(0f, …)`), so a node cannot supply
negative inertia or negative load. Direction is last-writer-wins across the walk: two drives fighting each
other is a build error, not a state worth modelling (`:75-77`).

A run with no inertia is dropped entirely: `if (inertia <= 0f)` nulls the state and broadcasts (`:81-89`),
so `State` is `null` and every reader sees ω = 0.

### The integration step

`MpEnergyNetworkState.Step` (`MpEnergyNetworkState.cs:72-92`) is pure and unit-testable without a world:

```
τ_fric = frictionCoeff·ω + max(0, idleTorque)        // :84   windage + standing resistance
τ_net  = τ_drive − τ_load − τ_fric                   // :85
Δω     = τ_net / I · dt                              // :86   (0 when I ≤ 0)
ω      = clamp(ω + Δω, 0, maxSpeed)                  // :88
E      = ½Iω²                                        // :89   EnergyAtSpeed
P_sup  = τ_drive·ω     P_dem = τ_load·ω              // :90-91 (display only)
```

Everything follows from the sign of `τ_net`, with no special cases (`:65-70`):

- The idle torque is a resistance, never a source, and ω is clamped at 0, so a shaft the drive cannot
  start stays stopped. This is the "not a battery" gate.
- Sustained under-torque winds ω down to a hard stall.
- A cut drive lets a charged wheel coast down at `b·ω + τ_idle`.
- A large `I` barely moves under a brief load spike or between engine strokes: that is the buffering.
- There is no over-speed mechanic. The `ω_max` clamp is the whole governor; a destructive over-speed burst
  is not modelled.

`StoredEnergy` is written every step from ω and I, so it is a derived readout, not an independent
accumulator:

| Quantity | Formula | Line |
|---|---|---|
| `CapacityFor(I, ω_max)` | `½·I·ω_max²` | `MpEnergyNetworkState.cs:48-49` |
| `DeriveSpeed(E, I)` | `√(2E/I)`, 0 when `I ≤ 0` | `:53-54` |
| `EnergyAtSpeed(I, ω)` | `½Iω²` | `:58-59` |

Capacity is derived, never declared: storage nodes contribute inertia only, so a full reservoir is exactly a
flywheel spinning at `ω_max`.

### Torque governs, not power

`IMpEnergyProducer.DriveTorque(speed)` returns a torque-speed curve, not a flat power
(`MpEnergyNodes.cs:8-15`): a flat power would let an under-powered drive buffer its way past any load, a
curve lets it stall. `IMpEnergyConsumer.LoadTorque(speed)` is the mirror (`MpEnergyNodes.cs:35-41`).

The rolling mill's implementation is independent of speed (`BlockEntityRollingMill.cs:314-326`, reasoning at
`:308-312`): plastic deformation resists the same however fast the rolls turn, unlike the friction term,
which eases off as ω falls. That asymmetry lets a pass drag a run all the way to a stall instead of settling
at a slower equilibrium.

### Direction

ω is unsigned; the whole balance is magnitudes (`MpEnergyNodes.cs:43-51`,
`MpEnergyNetworkState.cs:40-43`). Direction rides alongside as `State.Reversed`, entering the network only at
the bridge (`BlockEntityFlywheel.cs:81-97`, reading `BEBehaviorMPFillerPort.IsReversed` at
`BEBehaviorMPFillerPort.cs:57`). Its one consumer today is the rolling mill: `DriveReversed` swaps
`InputDeck`/`OutputDeck` (`BlockEntityRollingMill.cs:236-247`).

### The vanilla-MP bridge

The flywheel is a storage node and a producer (`BlockEntityFlywheel.cs:34-38`). It does not talk to the
vanilla MP network itself: its hub footprint cell(s) host a `BEBehaviorMPFillerPort`
(`BlockFlywheel.cs:45-48, 55, 71`), a real `BEBehaviorMPBase` participant on the vanilla graph, whose speed
the flywheel BE reads back and converts:

```
BridgeDriveTorque(hubSpeed, maxTorque, ratedHubSpeed)                    // BlockEntityFlywheel.cs:70-77
  = 0                                    when ratedHubSpeed ≤ 0 or hubSpeed ≤ 0
  = maxTorque · clamp(hubSpeed/rated, 0, 1)
```

Linear up to rated, flat above it, so an over-driven axle never pushes harder than rated. At rest this is
full torque, so the bridge can start a stopped load; if that full torque is still below `τ_load + τ_idle`,
the shaft never spins up.

`HubAxleSpeed()` takes the fastest turning hub port (`BlockEntityFlywheel.cs:205-219`). Hub cells are
hard-coded per size: normal `(0,1,0)`, large `(0,2,0)` and `(0,2,1)` - two hubs on the large wheel so an
axle couples from either shaft face (`:105-106`). Each hub declares both a north and a south port spec
(`BlockFlywheel.cs:45-48`), and `BEBehaviorMPFillerPort` additionally connects the opposite end of its axis
at init (`BEBehaviorMPFillerPort.cs:79-80`), so a row of ports merges into one vanilla network and power
passes straight through.

`DriveTorque(shaftSpeed)` ignores the mpenergy shaft speed today (`BlockEntityFlywheel.cs:56-61`); the
`ω_max` clamp in `Step` is the only governor.

### Transmission: coupling two separate runs

A transmission is not a graph node. Its principal reads the mpenergy network on each of its two port cells
via `GetNetworkAt` and projects them onto a gear constraint without ever merging them
(`BlockTransmission.cs:20-26`, `BlockEntityTransmission.cs:256-270`, `:278-305`). South port is `+Z`, north
port is `−Z` in the machine's own frame (`BlockEntityTransmission.cs:263-264`, `:284-285`).

`CoupleRatio` (`MpEnergyNetworkState.cs:107-130`):

```
E_total = (½·I_s·ω_s² + ½·I_n·ω_n²) · clamp(retention, 0, 1)     // :118-121
I_comb  = I_s + I_n / ratio²                                    // :122  north reflected to the south side
ω_s     = min(√(2·E_total / I_comb), maxSpeed)                   // :123
ω_n     = ω_s / ratio                                            // :124
```

Energy is conserved exactly when `retention = 1` and nothing clamps; that is the test invariant. Because
`ratio ≥ 1`, `ω_n ≤ ω_s`, so the single clamp on ω_s keeps the constraint valid. Speed, not `StoredEnergy`,
is the source of truth, which makes it robust to a stale energy field. A no-op when either side has no
inertia (`:115-116`). `TryCouple` refuses when the two ports resolve to the same network (`ReferenceEquals`,
a run looped back through the transmission) (`BlockEntityTransmission.cs:290`).

`retention = 1 − MpGearMeshLoss·dt` (`BlockEntityTransmission.cs:296`), so the mesh loss is a per-second
fraction, dt-scaled at the coupling tick.

### Merge and split

| Event | Behaviour | Line |
|---|---|---|
| Merge | pool `Inertia` and `StoredEnergy`, clamp E to the merged capacity `½·(I₁+I₂)·ω_max²`, re-derive ω | `MpEnergyNetwork.cs:114-131`, `:123-124` |
| Split | each fragment keeps `E · (I_frag / I_orig)`; its own inertia is recomputed on the next tick, so only the energy is seeded | `:133-155` |
| Fragment inertia | summed straight off the fragment's storage nodes, used only at split time before the first tick | `:158-165` |
| Rebuild | `InheritStateFrom` hands the whole state object across | `:45-49` |

### Client sync and animation

The network broadcast only reaches server BEs, so the flywheel round-trips the run state onto its own tree
and pushes it to clients on a throttled `MarkDirty` (`BlockEntityFlywheel.cs:223-224`, `:227-248`
serialize/deserialize, `:266-282` throttle). The throttle fires when the displayed speed moves by ≥ 2 % of
full scale or on a stop (`:272`). The transmission does the same for its two side speeds
(`BlockEntityTransmission.cs:220-234`), since it is not a graph node and receives no broadcast.

Clips are authored as one revolution, so the animation playback multiplier is the shaft's revolutions per
second, `ω / 2π` (`EnergyAnim.cs:23-24`). A shaft below 1 % of ω_max reads as stopped and rests on `idle`
(`EnergyAnim.cs:14`, `:28-29`); one clip must always be active or the animator drops the suppressed mesh
back to the static shape (`BlockEntityFlywheel.cs:141-150`).

Bevel branch spin sign falls out of the mitre pair rather than a lookup:
`ω_branch = −sign(branchFace · its own axis) · ω_driver`, so the two branches of one bevel turn opposite ways
(`EnergyAnim.cs:43-46`).

---

## Numbers

### Framework constants - exlib config, `ex_values.json` (domain `exlib`)

Read live each tick through `ExlibValues` (`MpEnergyNetwork.cs:28-30`), so retuning needs no rebuild.

| Key | Value | file:line | What it does |
|---|---|---|---|
| `MpFrictionCoeff` | `0.05` | `ExlibConfig.cs:114` | windage/bearing coefficient `b` (N·m per rad/s); the speed-proportional drain that winds an unpowered run down |
| `MpIdleTorque` | `0.5` | `ExlibConfig.cs:120` | standing-resistance floor `τ_idle` (N·m); range `[0, 1000]`. With the load, the threshold below which a drive never spins up |
| `MpMaxSpeed` | `2.0` | `ExlibConfig.cs:126` | burst speed `ω_max` (rad/s); range `[0.1, 1000]`. Capacity `= ½Iω_max²` scales with its square |
| `MpGearMeshLoss` | `0.02` | `ExlibConfig.cs:132` | transmission mesh loss, fraction of coupled energy lost per second; range `[0, 1]`; 0 = lossless |

### Content constants - iiex config, `ex_values.json` (domain `iiex`)

| Key | Value | file:line | What it does |
|---|---|---|---|
| `FlywheelInertiaNormal` | `10` | `IiexConfig.cs:804` | `I` of the 3×3×1 disc, the reference; range `[0.01, 1e6]` |
| `FlywheelInertiaLarge` | `150` | `IiexConfig.cs:809` | `I` of the 5×5×2 disc - 15× the normal, since a disc's `I` scales with `R⁴·t`; so 15× the energy and 15× the spin-up |
| `FlywheelBridgeChargePower` | `1.0` | `IiexConfig.cs:820` | bridge drive torque (N·m) at/above rated axle speed; range `[0, 1e6]` |
| `FlywheelBridgeRatedAxleSpeed` | `1.0` | `IiexConfig.cs:825` | axle speed at which the bridge delivers full torque (vanilla MP rated speed is ~1); range `[0.01, 1000]` |
| `ShaftInertia` | `0.5` | `IiexConfig.cs:830` | `I` a single cast-iron shaft (or bevel) segment adds - the "Buffer" node's rotating mass; range `[0, 1e6]` |

### Hard-coded - not config

| Constant | Value | file:line | Note |
|---|---|---|---|
| Network tick interval | `1000 ms` | `BlockNetworkModSystem.cs:42-45` | the `dt` of every `OnTick`, shared by all network families |
| Sync threshold | `0.02 · MpMaxSpeed` | `BlockEntityFlywheel.cs:272`, `BlockEntityTransmission.cs:222` | literal `0.02f` in both places, not a shared symbol |
| `EnergyAnim.StoppedFraction` | `0.01` | `EnergyAnim.cs:14` | `private const`; below 1 % of ω_max a shaft reads as stopped |
| Transmission ratios | `x2 → 2`, `x4 → 4`, `clutch → 1` | `BlockEntityTransmission.cs:59-64` | a `switch` on the variant, not config - the ratio is chosen by which block is built |
| Transmission tick | `250 ms` | `BlockEntityTransmission.cs:76` | `ProductionTickMs`; the coupling runs 4× per network tick |
| Mill pass tick | `250 ms` | `BlockEntityRollingMill.cs:41` | `PassTickMs`; reads the live network so progress stays in step with the load it imposes |
| Flywheel hub cells | normal `(0,1,0)`; large `(0,2,0)`,`(0,2,1)` | `BlockEntityFlywheel.cs:105-106` | `static readonly` tuples that must mirror `BlockFlywheel`'s footprint by hand |
| `BEBehaviorMPFillerPort.DefaultResistance` | `0.5` | `BEBehaviorMPFillerPort.cs:30` | vanilla-MP load a hosted port presents when its spec sets no `resistance` |
| Port turning epsilon | `0.001` | `BEBehaviorMPFillerPort.cs:45`, `:57` | vanilla-network speed below which the port reads as stopped |
| Block-info power gate | `> 1 W` | `BlockEntityFlywheel.cs:302` | supply/demand line is suppressed below this |
| Bevel gear item | `"iiex:bevelgear"` | `BlockCastIronBevel.cs:26` | `const string GearItemCode` |

The mill's own balance levers (`RollingLoadTorque`, `RollingTempC`, `RollingRollRadius`, …) are documented
on its page, but `RollingLoadTorque` is derived from this page's numbers: one bridge drive (1 N·m) less
friction at ω_max (`0.05·2 + 0.5 = 0.6`) leaves 0.4 N·m of headroom, and the mill's declared demand takes
85 % of it. Change `MpFrictionCoeff`, `MpIdleTorque`, `MpMaxSpeed` or `FlywheelBridgeChargePower` and the
mill's calibration moves with them.

★ Since 2026-08-13 that is the **only** thing that moves it. The mill's load used to be computed from the
bite (`T = Y·w·R·δ·k`), so re-cutting its schedule moved its demand on this network without anyone
choosing to - which is exactly what happened, and cost the mill 5× its intended draw. A consumer that
declares what it takes is the shape to copy for the next one.

---

## Code

### exlib - the model

| Type / member | file:line | Role |
|---|---|---|
| `MpEnergyNetwork` | `ExpandedLib/Networks/MpEnergyNetwork.cs:21` | `BlockNetwork` subclass; `NetworkType => "mpenergy"` (`:23`) |
| `.OnTick` | `:53-108` | the node walk + integrate + broadcast |
| `.OnMerge` / `.OnSplitFragment` | `:114-131` / `:133-155` | reservoir pooling and proportional split |
| `.State` (typed) | `:37-41` | shadows `BlockNetwork.State` so base code and the typed accessor share one object |
| `MpEnergyNetworkState` | `Networks/MpEnergyNetworkState.cs:19` | `Speed`, `Inertia`, `StoredEnergy`, `SupplyPower`, `DemandPower`, `Reversed` |
| `.Step` | `:72-92` | the simulation; pure static, no world needed |
| `.CoupleRatio` | `:107-130` | rigid-gear projection of two runs |
| `.CapacityFor` / `.DeriveSpeed` / `.EnergyAtSpeed` | `:48`, `:53`, `:58` | pure helpers |
| `IMpEnergyProducer.DriveTorque(ω)` | `Networks/MpEnergyNodes.cs:14` | implement to drive a run |
| `IMpEnergyStorage.Inertia` | `:26` | implement to buffer a run |
| `IMpEnergyConsumer.LoadTorque(ω)` | `:40` | implement to load a run |
| `IMpEnergyDirection.IsReversed` | `:56` | implement to set the run's direction |
| `BEBehaviorMPFillerPort` | `Blocks/Structures/BEBehaviorMPFillerPort.cs:25` | the vanilla-MP participant a footprint cell hosts; `Speed`/`IsTurning`/`IsReversed`/`CurrentAngleRad` |
| `BlockNetworkModSystem.GetNetworkAt` | `Blocks/Networks/BlockNetworkModSystem.cs:54-58` | how a non-node machine (the transmission, the mill's pass tick) reads a run |

### iiex - the blocks

| Type | file:line | Notes |
|---|---|---|
| `BlockFlywheel` | `BlockNetworkEnergy/Blocks/BlockFlywheel.cs:33` | `normal` 3×3×1 / `large` 5×5×2 × `ns`/`we`; footprints at `:52-64`, `:68-92`; `StructureAngle` at `:144` |
| `BlockEntityFlywheel` | `BlockNetworkEnergy/BlockEntities/BlockEntityFlywheel.cs:34` | storage + producer + direction; `BridgeDriveTorque` at `:70-77` is pure and pinned by tests |
| `BlockCastIronShaft` | `Blocks/BlockCastIronShaft.cs:19` | `ns`/`we`/`ud`, so a run can climb; using a bevel gear on one swaps it for a bevel (`:62-88`) |
| `BlockEntityCastIronShaft` | `BlockEntities/BlockEntityCastIronShaft.cs:14` | pass-through node + `Inertia => IiexValues.ShaftInertia` (`:24`) |
| `BlockCastIronBevel` | `Blocks/BlockCastIronBevel.cs:21` | `HasConnectorAt => true` on every face (`:65-69`) - the junction; drops shaft + gear (`:118-135`) |
| `BlockEntityCastIronBevel` | `BlockEntities/BlockEntityCastIronBevel.cs:19` | inherits the shaft's inertia; geared faces derived live from connected neighbours (`:23-37`), mesh composed in `OnTesselation` (`:54-80`) |
| `BlockTransmission` | `Blocks/BlockTransmission.cs:29` | `x2`/`x4`/`clutch` × 4 sides; 2×2 footprint (`:57-63`); three-stage RCC (`:67-78`); clutch lever routed from the `(1,1,0)` cell (`:116-136`) |
| `BlockEntityTransmission` | `BlockEntities/BlockEntityTransmission.cs:35` | `TryCouple` (`:278-305`), `SyncSideSpeeds` (`:220-234`), `ToggleEngaged` (`:99-106`) |
| `BlockRollingMill` | `BlockStructures/Forming/Blocks/BlockRollingMill.cs:31` | the consumer principal; places its two axle cells at `:169-192` |
| `BlockRollingMillAxle` | `Forming/Blocks/BlockRollingMillAxle.cs:20` | invisible graph node, not a filler (see [multiblock](multiblock.md)) |
| `BlockEntityRollingMill` | `Forming/BlockEntities/BlockEntityRollingMill.cs:33` | `LoadTorque` (`:314-326`), `AdvancePass` (`:333-371`) |
| `EnergyAnim` | `BlockNetworkEnergy/EnergyAnim.cs:10` | `SpinSpeed`, `IsTurning`, `BranchSpinSign` - pure, so the convention is pinned headless |
| Registration | `IronIndustryExpandedModSystem.cs:100-103` | `RegisterNetworkType("mpenergy", () => new MpEnergyNetwork(netManager))` |

### Where a caller hooks in

| To add | Contract |
|---|---|
| a machine that draws from the network | derive from `BlockNetworkNode` with `NetworkType => "mpenergy"`; give its BE `BlockEntityNetworkNode` + `IMpEnergyConsumer`; return the resisting torque from `LoadTorque(speed)` and 0 while idle. Advance the operation on its own tick by reading `(NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Speed` - the mill's pattern at `BlockEntityRollingMill.cs:75-82` |
| a prime mover | implement `IMpEnergyProducer` and return a torque curve, not a constant power. To sit on a vanilla axle, copy the flywheel's hub pattern: a footprint cell hosting `exlib.BEBehaviorMPFillerPort`, then read `port.Speed` back |
| storage | implement `IMpEnergyStorage`. Capacity, spin-up and buffering all fall out of `I` |

---

## Gotchas

- A run with no storage node has no state at all. `OnTick` nulls `State` when `Σ I ≤ 0`
  (`MpEnergyNetwork.cs:81-89`). A mill wired to a bare bridge with no flywheel and no shaft segment sees
  `Speed == 0` and can never roll. Every practical run needs at least one shaft segment
  (`ShaftInertia = 0.5`) or a wheel.
- A filler cell is a graph node exactly when it declares one. The walk resolves a cell through
  `NetworkMembership.Resolve` (`BlockNetworkModSystem.cs:482`), which reads the memberships on its block
  entity before the block, so a footprint cell hosting a `BEBehaviorNetworkMember` bridges a run the
  same way a node block does. The rolling mill's drive line still uses dedicated `BlockRollingMillAxle`
  cells (`BlockRollingMill.cs:78-85`), which predate the rule and stay only because removing a placed
  block needs a migration. See [multiblock](multiblock.md).
- The transmission must not become a node. If it did, its two sides would merge into a single run and the
  ratio would be meaningless. `ReferenceEquals(south, north)` guards the degenerate loop-back case
  (`BlockEntityTransmission.cs:290`).
- Direction is last-writer-wins. Two bridges turning opposite ways on one run produce an arbitrary
  `Reversed`, silently flipping the mill's feed deck. Not detected, not reported
  (`MpEnergyNetwork.cs:75-77`).
- Hub cells are duplicated by hand. `BlockEntityFlywheel.NormalHubs`/`LargeHubs` (`:105-106`) must mirror
  the `'M'` glyph in `BlockFlywheel`'s footprints (`BlockFlywheel.cs:52-92`). Nothing checks this; a wheel
  whose hub moved would silently never charge.
- `SpinSpeed` assumes every clip is authored as exactly one revolution (`EnergyAnim.cs:16-22`). A clip
  authored as two revolutions animates at half the true speed with no error anywhere. The ratio
  transmissions additionally rely on one north revolution per `cycle` with the south gear geared up inside
  the clip (`BlockEntityTransmission.cs:134-138`) - an art-side invariant with no code guard.
- The 2 % sync step is a duplicated literal, not a shared constant (`BlockEntityFlywheel.cs:272`,
  `BlockEntityTransmission.cs:222`), and `EnergyAnim.StoppedFraction` is a different number (1 %) whose doc
  comment claims it "matches the flywheel's 2 % block-info sync step" (`EnergyAnim.cs:12-14`). It does not.
  Harmless, since the animation threshold being tighter than the sync threshold is the safe direction.
- `CoupleRatio`'s summary says the reduction "slows and gains torque" (`MpEnergyNetworkState.cs:96-98`).
  Only the speed constraint is modelled; torque is summed per-network in `OnTick` and never reflected across
  the gear. The gameplay effect (a heavy machine on the slow side is fed from a bigger reservoir) is real;
  the mechanism is not the one the prose implies.
- Vertical `ud` shafts exist, so a `ud` bevel must too, or a run could climb and never turn off. Both blocks
  declare all three orientations for exactly this reason (`BlockCastIronBevel.cs:46-48`).

Stale source comments, all still in the tree:

| Says | Where | Truth |
|---|---|---|
| `FlywheelBridgeChargePower` names a power | `IiexConfig.cs:817-820` | its own summary says "Drive torque (N.m)", and `BridgeDriveTorque` returns N·m |
| capacity unit is "MP.s" | `IiexConfig.cs:802` | joules - `MpEnergyNetworkState.cs:30-32` and `:15-16` are the authority |
| "torque is not modelled here (the whole sub-machine ecosystem is speed-driven)" | `IiexConfig.cs:814` | `DriveTorque` returns N·m |
| "Phase-1 scope … the vanilla-MP bridge … live with the flywheel/engine blocks in a later increment" | `MpEnergyNetwork.cs:14-16` | the bridge is built (`BlockEntityFlywheel.cs:56-77`) |
| "The spin animation and any producers that spend the stored energy are follow-ups; until one lands the run simply sits idle" | `BlockFlywheel.cs:28-29` | both landed: spin animation at `BlockEntityFlywheel.cs:151-188`, the rolling mill is a live consumer |
| the base is "Currently used for gas pipes and molten canals" | `BlockNetworkNode.cs:18` | it is also the base of every mpenergy node |

---

## Idle draw - settled 2026-08-05, not yet built

An idle machine on the run costs power: every connected consumer contributes a standing torque whether or
not it is working. Today `LoadTorque` returns 0 when idle and the idle torque is a single per-network
constant (`MpEnergyNetwork.cs:29`), which is what the ruling changes.

The escape from a standing draw is to declutch the branch, which needs nothing new: the clutch is already a
`BlockTransmission` variant - a 2×2 footprint with a lever cell and persisted `_engaged`, where "a
disengaged clutch leaves the two runs fully independent" (`BlockEntityTransmission.cs:67-68`). The lever
belongs to the transmission, a block the player sites, not to a per-machine engaged flag inside every
bench. Historically, line shafting's no-load loss was the defining inefficiency of a shafted mill, and
fast-and-loose pulleys existed so an idle machine could be thrown off the line.

Build cost is nothing new: `MpIdleTorque` already exists (`ExlibConfig.cs:120`) as the standing-resistance
floor; what changes is that it becomes per connected consumer instead of one per-network constant. It makes
the wide hall's power cost, steel roll sets' "a bigger plant" gate, the nail machine's bank argument and the
flywheel's reason to exist mechanically true.

Consequence to design for: a machine built and walked away from drains the run forever unless it is behind a
clutch, so the clutch's engaged state needs a readout - the open item below. The torque value itself is
calibration, not part of this ruling.

## Open

- Pulsed supply is not built. The design's phase 2 (a producer `Power(phase)` so a bare engine visibly
  labours between strokes) has no implementation; the bridge is a smooth torque curve.
- No speed droop on the bridge. `DriveTorque` ignores its `shaftSpeed` argument
  (`BlockEntityFlywheel.cs:56-61`), so a fully-charged run still shows full supply power in block info.
- The flywheel cannot drive back into vanilla MP. The bridge is one-way (vanilla → mpenergy). The design's
  phase 4 wanted both directions.
- Only one consumer exists: the rolling mill. The steam hammer (iiex) and any pulveriser/stamp are unbuilt,
  so the transmission's ratios and the large flywheel have nothing yet that justifies them in play.
- The clutch's engaged state has no readout beyond the lever pose and the two shafts visibly turning at
  different speeds. Under R7 ("nothing is hidden") that is probably enough, but it has not been checked
  against the rule's wording.
- Every number above is a first-pass calibration, not a playtested one. The only worked example is the
  mill's, at `IiexConfig.cs:865`; `MpGearMeshLoss` in particular has never been exercised against a chain of
  transmissions.
