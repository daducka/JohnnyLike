# JohnnyLike

JohnnyLike is a deterministic autonomy simulation engine for autonomous actors living in dynamic worlds.

Inspired by **Johnny Castaway**, **The Sims autonomy model**, and idle simulations—but designed first and foremost as a fully deterministic, testable, headless simulation engine.

The engine runs without Unity and is intended to serve as a reusable autonomy core for games, simulations, and streaming integrations.

The flagship domain is a castaway survival simulation where Johnny must fish, explore, maintain shelter, react to environmental hazards, and experience rare vignette events—all governed by a DnD-style skill system.

---

## v0.2 Architecture

### Dependency Graph

```
Domain.Abstractions   (no dependencies)
       ↑
Domain.Island         (depends on Abstractions)
Domain.Kit.Dice       (depends on Abstractions)
       ↑
Engine                (depends on Abstractions)
       ↑
SimRunner             (depends on Engine + Domain.Island)
```

### Core Loop (`Engine.AdvanceTicks`)

Each call to `AdvanceTicks` performs the following in order:

1. **ITickableWorldItem ticking** — `WorldItemTickOrchestrator` topologically sorts all items implementing `ITickableWorldItem` and ticks them in stable deterministic order. The engine owns this orchestration; domains must not tick tickables themselves.
2. **Domain world tick** — `IDomainPack.TickWorldState` handles domain-specific passive updates: quality decay on `MaintainableWorldItem` instances, item expiration, and supply regeneration.
3. **Signal processing** — Queued signals whose tick has arrived are dequeued and dispatched to the domain.
4. **Housekeeping** — Expired reservations are cleaned up; variety memory is trimmed.

### Action Planning (`Director.PlanNextAction`)

1. **Generate candidates** — `IDomainPack.GenerateCandidates` produces all possible actions for the actor. Candidates are tagged with `ProviderItemId` for room filtering.
2. **Room filter** — The engine filters candidates to those whose provider item is in the actor's current room (or is room-agnostic).
3. **Variety penalty** — Recent action history applies a repetition penalty to discourage monotonous loops.
4. **Deterministic sort** — Candidates are sorted by `Score` desc, then `ActionId` asc, then `ProviderItemId` asc for fully stable tie-breaking.
5. **Resource reservation** — For each candidate in order, the director attempts to reserve required resources. The first candidate whose resources are all available is chosen.

### Key Abstractions

| Type | Location | Purpose |
|---|---|---|
| `WorldItem` | Domain.Abstractions | Base class for all world objects |
| `ITickableWorldItem` | Domain.Abstractions | Items ticked by engine each step |
| `MaintainableWorldItem` | Domain.Island | Items with quality decay over time |
| `ActionCandidate` | Domain.Abstractions | A possible action with score and provider info |
| `IDomainPack` | Domain.Abstractions | Domain plugin interface |
| `Director` | Engine | Action planning and resource reservation |
| `WorldItemTickOrchestrator` | Engine | Topo-sort and tick of ITickableWorldItems |
| `EngineConstants` | Domain.Abstractions | Shared constants (TickHz = 20) |

### Constants

- `EngineConstants.TickHz = 20` — 20 simulation ticks per second. All time calculations use `ticks / (double)EngineConstants.TickHz` for conversion. Never hardcode `20.0`.

### Determinism

Given the same seed, initial state, and signal schedule, JohnnyLike produces identical trace output. This enables:

- Replay debugging
- Fuzz testing
- CI regression detection

---

## Project Structure

```
src/
  JohnnyLike.Domain.Abstractions/   Core interfaces and base types
  JohnnyLike.Domain.Island/         Castaway survival domain
  JohnnyLike.Domain.Kit.Dice/       DnD-style dice and skill checks
  JohnnyLike.Engine/                Simulation engine
  JohnnyLike.SimRunner/             Fuzz runner and scenario loader
  JohnnyLike.Narration/             LLM narration pipeline
tests/
  JohnnyLike.Engine.Tests/
  JohnnyLike.Domain.Island.Tests/
  JohnnyLike.Scenario.Tests/
  JohnnyLike.Narration.Tests/
```

---

## Running

```bash
# Build
dotnet build

# Test
dotnet test

# Fuzz run (smoke profile)
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --profile smoke

# Run a scenario
dotnet run --project src/JohnnyLike.SimRunner -- --scenario scenarios/basic.json
```
