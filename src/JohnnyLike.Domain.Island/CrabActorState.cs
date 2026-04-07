using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// A crab actor: the first non-human living actor in the island simulation.
///
/// Crabs are simple scavengers. They:
/// <list type="bullet">
///   <item>Participate in the sim with their own action decision loop.</item>
///   <item>Use the shared DnD stats, vitals, and skill system from <see cref="LivingActorState"/>.</item>
///   <item>Use <see cref="CrabPhysiologyBuff"/> for crab-specific physiology (very slow metabolism,
///         no morale, health driven by satiety).</item>
///   <item>Satisfy <see cref="CandidateRequirements.IsScavenger"/>.</item>
///   <item>Can scavenge <see cref="CarcassScrapsSupply"/> for food (offered by the supply itself).</item>
///   <item>Offer a human-only <c>catch_crab</c> action via <see cref="AddCandidatesForOtherActors"/>.</item>
///   <item>Can idle/rest to recover energy.</item>
///   <item>Do NOT receive human-only actions (chat, recipes, tools, etc.).</item>
/// </list>
///
/// Crab actions are intentionally minimal and instinctive.
/// </summary>
public class CrabActorState : LivingActorState, IIslandActionCandidate
{
    // ── Idle/rest constants ───────────────────────────────────────────────────

    /// <summary>Energy recovered per idle/rest tick (flat recovery used in PreAction).</summary>
    private const double IdleEnergyRecoveryPerMinute = 0.5; // gentle recovery per minute of rest

    // ── Candidate generation ──────────────────────────────────────────────────

    /// <summary>
    /// Generates action candidates for this crab actor itself:
    /// <list type="bullet">
    ///   <item><c>crab_idle</c> — low-priority rest/idle action that gently recovers energy.</item>
    /// </list>
    ///
    /// Scavenging candidates are offered by <see cref="CarcassScrapsSupply.AddCandidates"/>
    /// via the supply-pile affordance system, not here.
    /// Catch candidates for humans are offered via <see cref="AddCandidatesForOtherActors"/>.
    /// </summary>
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (!CandidateRequirements.AliveOnly(this))
            return;

        // Idle/rest: always available as a fallback; recovers a small amount of energy.
        var duration = Duration.Minutes(5.0, 8.0, ctx.Random);
        var energyRecovery = IdleEnergyRecoveryPerMinute * (duration.Ticks / (double)EngineConstants.TickHz / 60.0);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("crab_idle"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                duration,
                NarrationDescription: "idle and rest"
            ),
            0.10,
            Reason: "Idle/rest (crab default)",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                effectCtx.Actor.Energy += energyRecovery;
                effectCtx.SetOutcomeNarration($"{effectCtx.ActorId.Value} sits still, conserving energy.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Safety] = 0.5
            },
            ActorRequirement: actor => CandidateRequirements.IsScavenger(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }

    /// <summary>
    /// Generates action candidates that this crab offers to OTHER actors (e.g., humans).
    /// Called during candidate generation for each other actor by <see cref="IslandDomainPack"/>.
    ///
    /// Currently provides:
    /// <list type="bullet">
    ///   <item><c>catch_crab</c> — human-only action that removes this crab and yields
    ///         <see cref="CrabSupply"/>.</item>
    /// </list>
    /// </summary>
    public void AddCandidatesForOtherActors(IslandContext ctx, List<ActionCandidate> output)
    {
        if (!CandidateRequirements.AliveOnly(this))
            return;

        var crabActorId = Id;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("catch_crab"),
                ActionKind.Interact,
                EmptyActionParameters.Instance,
                Duration.Minutes(2.0, 4.0, ctx.Random),
                NarrationDescription: "catch a crab"
            ),
            0.18,
            Reason: "Catch a nearby crab",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile != null)
                    pile.AddSupply(1.0, () => new CrabSupply());

                // Find the crab, stash its engine actor state so the engine won't try to
                // run its decision loop after it has been caught, then remove it from the
                // active crabs list so it no longer offers catch affordances to humans.
                var crab = effectCtx.World.ActiveCrabActors.FirstOrDefault(c => c.Id == crabActorId);
                if (crab != null)
                {
                    crab.PresenceState = PresenceState.Stashed;
                    effectCtx.World.ActiveCrabActors.Remove(crab);
                }

                effectCtx.SetOutcomeNarration(
                    $"{effectCtx.ActorId.Value} snatches up the crab before it can scuttle away.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodAcquisition] = 0.7,
                [QualityType.Preparation]     = 0.2
            },
            ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    public override string Serialize()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        return JsonSerializer.Serialize(new
        {
            Id = Id.Value,
            Status,
            CurrentAction = CurrentAction?.Id.Value,
            LastDecisionTick,
            PresenceState,
            STR,
            DEX,
            CON,
            INT,
            WIS,
            CHA,
            Satiety,
            Energy,
            Morale,
            Health,
            ActiveBuffs
        }, options);
    }

    public override void Deserialize(string json)
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);
        if (data == null) return;

        Id = new ActorId(data["Id"].GetString()!);

        if (data.TryGetValue("Status", out var statusEl))
        {
            Status = statusEl.ValueKind == JsonValueKind.String
                ? Enum.Parse<ActorStatus>(statusEl.GetString()!)
                : (ActorStatus)statusEl.GetInt32();
        }

        if (data.TryGetValue("LastDecisionTick", out var ldtEl))
            LastDecisionTick = ldtEl.GetInt64();

        if (data.TryGetValue("PresenceState", out var psEl))
        {
            PresenceState = psEl.ValueKind == JsonValueKind.String
                ? Enum.Parse<PresenceState>(psEl.GetString()!)
                : (PresenceState)psEl.GetInt32();
        }

        if (data.TryGetValue("STR", out var strEl)) STR = strEl.GetInt32();
        if (data.TryGetValue("DEX", out var dexEl)) DEX = dexEl.GetInt32();
        if (data.TryGetValue("CON", out var conEl)) CON = conEl.GetInt32();
        if (data.TryGetValue("INT", out var intEl)) INT = intEl.GetInt32();
        if (data.TryGetValue("WIS", out var wisEl)) WIS = wisEl.GetInt32();
        if (data.TryGetValue("CHA", out var chaEl)) CHA = chaEl.GetInt32();

        if (data.TryGetValue("Satiety", out var satEl)) Satiety = satEl.GetDouble();
        if (data.TryGetValue("Energy",  out var engEl)) Energy  = engEl.GetDouble();
        if (data.TryGetValue("Morale",  out var morEl)) Morale  = morEl.GetDouble();
        if (data.TryGetValue("Health",  out var hltEl)) Health  = hltEl.GetDouble();
    }
}
