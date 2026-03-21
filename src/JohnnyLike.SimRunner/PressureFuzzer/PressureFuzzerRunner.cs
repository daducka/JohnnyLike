using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Recipes;
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

/// <summary>
/// Tags the plausibility of a sampled state: terminal states (dead actor),
/// extreme states (multiple stats near zero), and ordinary gameplay states.
/// Terminal/extreme rows are included in output but should be interpreted carefully.
/// </summary>
public record StatePlausibility(
    bool IsTerminalState,
    bool IsExtremeState,
    bool IsPlausibleGameplayState);

/// <summary>
/// Concrete world facts derived from the built scenario.
/// Included per-row so the output is self-contained and the scenario setup
/// can be verified externally without reading source code.
/// </summary>
public record ScenarioMetadata(
    bool BedAvailable,
    bool BedDamaged,
    bool CampfireAvailable,
    bool CampfireDamaged,
    double EdibleFoodCount,
    int AcquirableFoodSourceCount,
    double RecipeOpportunityScore);

/// <summary>
/// Per-quality score decomposition for a single candidate action.
/// </summary>
public record QualityContribution(
    double QualityValue,
    double EffectiveWeight,
    double Contribution);

/// <summary>
/// Scored candidate with full decomposition of how the score was reached.
/// </summary>
public record TopCandidateInfo(
    string Action,
    double Score,
    double IntrinsicScore,
    IReadOnlyDictionary<string, QualityContribution> QualityContributions);

public record FuzzerFlags(
    bool CriticalState,
    bool FoodAvailableButNotChosen,
    bool NoFoodButNoAcquisition,
    bool PrepDominatesFood,
    bool BedLoopRisk,
    bool DirectFoodActionPresentButLost,
    bool ComfortBeatFood,
    bool SafetyBeatFood,
    bool PersonalityCollapseRisk);

public record PressureSample(
    string Actor,
    string Scenario,
    ActorStatSnapshot State,
    StatePlausibility Plausibility,
    ScenarioMetadata ScenarioInfo,
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

    // ── State classification thresholds ──────────────────────────────────────
    private const double TerminalHealthThreshold = 0.0;
    private const double ExtremeStatThreshold    = 10.0;
    private const int    ExtremeStatMinCount     = 3;

    // ── PersonalityCollapseRisk threshold (fraction of actors sharing top action) ─
    private const double PersonalityCollapseThreshold = 0.67;

    // ── ComfortBeatFood / SafetyBeatFood quality thresholds ──────────────────
    private const double ComfortQualityThreshold = 0.3;
    private const double SafetyQualityThreshold  = 0.2;

    // ── Scenario metadata quality thresholds ─────────────────────────────────
    private const double BedDamagedQualityThreshold      = 80.0;
    private const double CampfireDamagedQualityThreshold = 50.0;

    /// <summary>
    /// Runs the full pressure fuzzer grid sampling and returns the results.
    /// PersonalityCollapseRisk is computed in a post-processing pass after all
    /// samples are generated, since it requires cross-actor comparison.
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

        // ── Post-processing: PersonalityCollapseRisk ──────────────────────────
        // Group samples by (scenario, state) and flag states where >= 67% of actors
        // agree on the same top action — indicating personality isn't differentiating.
        var collapseKeys = new HashSet<(string, double, double, double, double)>();
        foreach (var group in results.GroupBy(s =>
            (s.Scenario, s.State.Satiety, s.State.Health, s.State.Energy, s.State.Morale)))
        {
            var items = group.ToList();
            if (items.Count < 2) continue;

            var maxSameAction = items
                .Select(s => s.TopCandidates.Count > 0 ? s.TopCandidates[0].Action : "")
                .GroupBy(a => a)
                .Max(g => g.Count());

            if (maxSameAction >= Math.Ceiling(items.Count * PersonalityCollapseThreshold))
                collapseKeys.Add(group.Key);
        }

        for (int i = 0; i < results.Count; i++)
        {
            var s = results[i];
            if (collapseKeys.Contains((s.Scenario, s.State.Satiety, s.State.Health, s.State.Energy, s.State.Morale)))
                results[i] = s with { Flags = s.Flags with { PersonalityCollapseRisk = true } };
        }

        return results;
    }

    // ── JSON writers ─────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Serializes all samples to the primary JSON output file.
    /// </summary>
    public static void WriteJson(List<PressureSample> samples, string outputPath)
    {
        File.WriteAllText(outputPath, JsonSerializer.Serialize(samples, JsonOpts));
    }

    /// <summary>
    /// Derives the summary path from the primary output path
    /// (e.g. <c>fuzzer-output.json</c> → <c>fuzzer-output.summary.json</c>).
    /// </summary>
    public static string DeriveSummaryPath(string outputPath)
    {
        var dir  = Path.GetDirectoryName(outputPath) ?? "";
        var stem = Path.GetFileNameWithoutExtension(outputPath);
        return Path.Combine(dir, $"{stem}.summary.json");
    }

    /// <summary>
    /// Writes a companion summary file with aggregated flag counts, top-action
    /// distributions, dominant quality breakdowns, and crossover statistics.
    /// </summary>
    public static void WriteSummaryJson(List<PressureSample> samples, string summaryPath)
    {
        // ── Flag counts ────────────────────────────────────────────────────────
        var flagCounts = new Dictionary<string, int>
        {
            ["criticalState"]                 = samples.Count(s => s.Flags.CriticalState),
            ["foodAvailableButNotChosen"]      = samples.Count(s => s.Flags.FoodAvailableButNotChosen),
            ["noFoodButNoAcquisition"]         = samples.Count(s => s.Flags.NoFoodButNoAcquisition),
            ["prepDominatesFood"]              = samples.Count(s => s.Flags.PrepDominatesFood),
            ["bedLoopRisk"]                    = samples.Count(s => s.Flags.BedLoopRisk),
            ["directFoodActionPresentButLost"] = samples.Count(s => s.Flags.DirectFoodActionPresentButLost),
            ["comfortBeatFood"]                = samples.Count(s => s.Flags.ComfortBeatFood),
            ["safetyBeatFood"]                 = samples.Count(s => s.Flags.SafetyBeatFood),
            ["personalityCollapseRisk"]        = samples.Count(s => s.Flags.PersonalityCollapseRisk)
        };

        // ── Top-action distribution by actor × scenario ────────────────────────
        // Shows which actions dominate each (actor, scenario) pairing.
        var topActionDist = new Dictionary<string, IReadOnlyList<object>>();
        foreach (var group in samples.GroupBy(s => $"{s.Actor}|{s.Scenario}"))
        {
            var actionCounts = group
                .Where(s => s.TopCandidates.Count > 0)
                .GroupBy(s => s.TopCandidates[0].Action)
                .Select(g => new { action = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(5)
                .Select(x => (object)new Dictionary<string, object>
                    { ["action"] = x.action, ["count"] = x.count })
                .ToList();
            topActionDist[group.Key] = actionCounts;
        }

        // ── Dominant quality by actor × scenario ──────────────────────────────
        // The quality with the highest average effective weight per (actor, scenario).
        var dominantQuality = new Dictionary<string, string>();
        foreach (var group in samples.GroupBy(s => $"{s.Actor}|{s.Scenario}"))
        {
            var avgWeights = new Dictionary<string, double>();
            foreach (var s in group)
            {
                foreach (var (q, w) in s.QualityWeights)
                {
                    avgWeights.TryGetValue(q, out var cur);
                    avgWeights[q] = cur + w;
                }
            }
            var count = group.Count();
            if (count > 0 && avgWeights.Count > 0)
            {
                var dominant = avgWeights.MaxBy(kv => kv.Value / count);
                dominantQuality[group.Key] = dominant.Key;
            }
        }

        // ── Crossover statistics ───────────────────────────────────────────────
        var crossoverStats = new Dictionary<string, int>
        {
            ["foodConsumptionGtSafety"] = samples.Count(s =>
            {
                s.QualityWeights.TryGetValue("FoodConsumption", out var f);
                s.QualityWeights.TryGetValue("Safety",          out var sf);
                return f > sf;
            }),
            ["foodConsumptionGtRest"] = samples.Count(s =>
            {
                s.QualityWeights.TryGetValue("FoodConsumption", out var f);
                s.QualityWeights.TryGetValue("Rest",            out var r);
                return f > r;
            }),
            ["safetyGtFoodConsumption"] = samples.Count(s =>
            {
                s.QualityWeights.TryGetValue("FoodConsumption", out var f);
                s.QualityWeights.TryGetValue("Safety",          out var sf);
                return sf > f;
            }),
            ["restGtFoodConsumption"] = samples.Count(s =>
            {
                s.QualityWeights.TryGetValue("FoodConsumption", out var f);
                s.QualityWeights.TryGetValue("Rest",            out var r);
                return r > f;
            })
        };

        var summary = new
        {
            generatedAt            = DateTime.UtcNow.ToString("o"),
            totalSamples           = samples.Count,
            terminalStateSamples   = samples.Count(s => s.Plausibility.IsTerminalState),
            extremeStateSamples    = samples.Count(s => s.Plausibility.IsExtremeState),
            plausibleSamples       = samples.Count(s => s.Plausibility.IsPlausibleGameplayState),
            flagCounts,
            topActionDistribution  = topActionDist,
            dominantQualityByActorScenario = dominantQuality,
            crossoverStats
        };

        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOpts));
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
        var topCandidates  = ExtractCandidateDecompositions(explain, sorted, topN);
        var foodContext    = ComputeFoodContext(actorState, worldState, actorId);
        var plausibility   = ComputePlausibility(actorState);
        var scenarioMeta   = ComputeScenarioMetadata(worldState, actorState, actorId);
        var flags          = ComputeFlags(actorState, sorted, qualityWeights, foodContext);

        return new PressureSample(
            actorName,
            scenario.ToString(),
            new ActorStatSnapshot(satiety, health, energy, morale),
            plausibility,
            scenarioMeta,
            foodContext,
            qualityWeights,
            topCandidates,
            flags);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StatePlausibility ComputePlausibility(IslandActorState actor)
    {
        var isTerminal  = actor.Health <= TerminalHealthThreshold;
        // An extreme state has 3+ stats near zero, regardless of whether health is also
        // at the terminal threshold — terminal states may simultaneously be extreme.
        // Consumers can use isTerminalState to further filter if needed.
        var extremeCount = new[] { actor.Satiety, actor.Health, actor.Energy, actor.Morale }
            .Count(v => v < ExtremeStatThreshold);
        var isExtreme   = extremeCount >= ExtremeStatMinCount;
        return new StatePlausibility(isTerminal, isExtreme, !isTerminal && !isExtreme);
    }

    private static ScenarioMetadata ComputeScenarioMetadata(
        IslandWorldState world, IslandActorState actor, ActorId actorId)
    {
        var bed      = world.WorldItems.OfType<PalmFrondBedItem>().FirstOrDefault();
        var campfire = world.MainCampfire;

        var piles = world.GetAccessiblePiles(actorId);
        var edibleFoodCount = piles
            .SelectMany(p => p.Supplies.OfType<IEdibleSupply>())
            .Sum(e => e.GetImmediateFoodUnits(actor, world));

        var acquirableSources = world.WorldItems.OfType<IFoodSource>().Count();

        // Recipe opportunity score: sum of BaseChance for currently discoverable recipes.
        var recipeScore = IslandRecipeRegistry.All.Values
            .Where(r => r.Discovery != null &&
                        r.Discovery.Trigger == DiscoveryTrigger.ThinkAboutSupplies &&
                        !actor.KnownRecipeIds.Contains(r.Id) &&
                        r.Discovery.CanDiscover(actor, world))
            .Sum(r => r.Discovery!.BaseChance);

        return new ScenarioMetadata(
            BedAvailable:            bed != null,
            BedDamaged:              bed != null && bed.Quality < BedDamagedQualityThreshold,
            CampfireAvailable:       campfire != null,
            CampfireDamaged:         campfire != null && (!campfire.IsLit || campfire.Quality < CampfireDamagedQualityThreshold),
            EdibleFoodCount:         Math.Round(edibleFoodCount, 2),
            AcquirableFoodSourceCount: acquirableSources,
            RecipeOpportunityScore:  Math.Round(recipeScore, 4));
    }

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

    /// <summary>
    /// Extracts per-candidate score decompositions from the ExplainCandidateScoring output.
    /// Matches candidates by action ID so ordering differences don't cause mismatches.
    /// </summary>
    private static IReadOnlyList<TopCandidateInfo> ExtractCandidateDecompositions(
        Dictionary<string, object>? explain,
        IReadOnlyList<ActionCandidate> sorted,
        int topN)
    {
        // Build lookup from explain's candidateBreakdowns by actionId.
        var breakdownByAction = new Dictionary<string, Dictionary<string, object>>();
        if (explain?.TryGetValue("candidateBreakdowns", out var rawBreakdowns) == true &&
            rawBreakdowns is List<object> breakdownList)
        {
            foreach (var item in breakdownList)
            {
                if (item is Dictionary<string, object> bd &&
                    bd.TryGetValue("actionId", out var ai) && ai is string actionId)
                    breakdownByAction[actionId] = bd;
            }
        }

        var result = new List<TopCandidateInfo>();
        foreach (var c in sorted.Take(topN))
        {
            var actionId      = c.Action.Id.Value;
            var intrinsicScore = c.IntrinsicScore;
            var contributions  = new Dictionary<string, QualityContribution>();

            if (breakdownByAction.TryGetValue(actionId, out var bd))
            {
                if (bd.TryGetValue("intrinsicScore", out var isObj) && isObj is double isd)
                    intrinsicScore = Math.Round(isd, 4);

                if (bd.TryGetValue("qualityContributions", out var qcObj) &&
                    qcObj is Dictionary<string, object> qcDict)
                {
                    foreach (var (q, v) in qcDict)
                    {
                        if (v is Dictionary<string, object> contribDict)
                        {
                            contribDict.TryGetValue("qualityValue",    out var qv);
                            contribDict.TryGetValue("effectiveWeight", out var ew);
                            contribDict.TryGetValue("contribution",    out var con);
                            contributions[q] = new QualityContribution(
                                Math.Round(Convert.ToDouble(qv),  4),
                                Math.Round(Convert.ToDouble(ew),  4),
                                Math.Round(Convert.ToDouble(con), 4));
                        }
                    }
                }
            }

            result.Add(new TopCandidateInfo(
                actionId, Math.Round(c.Score, 4), Math.Round(intrinsicScore, 4), contributions));
        }

        return result;
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

        // ── criticalState ────────────────────────────────────────────────────
        var criticalState =
            actor.Satiety < CriticalSatietyThreshold ||
            actor.Health  < CriticalHealthThreshold;

        // ── foodAvailableButNotChosen ────────────────────────────────────────
        // Food in pile; hungry; top action isn't a food consumption action.
        var topIsFoodConsumption = topCandidate?.Qualities
            .TryGetValue(QualityType.FoodConsumption, out var fcv) == true && fcv > 0.0;
        var foodAvailableButNotChosen =
            actor.Satiety < CriticalSatietyThreshold &&
            foodContext.ImmediateFoodAvailable &&
            !topIsFoodConsumption;

        // ── noFoodButNoAcquisition ───────────────────────────────────────────
        // Starving, no supply, and top-N contains no acquisition action.
        var topNIncludesAcquisition = sortedCandidates
            .Take(5)
            .Any(c => c.Qualities.TryGetValue(QualityType.FoodAcquisition, out var fav) && fav > 0.0);
        var noFoodButNoAcquisition =
            actor.Satiety < CriticalSatietyThreshold &&
            !foodContext.ImmediateFoodAvailable &&
            !topNIncludesAcquisition;

        // ── prepDominatesFood ────────────────────────────────────────────────
        qualityWeights.TryGetValue("Preparation",     out var prepWeight);
        qualityWeights.TryGetValue("FoodConsumption", out var foodConsWeight);
        var prepDominatesFood =
            actor.Satiety < CriticalSatietyThreshold &&
            prepWeight > foodConsWeight;

        // ── bedLoopRisk ──────────────────────────────────────────────────────
        // Quality-based: avoids fragile string matching on action IDs.
        var topIsRestAction = topCandidate?.Qualities
            .TryGetValue(QualityType.Rest, out var restVal) == true && restVal >= 0.4;
        var bedLoopRisk =
            actor.Satiety < FoodPressureThreshold &&
            topIsRestAction;

        // ── directFoodActionPresentButLost ───────────────────────────────────
        // A food consumption action actually exists in candidates but isn't chosen.
        // Differs from foodAvailableButNotChosen: requires the eat action to be
        // generated (not just food being in supply).
        var anyFoodConsumptionCandidate = sortedCandidates
            .Any(c => c.Qualities.TryGetValue(QualityType.FoodConsumption, out var v) && v > 0.0);
        var directFoodActionPresentButLost =
            actor.Satiety < CriticalSatietyThreshold &&
            anyFoodConsumptionCandidate &&
            !topIsFoodConsumption;

        // ── comfortBeatFood ──────────────────────────────────────────────────
        // Top action is primarily a Comfort action while the actor is hungry
        // and immediate food is available.
        var topIsComfortAction = topCandidate?.Qualities
            .TryGetValue(QualityType.Comfort, out var comfortVal) == true &&
            comfortVal >= ComfortQualityThreshold;
        var comfortBeatFood =
            actor.Satiety < CriticalSatietyThreshold &&
            foodContext.ImmediateFoodAvailable &&
            topIsComfortAction;

        // ── safetyBeatFood ───────────────────────────────────────────────────
        // Top action carries significant Safety quality while the actor is hungry
        // and immediate food is available.
        var topIsSafetyAction = topCandidate?.Qualities
            .TryGetValue(QualityType.Safety, out var safetyVal) == true &&
            safetyVal >= SafetyQualityThreshold;
        var safetyBeatFood =
            actor.Satiety < CriticalSatietyThreshold &&
            foodContext.ImmediateFoodAvailable &&
            topIsSafetyAction;

        return new FuzzerFlags(
            criticalState,
            foodAvailableButNotChosen,
            noFoodButNoAcquisition,
            prepDominatesFood,
            bedLoopRisk,
            directFoodActionPresentButLost,
            comfortBeatFood,
            safetyBeatFood,
            PersonalityCollapseRisk: false  // filled in post-processing pass in Run()
        );
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

