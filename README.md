# Gorilla Tag Client-Side Anti-Cheat

A modular, **client-side** anti-cheat plugin system for Gorilla Tag modded lobbies
(BepInEx + Photon PUN2). It runs entirely inside the mod process with **no server
authority**, relying only on:

- local physics-simulation approximation,
- Photon-replicated state (position, rotation, RPC timing, custom properties — all
  treated as **untrusted** observational metadata),
- tick-based historical buffers,
- behavioural / statistical anomaly detection.

It detects exploitative gameplay (movement cheats, tag exploits, network abuse, and
injected mod behaviour patterns) through **multi-signal correlation**, never a single
rule. The system is designed to **minimise false positives over aggression**, and it
**never bans** — it scores, escalates, and logs replays for human moderators.

> ⚠️ Because there is no server authority, a client-side system can be defeated by an
> attacker who fully controls their own process. The goal here is *correlation-based,
> bypass-resistant detection* of the common exploit classes, not unbeatable security.

---

## Architecture

The code is split into two assemblies so the detection logic is testable and
CI-verified independent of Unity:

| Project | Framework | Built in CI | Purpose |
|---|---|---|---|
| `GorillaAntiCheat.Core` | `netstandard2.0` | ✅ | All detection logic. No Unity/Photon deps. Struct-based, allocation-light, no LINQ on the hot path. |
| `GorillaAntiCheat.Unity` | `netstandard2.1` | ❌ (needs game DLLs) | Thin BepInEx/Photon integration: `MonoBehaviour`, plugin entrypoint, rig sampler, Photon observer, IMGUI overlay. |
| `GorillaAntiCheat.Tests` | `net8.0` | ✅ | xUnit tests for every detector + the scoring engine. |

```
Photon / Unity  ──sample──►  AntiCheatManager (MonoBehaviour, fixed tick)
                                   │ feeds untrusted PlayerTick / TagEvent / RPC / metadata
                                   ▼
                            AntiCheatEngine (core, engine-agnostic)
                             ├─ PlayerState ring buffers (position/velocity/hands/contacts/metadata)
                             ├─ Detectors:  Movement | TagIntegrity | Network | Metadata
                             ├─ ScoringEngine (weighted, decaying, multi-signal)
                             └─ ReplayRecorder (last N seconds per player)
                                   │ escalation events + replay captures
                                   ▼
                            DebugOverlay  /  ReplayWriter (CSV)  /  logs
```

### Core entrypoint — `AntiCheatEngine`
Orchestrates all detectors, manages tick updates, scoring + decay, join grace, and
replay capture on escalation. Implements `IAntiCheatWorld` so detectors can inspect
other players (e.g. a tag target's body) read-only.

### Detector interfaces (strict)
`IMovementDetector`, `ITagIntegrityDetector`, `INetworkDetector`, `IMetadataDetector`
— all extend `IDetector`. Each detector:
- runs **per tick** (not per frame),
- returns structured `Violation` results into a reusable collector,
- **never flags bans** — it only contributes weighted signals to the score,
- is individually **toggleable** (modular plugin architecture).

### Per-player state — `PlayerState`
Pre-allocated ring buffers (`RingBuffer<T>`) holding the last 3–10 s of:
position/velocity history, left/right hand transforms, collision (contact) history
for tag validation, Photon metadata snapshots (untrusted), and a timestamped tick
index. Latency is tracked as a slow EMA baseline for spike normalisation.

---

## Detection systems

### 1. Movement integrity (`MovementIntegrityDetector`)
| Signal | Logic |
|---|---|
| `SPEED_HACK` | rolling-average velocity exceeds the calibrated VR ceiling (smoothing kills spikes) |
| `TELEPORT` | inter-tick displacement over threshold **and** not inside a high-latency lag-comp window |
| `AIR_STALL` | sustained airtime with non-gravity-consistent (hovering) vertical velocity |
| `INVALID_CLIMB` | large hand impulse with no surface contact |
| `NOCLIP` | rig reported inside geometry for N consecutive ticks (persistence, not a one-frame glitch) |

### 2. Tag integrity (`TagIntegrityDetector`, high priority)
Each replicated tag claim must pass a full corroboration chain against physics history:

| Signal | Logic |
|---|---|
| `TAG_AURA` | hand-to-target distance at the tag tick exceeds plausible tag range |
| `EXTENDED_REACH` | reconstructed `head→hand` arm length exceeds the human max |
| `FAKE_TAG` | no real collision contact recorded in the confirm window |
| `SILENT_TAG` | tag landed with neither contact **nor** any proximity in-window (stronger than fake) |
| `LAG_SWITCH_TAG` | latency spike coincident with the interaction |
| `DESYNC_TAG` | claimed-time vs received-time position rollback beyond the allowed amount |

### 3. Network (`NetworkDetector`, Photon-observable only)
| Signal | Logic |
|---|---|
| `RPC_SPAM` | RPC events/sec from one sender over threshold |
| `BURST_LAG_ABUSE` | latency spike correlated with a high-impact (tag) event |
| `STATE_DESYNC` | replicated path has no plausible constant-velocity interpolation |
| `IMPOSSIBLE_TRANSITION` | near-instant rotation flip between ticks |

### 4. Metadata (`MetadataDetector`, context only)
**Never infers cheating from a property.** It keeps a safe whitelist
(`cosmetics, color, hat, nameTag, platform, …`) and only counts how many keys fall
outside it. Unknown keys become a **weak context signal** (`UNEXPECTED_METADATA`) and
**only** when real physics/network violations already exist that tick.

---

## Scoring

Per-category subscores accumulate weighted signals, decay every tick, then combine:

```
finalScore = movement*1.0 + tag*1.5 + network*1.2 + metadataContext*0.3   (clamped)
score      -= decayPerSecond * dt                                          (every tick)
```

Escalation levels: `0–20 Ignore` · `20–50 Highlight` · `50–80 Suspicious` ·
`80+ StrongWarning` (logs a replay). A single isolated event always decays back to
zero — sustained, multi-signal anomalies are what escalate. A **join grace period**
suppresses scoring right after spawn to avoid spawn/teleport-in false flags. Every
threshold and weight lives in `AntiCheatConfig` and is bindable from the BepInEx
config file.

---

## Build & test

```bash
# Core + tests (this is what CI runs)
dotnet test GorillaTagAntiCheat.sln
```

### Building the Unity plugin
The `GorillaAntiCheat.Unity` project references Unity/Photon/BepInEx assemblies that
ship with the game, so it is excluded from the solution/CI. Point it at your install:

```bash
dotnet build src/GorillaAntiCheat.Unity/GorillaAntiCheat.Unity.csproj \
  -p:GameManagedDir="/path/to/Gorilla Tag/Gorilla Tag_Data/Managed"
```

Then drop `GorillaAntiCheat.Core.dll` and `GorillaAntiCheat.Unity.dll` into
`BepInEx/plugins/`.

> The `VRRig` member names read in `GorillaRigProvider` (`headMesh`,
> `leftHandTransform`, `rightHandTransform`, `photonView`) drift between Gorilla Tag
> versions. They are isolated in `GorillaRigProvider.TrySample` so you only adjust one
> method when the game updates. `GorillaRigProvider`/`RigSample` are the single
> game-specific seam; everything else is neutral.

### Hooking tag events
RPC cadence, custom properties, and join/leave are captured automatically by
`PhotonObserver`. Tag *interactions* are game-specific, so forward them with a small
Harmony patch on Gorilla Tag's tag/infection RPC handler:

```csharp
[HarmonyPatch(typeof(/* GorillaTagManager or VRRig */), "/* tag handler method */")]
static class TagHook
{
    static void Postfix(int taggerActor, int targetActor, Vector3 taggerHand)
    {
        AntiCheatPlugin.Manager?.ReportTag(
            taggerActor, targetActor, PhotonNetwork.Time, taggerHand);
    }
}
```

---

## Design rules (enforced throughout)
- **Never** rely on a single detection as proof — all violations are multi-signal weighted.
- Photon data is **untrusted and observational only**.
- Bypass-resistance comes from **correlation**, not signatures.
- **False-positive reduction is prioritised** over aggression (smoothing, persistence
  windows, decay, join grace).
- VR performance: fixed tick rate, struct tick data, pre-allocated ring buffers, no
  LINQ in hot loops, minimal allocations (only rare replay captures allocate).
```
