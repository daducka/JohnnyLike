# JohnnyLike

JohnnyLike is a deterministic autonomy simulation engine for autonomous actors living in dynamic worlds.

It is inspired by systems like **Johnny Castaway**, **The Sims autonomy model**, and idle simulations—but designed first and foremost as a fully deterministic, testable, headless simulation engine.

The engine runs without Unity and is intended to serve as a reusable autonomy core for games, simulations, and streaming integrations.

The flagship domain is a castaway survival simulation, where Johnny must fish, explore, maintain shelter, react to environmental hazards, and experience rare vignette events—all governed by a DnD-style skill system.

---

# Key Features

## Deterministic Simulation

Given:

- Seed  
- Initial state  
- Signal schedule  

JohnnyLike produces identical behavior and identical trace output.

This enables:

- Replay debugging  
- Fuzz testing  
- CI validation  
- Regression detection  

---

## Autonomous Actor Model

Actors:

- Select actions based on needs, environment, and skill probability  
- Execute actions to completion (no interruption model)  
- Adapt behavior over time using memory and variety systems  
- Participate in coordinated multi-actor scenes  

---

## Pluggable Domain Packs

The engine is domain-agnostic. All behavior is defined by domain packs.

Domains define:

- Actor state  
- World state  
- Candidate actions  
- Skill checks  
- Action effects  
- Scene templates  
- Signal handling  

Current domains:

- JohnnyLike.Domain.Office — coordination demo  
- JohnnyLike.Domain.Island — survival simulation (primary domain)  

---

## DnD-Style Dice and Skill System

JohnnyLike includes a reusable dice toolkit:

- D20 rolls  
- Advantage / disadvantage  
- Skill modifiers  
- Critical success / failure  
- Probability estimation for scoring  

Located in:

```
src/JohnnyLike.Domain.Kit.Dice
```

Used extensively by the Island domain.

---

## Persistent World Simulation

Island world includes persistent objects:

- Campfires (fuel, decay, maintenance)  
- Shelter (quality, weather damage)  
- Treasure chests (spawn from swim critical success)  
- Sharks (spawn from swim critical failure)  
- Fish and coconut resources with regeneration  
- Weather and tide systems  
- Day/night cycle  

World state evolves continuously and is fully deterministic.

---

## Rich Skill-Driven Survival Gameplay

Johnny must balance:

- Hunger  
- Energy  
- Morale  
- Boredom  

Actions include:

- Fishing  
- Coconut gathering  
- Swimming  
- Sleeping  
- Sand castle building  
- Chest discovery and opening  
- Campfire maintenance  
- Shelter maintenance  

Rare vignette events:

- Plane sightings  
- Mermaid encounters  

All governed by skill checks with probability-aware scoring.

---

## Fully Instrumented Trace System

Every action generates structured trace output including:

- Action ID  
- Duration  
- Skill DC  
- Modifier  
- Dice roll  
- Total roll  
- Outcome tier  
- World effects  

Example:

```
[1234.50] Johnny - ActionCompleted (
  actionId=fish_for_food,
  outcomeType=Success,
  actualDuration=18.5,
  dc=15,
  modifier=4,
  roll=13,
  total=17,
  tier=Success
)
```

Trace is deterministic and replayable.

---

## Fuzz Testing Infrastructure

JohnnyLike includes full fuzz testing support.

Fuzz tests:

- Generate deterministic random signal schedules  
- Inject failures, delays, and contention  
- Validate invariants  
- Detect rare edge cases  
- Produce reproducible traces  

Profiles:

- smoke  
- extended  
- nightly  

Located in:

```
src/JohnnyLike.SimRunner/FuzzRunner.cs
```

CI runs fuzz tests automatically.

---

# Project Structure

```
src/
  JohnnyLike.Engine/
  JohnnyLike.Domain.Abstractions/
  JohnnyLike.Domain.Kit.Dice/
  JohnnyLike.Domain.Office/
  JohnnyLike.Domain.Island/
  JohnnyLike.SimRunner/

tests/
  JohnnyLike.Engine.Tests/
  JohnnyLike.Domain.Kit.Dice.Tests/
  JohnnyLike.Domain.Office.Tests/
  JohnnyLike.Domain.Island.Tests/
  JohnnyLike.Scenario.Tests/

scenarios/
```

---

# Running the Simulation

Build:

dotnet build

Run Island simulation:

```
dotnet run --project src/JohnnyLike.SimRunner -- --domain island --duration 120 --seed 42 --trace
```

Run fuzz tests:

```
dotnet run --project src/JohnnyLike.SimRunner -- --fuzz --domain island --runs 10 --profile smoke
```

---

# Creating a New Domain

Implement:

IDomainPack

Define:

- ActorState  
- WorldState  
- Candidate generation  
- Skill resolution  
- Action effects  
- Scene templates  

The engine handles everything else.

---

# Design Philosophy

JohnnyLike is built on:

- Determinism first  
- Domain isolation  
- Testability  
- Reproducibility  
- Extensibility  
- Simulation correctness  

Rendering is optional and external.

---

# Current Status

JohnnyLike is fully functional and actively evolving.

The Island domain serves as the primary reference implementation and demonstrates:

- Skill-driven autonomy  
- Persistent world state  
- Resource systems  
- Hazard systems  
- Rare event systems  
- Fuzz-validated stability  

---


JohnnyLike is a foundation for building deterministic autonomous worlds.
