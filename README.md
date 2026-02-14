# JohnnyLike

A deterministic, headless-testable "idle world" simulation engine inspired by Johnny Castaway and The Sims autonomy system. Designed to be used from Unity but with ZERO Unity dependencies in the core.

## Overview

JohnnyLike is a mono-repo implementing a generic simulation engine where autonomous actors execute tasks to completion without interruption. The system supports multi-actor coordination through "Scenes" (group activities) using join windows, staging, and resource reservations.

## Architecture

### Core Components

- **Engine**: Main simulation coordinator managing world state, actors, time progression, and event processing
- **Director**: Authoritative planner that manages scene lifecycles, resource reservations, and task assignment
- **Domain Packs**: Pluggable content modules that define:
  - State schemas (world and actor states)
  - Action candidates and scoring
  - Scene templates
  - Action effects

### Key Features

- **Deterministic Replay**: Same seed + event schedule = identical trace
- **No Interruption**: Actions execute to completion; new events only influence future decisions
- **Multi-Actor Coordination**: Scenes with join windows and resource locking
- **Variety System**: Memory-based repetition penalty
- **Headless Testing**: FakeExecutor with simulated time for testing without Unity

## Project Structure

```
JohnnyLike/
├── src/
│   ├── JohnnyLike.Engine/              # Core simulation engine
│   ├── JohnnyLike.Domain.Abstractions/ # Interfaces and base types
│   ├── JohnnyLike.Domain.Office/       # Office domain pack (sample)
│   └── JohnnyLike.SimRunner/           # Console app for headless simulation
├── tests/
│   ├── JohnnyLike.Engine.Tests/        # Engine unit tests
│   ├── JohnnyLike.Domain.Office.Tests/ # Domain validation tests
│   └── JohnnyLike.Scenario.Tests/      # System/integration tests
└── scenarios/                          # JSON scenario definitions
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later

### Building

```bash
# Build the entire solution
dotnet build

# Build specific project
dotnet build src/JohnnyLike.Engine
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/JohnnyLike.Engine.Tests

# Run tests with verbose output
dotnet test -v n
```

### Running Simulations

#### Default Simulation

```bash
dotnet run --project src/JohnnyLike.SimRunner -- --duration 30
```

#### With Scenario File

```bash
dotnet run --project src/JohnnyLike.SimRunner -- --scenario scenarios/jim_pam_highfive.json --trace
```

#### Command-line Options

- `--scenario <path>`: Load and run scenario from JSON file
- `--seed <number>`: Random seed (default: 42)
- `--duration <sec>`: Simulation duration in seconds
- `--trace`: Output detailed trace events

#### Fuzz Testing

Run deterministic fuzz tests to stress-test the engine with random events and validate invariants:

```bash
# Run 10 fuzz tests with default config
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 10 --seed 100

# Run with custom config
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 5 --config fuzz-configs/stress-test.json

# Verbose output for detailed metrics
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 1 --seed 42 --verbose
```

**Fuzz Testing Features:**
- **Deterministic Event Generation**: Pre-generated signal schedule from seed (Poisson arrivals with bursts)
- **Action Jitter**: Random duration variations for realism (configurable %)
- **Failure Injection**: Random task failures, no-shows, busy locks
- **Invariant Checking**: Real-time validation of reservation conflicts, scene lifetimes, starvation, signal backlogs
- **Metrics Collection**: Tracks actions, scenes, signals, per-actor completions
- **Reproducible Failures**: Full config + event schedule + trace logged on violation

**Fuzz Config Parameters:**
- `Seed`, `SimulatedDurationSeconds`, `DtSeconds`, `NumActors`
- `EventRatePerMinute`, `BurstProbability`, `BurstMultiplier`
- `ActionDurationJitterPct`, `TravelTimeJitterPct`, `TaskFailureRate`
- `NoShowProbability`, `BusyLockProbability`
- `JoinWindowMinSeconds`, `JoinWindowMaxSeconds`
- `ResourceScarcityProfile`, `MaxActorQueueLength`, `MaxSceneLifetimeSeconds`
- `StarvationThresholdSeconds`, `MaxAllowedReservationConflicts`

## Creating Custom Domain Packs

1. Reference `JohnnyLike.Domain.Abstractions`
2. Implement `IDomainPack` interface
3. Define custom `ActorState` and `WorldState` classes
4. Implement `GenerateCandidates()` for action selection
5. Implement `ApplyActionEffects()` for state changes
6. Define `SceneTemplate` objects for multi-actor activities

Example:
```csharp
public class MyDomainPack : IDomainPack
{
    public string DomainName => "MyDomain";
    
    public WorldState CreateInitialWorldState() { ... }
    public ActorState CreateActorState(ActorId id, ...) { ... }
    public List<ActionCandidate> GenerateCandidates(...) { ... }
    public void ApplyActionEffects(...) { ... }
    public List<SceneTemplate> GetSceneTemplates() { ... }
    public bool ValidateContent(out List<string> errors) { ... }
}
```

## Scenario JSON Format

```json
{
  "name": "ScenarioName",
  "seed": 42,
  "durationSeconds": 60.0,
  "actors": [
    {
      "actorId": "ActorName",
      "initialState": {
        "hunger": 35.0,
        "energy": 80.0
      }
    }
  ],
  "signals": [
    {
      "atTime": 10.0,
      "type": "chat_redeem",
      "targetActor": "ActorName",
      "data": { "emote": "wave" }
    }
  ]
}
```

## Engine API

```csharp
// Initialize engine with domain pack and seed
var engine = new Engine(domainPack, seed, traceSink);

// Add actors
engine.AddActor(new ActorId("Jim"), initialState);

// Advance simulation time
engine.AdvanceTime(0.5); // in seconds

// Enqueue signals for future processing
engine.EnqueueSignal(new Signal(...));

// Get next action for ready actor
if (engine.TryGetNextAction(actorId, out var action))
{
    // Execute action...
}

// Report action completion
engine.ReportActionComplete(actorId, outcome);

// Get trace for analysis
var events = engine.GetTrace();
var hash = TraceHelper.ComputeTraceHash(events);
```

## Testing Strategy

### Unit Tests (`Engine.Tests`)
- ReservationTable prevents double reservation
- SceneInstance aborts after deadline
- Determinism: same seed produces identical trace
- VarietyMemory penalizes repetition

### Domain Tests (`Domain.Office.Tests`)
- Content validation (anchors, scene roles)
- Scoring logic (hunger affects EatSnack priority)
- Chat redeem generates high-priority candidate
- Action effects modify actor state correctly

### Scenario Tests (`Scenario.Tests`)
- Multi-actor scene coordination
- Signal handling without interruption
- Scene abort and resource cleanup
- End-to-end determinism

## Design Principles

1. **No Unity Dependencies**: Core engine is pure C# with no game engine dependencies
2. **Deterministic**: Given same seed and events, produces identical results
3. **No Interruption**: Actions run to completion; events only affect future planning
4. **Resource Safety**: Reservations prevent conflicts; automatic cleanup on deadlines
5. **Extensible**: Domain packs plug in custom content and behavior
6. **Testable**: FakeExecutor enables fast, headless simulation testing

## License

[Your License Here]
