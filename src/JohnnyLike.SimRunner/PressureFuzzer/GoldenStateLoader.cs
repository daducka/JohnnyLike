using JohnnyLike.Domain.Abstractions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JohnnyLike.SimRunner.PressureFuzzer;

/// <summary>
/// Loads and validates the curated <c>golden-states.json</c> dataset.
///
/// <para>
/// Three entry points are provided:
/// <list type="bullet">
///   <item><see cref="LoadFromFile"/> — load from a file path</item>
///   <item><see cref="LoadFromJson"/> — parse from a JSON string</item>
///   <item><see cref="LoadEmbedded"/> — load the bundled <c>golden-states.json</c> resource</item>
/// </list>
/// All three paths run the same <see cref="Validate"/> logic and throw
/// <see cref="GoldenStateValidationException"/> on the first schema violation detected.
/// </para>
/// </summary>
public static class GoldenStateLoader
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> _validCategories =
        Enum.GetNames<QualityType>().ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> _validScenarios =
        Enum.GetNames<FuzzerScenarioKind>().ToHashSet(StringComparer.Ordinal);

    // ── Public entry points ───────────────────────────────────────────────

    /// <summary>
    /// Loads the bundled <c>golden-states.json</c> that is embedded as a resource inside
    /// <c>JohnnyLike.SimRunner</c>.  Use this as the default production path.
    /// </summary>
    public static IReadOnlyList<GoldenStateEntry> LoadEmbedded()
    {
        var assembly = typeof(GoldenStateLoader).Assembly;
        // Resource name: default namespace + folder path with '.' separators.
        const string resourceName =
            "JohnnyLike.SimRunner.PressureFuzzer.golden-states.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found in assembly '{assembly.FullName}'. " +
                "Ensure golden-states.json has build action EmbeddedResource.");

        using var reader = new StreamReader(stream);
        return LoadFromJson(reader.ReadToEnd());
    }

    /// <summary>
    /// Loads golden states from the JSON file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Absolute or relative path to a <c>golden-states.json</c> file.</param>
    public static IReadOnlyList<GoldenStateEntry> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"golden-states file not found: '{path}'", path);

        return LoadFromJson(File.ReadAllText(path));
    }

    /// <summary>
    /// Parses and validates golden states from a JSON string.
    /// </summary>
    public static IReadOnlyList<GoldenStateEntry> LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new GoldenStateValidationException("JSON input is empty or whitespace.");

        List<GoldenStateEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<GoldenStateEntry>>(json, _jsonOpts);
        }
        catch (JsonException ex)
        {
            throw new GoldenStateValidationException(
                $"Failed to parse golden-states JSON: {ex.Message}", ex);
        }

        if (entries is null)
            throw new GoldenStateValidationException("golden-states JSON deserialized to null.");

        for (int i = 0; i < entries.Count; i++)
            Validate(entries[i], i);

        return entries.AsReadOnly();
    }

    // ── Validation ────────────────────────────────────────────────────────

    /// <summary>
    /// Validates a single <see cref="GoldenStateEntry"/> and throws
    /// <see cref="GoldenStateValidationException"/> if any field is invalid.
    /// </summary>
    public static void Validate(GoldenStateEntry entry, int index = -1)
    {
        string At(string field) =>
            index >= 0 ? $"Entry[{index}].{field}" : field;

        RequireNonEmpty(entry.SampleKey, At(nameof(entry.SampleKey)));
        RequireNonEmpty(entry.Actor,     At(nameof(entry.Actor)));
        RequireNonEmpty(entry.Scenario,  At(nameof(entry.Scenario)));

        if (!_validScenarios.Contains(entry.Scenario))
            throw new GoldenStateValidationException(
                $"{At(nameof(entry.Scenario))}: '{entry.Scenario}' is not a valid FuzzerScenarioKind. " +
                $"Valid values: {string.Join(", ", _validScenarios)}");

        ValidateState(entry.State, At(nameof(entry.State)));

        if (entry.DesiredOutcome is null)
            throw new GoldenStateValidationException(
                $"{At(nameof(entry.DesiredOutcome))} is required.");

        ValidateDesiredOutcome(entry.DesiredOutcome, At(nameof(entry.DesiredOutcome)));

        if (entry.Priority <= 0)
            throw new GoldenStateValidationException(
                $"{At(nameof(entry.Priority))}: must be > 0, got {entry.Priority}.");
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static void ValidateState(GoldenStateValues state, string path)
    {
        if (state is null)
            throw new GoldenStateValidationException($"{path} is required.");

        ValidateStat(state.Satiety, $"{path}.Satiety");
        ValidateStat(state.Health,  $"{path}.Health");
        ValidateStat(state.Energy,  $"{path}.Energy");
        ValidateStat(state.Morale,  $"{path}.Morale");
    }

    private static void ValidateStat(double value, string path)
    {
        if (value < 0 || value > 100)
            throw new GoldenStateValidationException(
                $"{path}: value {value} is out of range [0, 100].");
    }

    private static void ValidateDesiredOutcome(GoldenStateDesiredOutcome outcome, string path)
    {
        // At least one category constraint must be present.
        bool hasDesired    = !string.IsNullOrWhiteSpace(outcome.DesiredTopCategory);
        bool hasAcceptable = outcome.AcceptableTopCategories?.Count > 0;

        if (!hasDesired && !hasAcceptable)
            throw new GoldenStateValidationException(
                $"{path}: at least one of DesiredTopCategory or AcceptableTopCategories must be provided.");

        if (hasDesired)
            ValidateCategory(outcome.DesiredTopCategory!, $"{path}.DesiredTopCategory");

        if (outcome.AcceptableTopCategories is not null)
            foreach (var cat in outcome.AcceptableTopCategories)
                ValidateCategory(cat, $"{path}.AcceptableTopCategories[]");

        if (outcome.ForbiddenTopCategories is not null)
            foreach (var cat in outcome.ForbiddenTopCategories)
                ValidateCategory(cat, $"{path}.ForbiddenTopCategories[]");
    }

    private static void ValidateCategory(string category, string path)
    {
        if (!_validCategories.Contains(category))
            throw new GoldenStateValidationException(
                $"{path}: '{category}' is not a valid QualityType. " +
                $"Valid values: {string.Join(", ", _validCategories)}");
    }

    private static void RequireNonEmpty(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new GoldenStateValidationException($"{path} is required and must not be empty.");
    }
}

/// <summary>
/// Thrown when a <see cref="GoldenStateEntry"/> fails schema or value validation.
/// </summary>
public sealed class GoldenStateValidationException : Exception
{
    public GoldenStateValidationException(string message) : base(message) { }
    public GoldenStateValidationException(string message, Exception inner) : base(message, inner) { }
}
