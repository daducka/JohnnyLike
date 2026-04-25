using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Leftover fish scraps from eating raw or cooked fish, or occasionally found on the beach.
/// Can be crafted into fishing bait.
///
/// Implements <see cref="ISupplyActionCandidate"/> to offer a scavenger-only
/// <c>scavenge_carcass_scraps</c> action when sufficient quantity is present.
///
/// Implements <see cref="ISupplyWorldEventProvider"/> to attract new <see cref="CrabActorState"/>
/// actors when the quantity exceeds <see cref="CrabSpawnScrapsThreshold"/>. The spawning
/// is invoked each tick by <see cref="SupplyPile"/> as the world-event provider.
/// </summary>
public class CarcassScrapsSupply : SupplyItem, ISupplyActionCandidate, ISupplyWorldEventProvider
{
    // ── Scavenge action constants ─────────────────────────────────────────────

    /// <summary>
    /// Satiety gained per scavenging event in kcal. Raw scraps are not very nutritious —
    /// roughly 100 kcal worth (~5 Satiety points) per scavenge action.
    /// </summary>
    private const double ScavengeKcal = 100.0;

    /// <summary>Minimum quantity of scraps required before the scavenge action is offered.</summary>
    private const double MinQuantityToScavenge = 1.0;

    /// <summary>Amount of scraps consumed by one scavenging event.</summary>
    private const double ScavengeConsumeAmount = 1.0;

    // ── Crab spawning constants ───────────────────────────────────────────────

    /// <summary>Minimum CarcassScraps quantity before a crab may spawn.</summary>
    public const double CrabSpawnScrapsThreshold = 5.0;

    /// <summary>
    /// Probability per tick of a crab spawning when the threshold is met.
    /// At 1 Hz ticking, a crab typically appears after ~2 sim-hours on average (7 200 ticks).
    /// </summary>
    public const double CrabSpawnProbabilityPerTick = 1.0 / 7200.0;

    /// <summary>Maximum number of active crabs in the world at any time.</summary>
    public const int MaxActiveCrabs = 3;

    // ── Constructors ──────────────────────────────────────────────────────────

    public CarcassScrapsSupply(double quantity)
        : this("carcass_scraps", quantity)
    {
    }

    public CarcassScrapsSupply(string id = "carcass_scraps", double quantity = 0.0)
        : base(id, "supply_carcass_scraps", quantity)
    {
    }

    // ── ISupplyActionCandidate ────────────────────────────────────────────────

    /// <summary>
    /// Provides the <c>scavenge_carcass_scraps</c> action candidate to scavengers when
    /// there is at least <see cref="MinQuantityToScavenge"/> of scraps available.
    /// The action is gated by <see cref="CandidateRequirements.IsScavenger"/> and
    /// <see cref="CandidateRequirements.AliveOnly"/>, so humans never see it.
    /// </summary>
    public void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output)
    {
        if (Quantity < MinQuantityToScavenge)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("scavenge_carcass_scraps"),
                ActionKind.Interact,
                EmptyActionParameters.Instance,
                Duration.Minutes(3.0, 5.0, ctx.Random),
                NarrationDescription: "scavenge carcass scraps"
            ),
            0.30,
            Reason: "Scavenge carcass scraps for food",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(ScavengeKcal);
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} picks through the scraps and finds enough to eat.");
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<CarcassScrapsSupply>(ScavengeConsumeAmount)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 1.0
            },
            ActorRequirement: actor => CandidateRequirements.IsScavenger(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }

    // ── ISupplyWorldEventProvider ─────────────────────────────────────────────

    /// <summary>
    /// Attempts to spawn a new <see cref="CrabActorState"/> when carcass scraps are
    /// sufficiently abundant. Called once per tick by <see cref="SupplyPile"/>.
    ///
    /// Spawn conditions:
    /// <list type="bullet">
    ///   <item>A mutable actor dictionary is available (engine-owned reference).</item>
    ///   <item>Active crab count is below <see cref="MaxActiveCrabs"/>.</item>
    ///   <item>Scraps quantity ≥ <see cref="CrabSpawnScrapsThreshold"/>.</item>
    ///   <item>A per-tick random roll passes <see cref="CrabSpawnProbabilityPerTick"/>.</item>
    /// </list>
    /// </summary>
    public void ExecuteWorldEvents(
        SupplyPile pile,      // available for implementations that consume/query the pile
        IslandWorldState world,
        long currentTick,
        Dictionary<ActorId, ActorState>? mutableActors)
    {
        if (mutableActors == null)
            return;

        if (world.ActiveCrabActors.Count >= MaxActiveCrabs)
            return;

        if (Quantity < CrabSpawnScrapsThreshold)
            return;

        // Per-tick seeded RNG for reproducibility without shared mutable state.
        // XOR high and low 32 bits before multiplying to avoid collisions when
        // the low 32 bits of currentTick wrap around (would otherwise recur at
        // tick ≈ 2^32/1337 ≈ 3.2 M).
        var seed = unchecked((int)(currentTick ^ (currentTick >> 32)) * 1337 + 42);
        var spawnRng = new Random(seed);
        if (spawnRng.NextDouble() > CrabSpawnProbabilityPerTick)
            return;

        var crabId = new ActorId($"crab_{currentTick}");
        var crab = IslandDomainPack.CreateCrabActorState(crabId);
        crab.Status = ActorStatus.Ready;

        mutableActors[crabId] = crab;
        world.ActiveCrabActors.Add(crab);
    }
}
