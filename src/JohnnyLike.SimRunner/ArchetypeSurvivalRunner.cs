using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JohnnyLike.SimRunner;

// ── Options ───────────────────────────────────────────────────────────────────

public record ArchetypeSurvivalOptions(
    IReadOnlyList<string> Actors,
    int RunsPerActor,
    double DurationSeconds,
    int BaseSeed,
    string OutputDirectory,
    bool SaveTraces = false
);

// ── Per-run result ─────────────────────────────────────────────────────────────

public class ArchetypeSurvivalRunResult
{
    public string Actor { get; set; } = "";
    public int Seed { get; set; }
    public double ConfiguredDurationSeconds { get; set; }
    public double FinalSimTimeSeconds { get; set; }
    public bool SurvivedToEnd { get; set; }
    public double? DeathTimeSeconds { get; set; }
    public double SurvivalTimeSeconds { get; set; }

    // Optional compact final-state snapshot
    public double? Health { get; set; }
    public double? Satiety { get; set; }
    public double? Energy { get; set; }
    public double? Morale { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AlivenessState? FinalAlivenessState { get; set; }
}

// ── Per-archetype summary entry ───────────────────────────────────────────────

public class ArchetypeSurvivalSummaryEntry
{
    public string Actor { get; set; } = "";
    public int RunCount { get; set; }
    public int SurvivedToEndCount { get; set; }
    public double SurvivedToEndRate { get; set; }
    public double MeanSurvivalTimeSeconds { get; set; }
    public double MedianSurvivalTimeSeconds { get; set; }
    public double StddevSurvivalTimeSeconds { get; set; }
    public double MinSurvivalTimeSeconds { get; set; }
    public double MaxSurvivalTimeSeconds { get; set; }
    public double P25SurvivalTimeSeconds { get; set; }
    public double P75SurvivalTimeSeconds { get; set; }
}

// ── Summary wrapper ───────────────────────────────────────────────────────────

public class ArchetypeSurvivalSummary
{
    public double ConfiguredDurationSeconds { get; set; }
    public int RunsPerActor { get; set; }
    public int BaseSeed { get; set; }
    public string Timestamp { get; set; } = "";
    public List<ArchetypeSurvivalSummaryEntry> Archetypes { get; set; } = new();
}

// ── Runner ────────────────────────────────────────────────────────────────────

public static class ArchetypeSurvivalRunner
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Run(ArchetypeSurvivalOptions options)
    {
        Directory.CreateDirectory(options.OutputDirectory);

        Console.WriteLine("=== ARCHETYPE SURVIVAL STUDY ===");
        Console.WriteLine($"Duration:         {options.DurationSeconds}s");
        Console.WriteLine($"Runs per actor:   {options.RunsPerActor}");
        Console.WriteLine($"Base seed:        {options.BaseSeed}");
        Console.WriteLine($"Actors:           {string.Join(", ", options.Actors)}");
        Console.WriteLine($"Output:           {options.OutputDirectory}");
        Console.WriteLine();

        var allRuns = new List<ArchetypeSurvivalRunResult>();
        var actorIndex = 0;

        foreach (var actorName in options.Actors)
        {
            if (!Archetypes.All.TryGetValue(actorName, out var archetypeData))
            {
                Console.Error.WriteLine($"Warning: unknown actor '{actorName}', skipping.");
                continue;
            }

            actorIndex++;
            Console.Write($"[{actorIndex}/{options.Actors.Count}] {actorName,-12}  ");

            for (int run = 0; run < options.RunsPerActor; run++)
            {
                var seed = options.BaseSeed + actorIndex * 10000 + run;
                var runResult = RunOne(actorName, archetypeData, seed, options.DurationSeconds, options.SaveTraces, options.OutputDirectory);
                allRuns.Add(runResult);

                Console.Write(runResult.SurvivedToEnd ? "." : "x");
            }

            Console.WriteLine();
        }

        // Write raw run artifact
        var runsPath = Path.Combine(options.OutputDirectory, "survival-runs.json");
        File.WriteAllText(runsPath, JsonSerializer.Serialize(allRuns, JsonOpts));
        Console.WriteLine($"\n✓ Run results written to {runsPath}");

        // Build & write summary
        var summary = BuildSummary(allRuns, options);
        var summaryPath = Path.Combine(options.OutputDirectory, "survival-summary.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOpts));
        Console.WriteLine($"✓ Summary written to {summaryPath}");

        // Print console ranking table
        PrintRankingTable(summary);
    }

    // ── Single-run simulation ──────────────────────────────────────────────────

    private static ArchetypeSurvivalRunResult RunOne(
        string actorName,
        Dictionary<string, object> archetypeData,
        int seed,
        double durationSeconds,
        bool saveTraces,
        string outputDirectory)
    {
        var result = new ArchetypeSurvivalRunResult
        {
            Actor = actorName,
            Seed = seed,
            ConfiguredDurationSeconds = durationSeconds
        };

        var domainPack = new IslandDomainPack();
        var traceSink = saveTraces ? (ITraceSink)new InMemoryTraceSink() : JohnnyLike.Domain.Abstractions.NullTraceSink.Instance;
        var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink);

        var actorId = new ActorId(actorName);
        engine.AddActor(actorId, archetypeData);

        var executor = new FakeExecutor(engine);
        var timeStep = 0.5;
        var elapsed = 0.0;

        while (elapsed < durationSeconds)
        {
            executor.Update(timeStep);
            elapsed += timeStep;

            // Inspect real actor aliveness state
            if (engine.Actors.TryGetValue(actorId, out var actorState) &&
                actorState is LivingActorState living)
            {
                var alivenessBuff = living.TryGetBuff<AlivenessBuff>();
                if (alivenessBuff?.State == AlivenessState.Dead)
                {
                    // Actor is dead — record the death time and stop
                    result.SurvivedToEnd = false;
                    result.DeathTimeSeconds = engine.CurrentSeconds;
                    result.SurvivalTimeSeconds = engine.CurrentSeconds;
                    result.FinalSimTimeSeconds = engine.CurrentSeconds;

                    // Snapshot final state
                    result.Health = living.Health;
                    result.Satiety = living.Satiety;
                    result.Energy = living.Energy;
                    result.Morale = living.Morale;
                    result.FinalAlivenessState = AlivenessState.Dead;

                    if (saveTraces)
                        SaveTrace(actorName, seed, outputDirectory, traceSink);

                    return result;
                }
            }
        }

        // Actor survived to the horizon
        result.SurvivedToEnd = true;
        result.DeathTimeSeconds = null;
        result.SurvivalTimeSeconds = durationSeconds;
        result.FinalSimTimeSeconds = engine.CurrentSeconds;

        if (engine.Actors.TryGetValue(actorId, out var finalState) && finalState is LivingActorState finalLiving)
        {
            result.Health = finalLiving.Health;
            result.Satiety = finalLiving.Satiety;
            result.Energy = finalLiving.Energy;
            result.Morale = finalLiving.Morale;
            var buff = finalLiving.TryGetBuff<AlivenessBuff>();
            result.FinalAlivenessState = buff?.State;
        }

        if (saveTraces)
            SaveTrace(actorName, seed, outputDirectory, traceSink);

        return result;
    }

    private static void SaveTrace(string actorName, int seed, string outputDirectory, ITraceSink traceSink)
    {
        if (traceSink is not InMemoryTraceSink inMemory) return;

        var tracesDir = Path.Combine(outputDirectory, "traces");
        Directory.CreateDirectory(tracesDir);
        var tracePath = Path.Combine(tracesDir, $"trace-{actorName}-seed{seed}.txt");

        using var w = new StreamWriter(tracePath);
        foreach (var evt in inMemory.GetEvents())
            w.WriteLine(evt.ToString());
    }

    // ── Summary aggregation ────────────────────────────────────────────────────

    private static ArchetypeSurvivalSummary BuildSummary(
        List<ArchetypeSurvivalRunResult> runs,
        ArchetypeSurvivalOptions options)
    {
        var entriesByActor = runs
            .GroupBy(r => r.Actor)
            .Select(g =>
            {
                var times = g.Select(r => r.SurvivalTimeSeconds).OrderBy(t => t).ToList();
                var survivedCount = g.Count(r => r.SurvivedToEnd);
                var mean = times.Average();
                var stddev = ComputeStddev(times, mean);

                return new ArchetypeSurvivalSummaryEntry
                {
                    Actor = g.Key,
                    RunCount = times.Count,
                    SurvivedToEndCount = survivedCount,
                    SurvivedToEndRate = (double)survivedCount / times.Count,
                    MeanSurvivalTimeSeconds = mean,
                    MedianSurvivalTimeSeconds = Percentile(times, 0.5),
                    StddevSurvivalTimeSeconds = stddev,
                    MinSurvivalTimeSeconds = times[0],
                    MaxSurvivalTimeSeconds = times[^1],
                    P25SurvivalTimeSeconds = Percentile(times, 0.25),
                    P75SurvivalTimeSeconds = Percentile(times, 0.75)
                };
            })
            .OrderByDescending(e => e.SurvivedToEndRate)
            .ThenByDescending(e => e.MedianSurvivalTimeSeconds)
            .ThenByDescending(e => e.MeanSurvivalTimeSeconds)
            .ToList();

        return new ArchetypeSurvivalSummary
        {
            ConfiguredDurationSeconds = options.DurationSeconds,
            RunsPerActor = options.RunsPerActor,
            BaseSeed = options.BaseSeed,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Archetypes = entriesByActor
        };
    }

    private static double ComputeStddev(IReadOnlyList<double> values, double mean)
    {
        if (values.Count <= 1) return 0.0;
        var variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    private static double Percentile(IList<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0.0;
        if (sorted.Count == 1) return sorted[0];

        var index = p * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
    }

    // ── Console output ─────────────────────────────────────────────────────────

    private static void PrintRankingTable(ArchetypeSurvivalSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine("=== ARCHETYPE SURVIVAL SUMMARY ===");
        Console.WriteLine($"Duration: {summary.ConfiguredDurationSeconds}s");
        Console.WriteLine($"Runs per archetype: {summary.RunsPerActor}");
        Console.WriteLine();

        const int rankW    = 4;
        const int actorW   = 12;
        const int survW    = 9;
        const int meanW    = 10;
        const int medW     = 10;
        const int stdW     = 10;
        const int minW     = 9;
        const int maxW     = 9;

        var header = $"{"Rank",rankW}  {"Actor",-actorW}  {"Survive%",survW}  {"Mean(s)",meanW}  {"Median(s)",medW}  {"StdDev(s)",stdW}  {"Min(s)",minW}  {"Max(s)",maxW}";
        var sep    = $"{"----",rankW}  {"----------",-actorW}  {"---------",survW}  {"--------",meanW}  {"----------",medW}  {"----------",stdW}  {"-------",minW}  {"-------",maxW}";

        Console.WriteLine(header);
        Console.WriteLine(sep);

        var rank = 1;
        foreach (var e in summary.Archetypes)
        {
            var survPct = $"{e.SurvivedToEndRate * 100.0:F1}%";
            Console.WriteLine(
                $"{rank,rankW}  {e.Actor,-actorW}  {survPct,survW}  {e.MeanSurvivalTimeSeconds,meanW:F0}  {e.MedianSurvivalTimeSeconds,medW:F0}  {e.StddevSurvivalTimeSeconds,stdW:F0}  {e.MinSurvivalTimeSeconds,minW:F0}  {e.MaxSurvivalTimeSeconds,maxW:F0}");
            rank++;
        }

        Console.WriteLine();
    }
}


