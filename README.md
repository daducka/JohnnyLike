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
│   ├── JohnnyLike.Domain.Kit.Dice/     # Reusable DnD-style dice & skill toolkit
│   ├── JohnnyLike.Domain.Office/       # Office domain pack (sample)
│   ├── JohnnyLike.Domain.Island/       # Island survival domain pack with skill checks
│   └── JohnnyLike.SimRunner/           # Console app for headless simulation
├── tests/
│   ├── JohnnyLike.Engine.Tests/        # Engine unit tests
│   ├── JohnnyLike.Domain.Kit.Dice.Tests/ # Dice kit unit tests
│   ├── JohnnyLike.Domain.Office.Tests/ # Domain validation tests
│   ├── JohnnyLike.Domain.Island.Tests/ # Island domain tests
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

#### Default Simulation (Office domain)

```bash
dotnet run --project src/JohnnyLike.SimRunner -- --duration 30
```

#### Island Domain Simulation

```bash
dotnet run --project src/JohnnyLike.SimRunner -- --domain island --duration 60 --seed 42
```

#### With Scenario File

```bash
dotnet run --project src/JohnnyLike.SimRunner -- --scenario scenarios/jim_pam_highfive.json --trace
```

#### Command-line Options

- `--scenario <path>`: Load and run scenario from JSON file
- `--domain <name>`: Domain to use: office, island (default: office)
- `--seed <number>`: Random seed (default: 42)
- `--duration <sec>`: Simulation duration in seconds
- `--trace`: Output detailed trace events

#### Fuzz Testing

Run deterministic fuzz tests to stress-test the engine with random events and validate invariants:

```bash
# Run 10 fuzz tests with default config (Office domain)
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 10 --seed 100

# Run Island domain fuzz tests
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --domain island --runs 10 --seed 100 --profile smoke

# Run with custom config
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 5 --config fuzz-configs/stress-test.json

# Use predefined profiles
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 10 --seed 123 --profile smoke
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 200 --profile extended
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 1000 --profile nightly

# Verbose output for detailed metrics
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 1 --seed 42 --verbose
```

**Fuzz Testing Profiles:**

- **smoke** - Fast, deterministic tests for CI (60s sim, 2 actors, low failure rates)
  - Used in required `fuzz-smoke` CI check
  - Runs on every PR and push to main
  - Designed to complete in < 60 seconds
  
- **extended** - Moderate stress testing (180s sim, 6 actors, higher event rates)
  - Triggered manually via `workflow_dispatch` or with `run-extended-fuzz` label
  - 200 runs for thorough coverage
  - Uploads failure artifacts for debugging
  
- **nightly** - Maximum stress testing (240s sim, 8 actors, high burst/failure rates)
  - Runs daily via cron schedule at 2 AM UTC
  - 1000 runs to catch rare edge cases
  - Uploads all results and failures as artifacts

**Fuzz Testing Features:**
- **Deterministic Event Generation**: Pre-generated signal schedule from seed (Poisson arrivals with bursts)
- **Action Jitter**: Random duration variations for realism (configurable %)
- **Failure Injection**: Random task failures, no-shows, busy locks
- **Invariant Checking**: Real-time validation of reservation conflicts, scene lifetimes, starvation, signal backlogs
- **Metrics Collection**: Tracks actions, scenes, signals, per-actor completions
- **Reproducible Failures**: Full config + event schedule + trace logged on violation

**Replaying Failures:**

If a fuzz test fails, the output includes:
1. Seed that caused the failure
2. Full FuzzConfig as JSON
3. Complete EventSchedule
4. Last 100 trace events

To replay a failure:
```bash
# Use the seed from the failure output
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --runs 1 --seed <failed-seed> --profile <profile-name> --verbose
```

The deterministic nature ensures the exact same trace will be produced, making debugging straightforward.

**CI Integration:**

The repository includes three GitHub Actions workflows:
- `.github/workflows/ci.yml` - Required checks including `ci-tests` and `fuzz-smoke`
- `.github/workflows/fuzz-extended.yml` - Manual or label-triggered extended testing
- `.github/workflows/fuzz-nightly.yml` - Daily stress testing with artifact uploads

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

## Dice Kit: Reusable DnD-style Skill Resolution

The `JohnnyLike.Domain.Kit.Dice` package provides a generic, reusable D&D-inspired dice rolling and skill check system that can be used by any domain pack.

### Core Features

- **D20 Rolls**: Standard d20, advantage, disadvantage
- **Skill Checks**: Deterministic skill resolution with DC, modifiers, and advantage
- **Outcome Tiers**: CriticalFailure, Failure, PartialSuccess, Success, CriticalSuccess
- **Probability Estimation**: Calculate success chances for action scoring
- **Domain-Agnostic**: No dependencies on engine internals except `IRngStream`

### Key Types

```csharp
// Roll a D20 with various modes
int roll = Dice.RollD20(rng);
int advantageRoll = Dice.RollD20WithAdvantage(rng);
int disadvantageRoll = Dice.RollD20WithDisadvantage(rng);

// Calculate ability modifier from D&D stats
int modifier = DndMath.AbilityModifier(14); // Returns 2

// Estimate success probability for action scoring
double chance = DndMath.EstimateSuccessChanceD20(dc: 15, modifier: 3, AdvantageType.Normal);

// Resolve a skill check
var request = new SkillCheckRequest(DC: 15, Modifier: 3, AdvantageType.Normal, "Fishing");
var result = SkillCheckResolver.Resolve(rng, request);
// result.OutcomeTier: CriticalSuccess, Success, PartialSuccess, Failure, or CriticalFailure
// result.IsSuccess: true/false (considers natural 1/20 auto-fail/success)
// result.EstimatedSuccessChance: probability estimate
```

### Usage in Domain Packs

Domain packs can use the Dice kit to:
- Determine action outcomes with realistic probability distributions
- Weight action candidates by success probability
- Implement buffs/debuffs via advantage/disadvantage
- Create dynamic difficulty with context-sensitive DCs

## Island Domain: Survival Simulation

The `JohnnyLike.Domain.Island` demonstrates the Dice kit with a castaway survival domain featuring:

### Actor State
- **Attributes**: STR, DEX, CON, INT, WIS, CHA (D&D-style stats)
- **Derived Skills**: FishingSkill, SurvivalSkill, PerceptionSkill, PerformanceSkill
- **Needs**: Hunger (0-100), Energy (0-100), Morale (0-100), Boredom (0-100)
- **Buff System**: Temporary modifiers and advantage grants

### World State
- **Time**: Day/night cycle with timeOfDay (0-1) and dayCount
- **Weather**: Clear, Rainy, Windy (affects skill DCs)
- **Resources**: Fish population and coconut availability with regeneration
- **Tide**: Low/High tides affecting activities

### Actions with Skill Checks

All actions use the Dice kit for resolution:

- **FishForFood**: Fishing skill check, DC varies by time of day, weather, fish availability, and energy
- **ShakeTreeForCoconut**: Survival skill check, DC based on coconut availability and weather
- **BuildSandCastle**: Performance skill check for morale/boredom management
- **Swim**: Survival/athletics check with morale and energy effects
- **SleepUnderTree**: No check, restores energy

### Vignette Events

Rare, special events with perception checks:
- **PLANE_SIGHTING** (DC 15): Grants morale boost, critical success adds temporary Luck buff
- **MERMAID_ENCOUNTER** (DC 18, night only): Grants strong morale boost, critical success adds fishing advantage

### Dynamic Scoring

Action candidates are scored using estimated success probability:
```csharp
var baseScore = 0.5 + (hunger / 100.0);
var estimatedChance = DndMath.EstimateSuccessChanceD20(dc, modifier, advantage);
var finalScore = baseScore * estimatedChance; // Lower score for harder checks

// Exception: Survival actions override probability penalty when needs are critical
if (hunger > 70.0 || energy < 20.0)
    finalScore = 1.0; // Desperation ignores difficulty
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
