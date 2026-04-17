using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// A crab actor: a non-human living actor in the island simulation.
///
/// Crabs are simple scavengers. They:
/// <list type="bullet">
///   <item>Participate in the sim with their own action decision loop.</item>
///   <item>Use the shared DnD stats, vitals, and skill system from <see cref="LivingActorState"/>.</item>
///   <item>Use <see cref="CrabPhysiologyBuff"/> for crab-specific physiology (very slow metabolism,
///         no morale, health driven by satiety).</item>
///   <item>Satisfy <see cref="CandidateRequirements.IsScavenger"/>.</item>
///   <item>Can scavenge <see cref="CarcassScrapsSupply"/> for food (offered by the supply itself).</item>
///   <item>Can idle/rest to recover energy.</item>
///   <item>Offer a human-only <c>catch_crab</c> action via <see cref="AddCandidates"/>.</item>
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
    /// Contributes all candidates provided by this actor — both self-actions (only visible
    /// to the crab itself) and affordances offered to other actors (only visible to the
    /// appropriate actor type). The <see cref="ActionCandidate.ActorRequirement"/> predicate
    /// on each candidate determines which requesting actors can see it; the caller must not
    /// assume this method produces only self-actions.
    ///
    /// Self-candidates (only available to the crab itself, gated by <c>IsSelf</c>):
    /// <list type="bullet">
    ///   <item><c>crab_idle</c> — low-priority rest/idle action that gently recovers energy.</item>
    /// </list>
    ///
    /// Candidates offered to other actors:
    /// <list type="bullet">
    ///   <item><c>catch_crab</c> — human-only action that permanently removes this crab from
    ///         the simulation and yields <see cref="CrabSupply"/>. The
    ///         <see cref="ActionCandidate.ActorRequirement"/> gates it so only human actors
    ///         receive it.</item>
    /// </list>
    ///
    /// Scavenging candidates are offered by <see cref="CarcassScrapsSupply.AddCandidates"/>
    /// via the supply-pile affordance system.
    /// </summary>
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (!CandidateRequirements.AliveOnly(this))
            return;

        // Idle/rest: self-action, only visible to the crab itself.
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
            ActorRequirement: actor => CandidateRequirements.IsSelf(Id)(actor) && CandidateRequirements.AliveOnly(actor)
        ));

        // Catch crab: offered to nearby human actors only.
        // The effect permanently removes this crab from the simulation.
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

                // Remove this crab immediately from the active actors list so it no longer
                // offers catch affordances. The engine actor dictionary entry is removed on
                // the next TickWorldState call via PendingActorRemovals.
                effectCtx.World.ActiveCrabActors.RemoveAll(c => c.Id == crabActorId);
                effectCtx.World.PendingActorRemovals.Add(crabActorId);

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
