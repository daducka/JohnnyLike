# Narration Beats

## What is a NarrationBeat?

A **NarrationBeat** is a small, domain-authored sentence that describes a meaningful event in the simulation. Beats are collected by the engine during world ticks and action execution, then emitted as `TraceEvent`s of type `"NarrationBeat"`. The narration pipeline consumes these events and uses them—alongside recent history and actor facts—to generate spoken narration via LLM and TTS.

Beats are the primary way domain code communicates narrative information to the LLM **without** coupling the narration system to Island-specific event types.

---

## Style guide

When writing beat text, follow these rules:

1. **One sentence.** Keep it tight. Avoid compound sentences with semicolons.
2. **Present tense.** "The tide turns." not "The tide turned."
3. **No internal IDs.** Don't write `"actor:Johnny"` or `"item:fishing_pole_Johnny"`. Use readable names: `"Johnny"`, `"the fishing rod"`.
4. **Observer perspective.** Write as if a documentary narrator is watching.
5. **Transitions only.** Only emit a beat when something *changes* — not every tick.
6. **No redundancy.** If the action-completed event already narrates the outcome, the beat should add something different, not repeat it.

**Good:**
```
"The tide turns, rising from low to high."
"The fishing rod is starting to splinter."
"A new day dawns—day 3 on the island."
"Johnny pulls a fish from the water."
```

**Bad:**
```
"Tide level changed."              // Too terse, no narrative voice
"actor:Johnny has caught fish."    // Exposes internal ID
"Johnny caught a fish successfully after rolling above DC 12."  // Too mechanical
"The fishing rod quality is 74.3 percent."  // State dump, not a beat
```

---

## How to emit beats in domain code

### In world-tick handlers (`ITickableWorldItem.Tick`)

The active tracer is available on the `IslandWorldState`:

```csharp
using JohnnyLike.Domain.Island.Telemetry;

public List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime)
{
    var prevTide = Tide;
    // ... update Tide ...

    if (Tide != prevTide)
    {
        using (world.Tracer.PushPhase(TracePhase.WorldTick))
            world.Tracer.BeatWorld(
                "The tide turns, rising from low to high.",
                subjectId: "beach:tide",
                priority: 20);
    }

    return new List<TraceEvent>();
}
```

### In `MaintainableWorldItem.Tick`

Override `EmitDegradationBeat` and `EmitBrokenBeat` to provide item-specific text:

```csharp
protected override void EmitDegradationBeat(IEventTracer tracer, double threshold)
{
    var description = threshold switch
    {
        >= 75.0 => "starting to show wear",
        >= 50.0 => "getting difficult to cast",
        >= 25.0 => "starting to splinter",
        _ => "barely holding together"
    };
    using (tracer.PushPhase(TracePhase.WorldTick))
        tracer.BeatWorld($"The fishing rod is {description}.", subjectId: "item:fishing_pole", priority: 30);
}
```

### In action effect handlers (`EffectContext`)

The tracer is available on `EffectContext.Tracer`:

```csharp
EffectHandler: new Action<EffectContext>(effectCtx =>
{
    // ... compute outcome ...

    using (effectCtx.Tracer.PushPhase(TracePhase.ActionCompleted))
    {
        if (success)
            effectCtx.Tracer.BeatActor(
                effectCtx.ActorId.Value,
                "Johnny pulls a fish from the water.",
                subjectId: "resource:fish",
                priority: 60);
        else
            effectCtx.Tracer.BeatActor(
                effectCtx.ActorId.Value,
                "The line comes back empty.",
                subjectId: "resource:fish",
                priority: 50);
    }
})
```

### Helper extensions

`TracerExtensions` (in `JohnnyLike.Domain.Island.Telemetry`) provides:

- `tracer.BeatWorld(text, subjectId, priority)` — world-state beats (no actor)
- `tracer.BeatActor(actorId, text, subjectId, priority)` — actor-linked beats

---

## Priority guide

| Priority | When to use |
|----------|-------------|
| 70–100   | Recipe discoveries, critical story moments |
| 50–69    | Action outcomes (fishing success/failure, treasure found) |
| 30–49    | Tool breakage, campfire extinguished |
| 15–29    | World transitions (tide, temperature, day change) |
| 0–14     | Low-signal state updates (reserved for future use) |

The narration pipeline uses priority to select which beats to surface in LLM prompts when the buffer is large.

---

## Architecture overview

```
Domain code
  └─► IEventTracer.Beat(text, subjectId, priority, actorId)
        │
        ▼
  EventTracer (Engine) — buffers NarrationBeat records
        │
  Engine drains at boundaries:
    • after TickWorldState completes
    • after ApplyActionEffects completes
        │
        ▼
  ITraceSink.Record(TraceEvent { EventType="NarrationBeat", Details=[text, phase, priority, ...] })
        │
        ▼
  TraceBeatExtractor.Consume → NarrationJob
        │
        ▼
  LLM prompt includes domain beats in "DOMAIN BEATS" section
        │
        ▼
  TTS → audio narration
```
