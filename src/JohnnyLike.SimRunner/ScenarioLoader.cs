using System.Text.Json;

namespace JohnnyLike.SimRunner;

public record ScenarioActorTask(
    string ActionId,
    string ActionKind,
    Dictionary<string, object> Parameters,
    double Duration
);

public record ScenarioActor(
    string ActorId,
    Dictionary<string, object> InitialState,
    List<ScenarioActorTask>? InitialTasks = null
);

public record ScenarioSignal(
    double AtTime,
    string Type,
    string? TargetActor,
    Dictionary<string, object> Data
);

public record Scenario(
    string Name,
    int Seed,
    double DurationSeconds,
    List<ScenarioActor> Actors,
    List<ScenarioSignal> Signals
);

public static class ScenarioLoader
{
    public static Scenario LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public static Scenario LoadFromJson(string json)
    {
        var scenario = JsonSerializer.Deserialize<Scenario>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return scenario ?? throw new InvalidOperationException("Failed to deserialize scenario");
    }
}
