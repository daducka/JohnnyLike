using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JohnnyLike.SimRunner.PressureFuzzer;

// ─── Output model records ─────────────────────────────────────────────────────

public record ActorStatSnapshot(
    double Satiety,
    double Health,
    double Energy,
    double Morale);

public record FoodContextInfo(
    bool ImmediateFoodAvailable,
    bool AcquirableFoodAvailable);

public record TopCandidateInfo(string Action, double Score);

public record FuzzerFlags(
    bool CriticalState,
    bool FoodAvailableButNotChosen,
    bool NoFoodButNoAcquisition,
    bool PrepDominatesFood,
    bool BedLoopRisk);

public record PressureSample(
    string Actor,
    string Scenario,
    ActorStatSnapshot State,
    FoodContextInfo FoodContext,
    Dictionary<string, double> QualityWeights,
    IReadOnlyList<TopCandidateInfo> TopCandidates,
    FuzzerFlags Flags);

// ─── Runner options ───────────────────────────────────────────────────────────

public record PressureFuzzerOptions(
    IReadOnlyList<string>? ActorFilter = null,
    IReadOnlyList<FuzzerScenarioKind>? ScenarioFilter = null,
    string OutputPath = "./fuzzer-output.json",
    int TopCandidateCount = 5,
    bool CoarseGrid = true);

// ─── Runner ───────────────────────────────────────────────────────────────────

/// <summary>
/// Deterministic grid sampler for the Decision Surface Explorer.
/// For each combination of actor archetype × scenario × stat band, samples the
/// production quality model and candidate scoring without running a full simulation.
/// </summary>
public static class PressureFuzzerRunner
{
    // ── Stat grids ────────────────────────────────────────────────────────────
    private static readonly double[] SatietyCoarse = { 0, 10, 20, 35, 50, 70, 100 };
    private static readonly double[] HealthCoarse  = { 0, 10, 20, 35, 50, 70, 100 };
    private static readonly double[] EnergyCoarse  = { 0, 10, 30, 50, 70, 100 };
    private static readonly double[] MoraleCoarse  = { 0, 10, 30, 50, 70, 100 };

    private static readonly double[] SatietyFine = { 0, 5, 10, 15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100 };
    private static readonly double[] HealthFine  = { 0, 5, 10, 15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100 };
    private static readonly double[] EnergyFine  = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
    private static readonly double[] MoraleFine  = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

    // ── Thresholds for derived flags ─────────────────────────────────────────
    private const double CriticalSatietyThreshold = 20.0;
    private const double CriticalHealthThreshold  = 20.0;
    private const double FoodPressureThreshold    = 30.0;

    /// <summary>
    /// Runs the full pressure fuzzer grid sampling and returns the results.
    /// </summary>
    public static List<PressureSample> Run(PressureFuzzerOptions options)
    {
        var actorNames = options.ActorFilter?.ToList()
            ?? Archetypes.All.Keys.OrderBy(k => k).ToList();
        var scenarios = options.ScenarioFilter?.ToList()
            ?? Enum.GetValues<FuzzerScenarioKind>().ToList();

        var satietyGrid = options.CoarseGrid ? SatietyCoarse : SatietyFine;
        var healthGrid  = options.CoarseGrid ? HealthCoarse  : HealthFine;
        var energyGrid  = options.CoarseGrid ? EnergyCoarse  : EnergyFine;
        var moraleGrid  = options.CoarseGrid ? MoraleCoarse  : MoraleFine;

        var domain  = new IslandDomainPack();
        var results = new List<PressureSample>();
        // Deterministic RNG: rolling dice during GenerateCandidates (skill checks etc.)
        // uses this seeded random, keeping the fuzzer fully reproducible.
        var rng = new Random(42);

        foreach (var actorName in actorNames)
        {
            if (!Archetypes.All.TryGetValue(actorName, out var archetypeData))
                throw new ArgumentException($"Unknown actor: {actorName}. Available: {string.Join(", ", Archetypes.All.Keys)}");

            var actorId = new ActorId(actorName);

            foreach (var scenario in scenarios)
            foreach (var satiety in satietyGrid)
            foreach (var health  in healthGrid)
            foreach (var energy  in energyGrid)
            foreach (var morale  in moraleGrid)
            {
                results.Add(SampleState(
                    domain, actorId, actorName, archetypeData,
                    scenario, satiety, health, energy, morale,
                    options.TopCandidateCount, rng));
            }
        }

        return results;
    }

    /// <summary>
    /// Serializes the results to JSON and writes them to the specified output path.
    /// </summary>
    public static void WriteJson(List<PressureSample> samples, string outputPath)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        var json = JsonSerializer.Serialize(samples, jsonOptions);
        File.WriteAllText(outputPath, json);
    }

    // ── Core sampling ─────────────────────────────────────────────────────────

    private static PressureSample SampleState(
        IslandDomainPack domain,
        ActorId actorId,
        string actorName,
        Dictionary<string, object> archetypeData,
        FuzzerScenarioKind scenario,
        double satiety, double health, double energy, double morale,
        int topN, Random rng)
    {
        // Build actor state from archetype with overridden stats.
        var stateData = new Dictionary<string, object>(archetypeData)
        {
            ["satiety"] = satiety,
            ["energy"]  = energy,
            ["morale"]  = morale
        };
        var actorState = (IslandActorState)domain.CreateActorState(actorId, stateData);
        actorState.Health = health;

        // Build scenario world state.
        var worldState = PressureFuzzerScenarios.Build(scenario, actorId);

        // ── Production logic calls ─────────────────────────────────────────────
        // GenerateCandidates uses the production BuildQualityModel + candidate generators.
        var candidates = domain.GenerateCandidates(
            actorId, actorState, worldState, 0L, rng,
            NullResourceAvailability.Instance);

        // Sort by score descending to get the production ranking.
        var sorted = candidates.OrderByDescending(c => c.Score).ToList();

        // ExplainCandidateScoring returns effective quality weights and per-candidate breakdowns.
        var explain = domain.ExplainCandidateScoring(actorId, actorState, worldState, 0L, sorted);

        // ── Extract results ────────────────────────────────────────────────────
        var qualityWeights = ExtractQualityWeights(explain);

        var topCandidates = sorted
            .Take(topN)
            .Select(c => new TopCandidateInfo(c.Action.Id.Value, Math.Round(c.Score, 4)))
            .ToList();

        var foodContext = ComputeFoodContext(actorState, worldState, actorId);
        var flags = ComputeFlags(actorState, sorted, qualityWeights, foodContext);

        return new PressureSample(
            actorName,
            scenario.ToString(),
            new ActorStatSnapshot(satiety, health, energy, morale),
            foodContext,
            qualityWeights,
            topCandidates,
            flags);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, double> ExtractQualityWeights(Dictionary<string, object>? explain)
    {
        var weights = new Dictionary<string, double>();
        if (explain == null)
            return weights;

        if (!explain.TryGetValue("effectiveWeights", out var raw) ||
            raw is not Dictionary<string, object> weightsDict)
            return weights;

        foreach (var (k, v) in weightsDict)
        {
            weights[k] = v is double d
                ? Math.Round(d, 4)
                : Math.Round(Convert.ToDouble(v), 4);
        }
        return weights;
    }

    private static FoodContextInfo ComputeFoodContext(
        IslandActorState actor, IslandWorldState world, ActorId actorId)
    {
        var piles = world.GetAccessiblePiles(actorId);
        var immediateFood = piles
            .SelectMany(p => p.Supplies.OfType<IEdibleSupply>())
            .Sum(e => e.GetImmediateFoodUnits(actor, world));

        var acquirableFood = world.WorldItems
            .OfType<IFoodSource>()
            .Sum(s => s.GetAcquirableFoodUnits(actor, world));

        return new FoodContextInfo(immediateFood > 0.0, acquirableFood > 0.0);
    }

    private static FuzzerFlags ComputeFlags(
        IslandActorState actor,
        IReadOnlyList<ActionCandidate> sortedCandidates,
        Dictionary<string, double> qualityWeights,
        FoodContextInfo foodContext)
    {
        var topCandidate = sortedCandidates.Count > 0 ? sortedCandidates[0] : null;

        // criticalState: dangerously low satiety or health
        var criticalState =
            actor.Satiety < CriticalSatietyThreshold ||
            actor.Health  < CriticalHealthThreshold;

        // foodAvailableButNotChosen: food in pile but top action is not eating
        var topIsFoodConsumption = topCandidate?.Qualities
            .TryGetValue(QualityType.FoodConsumption, out var fcv) == true && fcv > 0.0;
        var foodAvailableButNotChosen =
            actor.Satiety < CriticalSatietyThreshold &&
            foodContext.ImmediateFoodAvailable &&
            !topIsFoodConsumption;

        // noFoodButNoAcquisition: no food in pile, and top-N doesn't include any acquisition action
        var topNIncludesAcquisition = sortedCandidates
            .Take(5)
            .Any(c => c.Qualities.TryGetValue(QualityType.FoodAcquisition, out var fav) && fav > 0.0);
        var noFoodButNoAcquisition =
            actor.Satiety < CriticalSatietyThreshold &&
            !foodContext.ImmediateFoodAvailable &&
            !topNIncludesAcquisition;

        // prepDominatesFood: Preparation weight beats FoodConsumption when hungry
        qualityWeights.TryGetValue("Preparation",     out var prepWeight);
        qualityWeights.TryGetValue("FoodConsumption", out var foodConsWeight);
        var prepDominatesFood =
            actor.Satiety < CriticalSatietyThreshold &&
            prepWeight > foodConsWeight;

        // bedLoopRisk: a rest-quality action tops the list while food pressure is high.
        // Uses the Rest quality on the candidate rather than action-name string matching
        // so that any future rest-providing action (e.g. hammock) is caught automatically.
        var topIsRestAction = topCandidate?.Qualities
            .TryGetValue(QualityType.Rest, out var restVal) == true && restVal >= 0.4;
        var bedLoopRisk =
            actor.Satiety < FoodPressureThreshold &&
            topIsRestAction;

        return new FuzzerFlags(
            criticalState,
            foodAvailableButNotChosen,
            noFoodButNoAcquisition,
            prepDominatesFood,
            bedLoopRisk);
    }

    // ── Null IResourceAvailability stub ──────────────────────────────────────

    /// <summary>
    /// Minimal IResourceAvailability implementation for the pressure fuzzer.
    /// Reports all resources as unreserved so every candidate action is eligible.
    /// </summary>
    private sealed class NullResourceAvailability : IResourceAvailability
    {
        public static readonly NullResourceAvailability Instance = new();
        public bool IsReserved(ResourceId _) => false;
        public bool TryReserve(ResourceId _, string __, long ___) => true;
        public void Release(ResourceId _) { }
    }
}
