using JohnnyLike.Narration;

namespace JohnnyLike.Narration.Tests;

public class NarrationResponseParserTests
{
    private static NarrationJob MakeJob(NarrationJobKind kind = NarrationJobKind.Attempt) =>
        new(Guid.NewGuid(), 0, 10, kind, "Alice", "prompt");

    [Fact]
    public void Parse_ValidJson_ReturnsNarration()
    {
        var json = """{"narration":"Alice eyes the fruit cautiously.","updatedSummary":null}""";
        var result = NarrationResponseParser.Parse(json, MakeJob());

        Assert.Equal("Alice eyes the fruit cautiously.", result.Narration);
        Assert.Null(result.UpdatedSummary);
    }

    [Fact]
    public void Parse_ValidJsonWithSummary_ReturnsBoth()
    {
        var json = """{"narration":"Bob succeeds.","updatedSummary":"Bob has found food."}""";
        var result = NarrationResponseParser.Parse(json, MakeJob(NarrationJobKind.Outcome));

        Assert.Equal("Bob succeeds.", result.Narration);
        Assert.Equal("Bob has found food.", result.UpdatedSummary);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsFallback()
    {
        var result = NarrationResponseParser.Parse("THIS IS NOT JSON", MakeJob(NarrationJobKind.Attempt));

        Assert.False(string.IsNullOrWhiteSpace(result.Narration));
    }

    [Fact]
    public void Parse_EmptyString_ReturnsFallback()
    {
        var result = NarrationResponseParser.Parse(string.Empty, MakeJob());
        Assert.False(string.IsNullOrWhiteSpace(result.Narration));
    }

    [Fact]
    public void Parse_JsonWrappedInMarkdown_StillParses()
    {
        var raw = "```json\n{\"narration\":\"Wrapped.\",\"updatedSummary\":null}\n```";
        var result = NarrationResponseParser.Parse(raw, MakeJob());
        Assert.Equal("Wrapped.", result.Narration);
    }

    [Fact]
    public void Parse_MissingNarrationField_ReturnsFallback()
    {
        var json = """{"updatedSummary":"summary only"}""";
        var result = NarrationResponseParser.Parse(json, MakeJob());
        Assert.False(string.IsNullOrWhiteSpace(result.Narration));
    }
}
