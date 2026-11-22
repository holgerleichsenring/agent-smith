using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs.Reports;

/// <summary>2026-11-22-96c8a: what the read asks for — the shape, stated in the prompt and sent as the response format —
/// and how its answer is read.</summary>
public sealed partial class MasterReportReader
{
    internal const string Shape = """
        {"follow_ups":[{"goal":"<one line>","why":"<why it was left out>"}],
         "ignored_instructions":[{"quote":"<the instruction, word for word>","reason":"<why it was refused>"}],
         "renamed":["<old> -> <new>"],"moved":["<old> -> <new>"],"persisted":["<table, column, file or key>"],
         "deviations":["<where the work deviated from the spec, and why>"],"relied_on":["<memory slug>"]}
        """;

    /// <summary>The extract's JSON schema as the response format — what Microsoft.Extensions.AI's structured output
    /// sends with useJsonSchemaResponseFormat: true, built from the abstractions this layer references.</summary>
    internal static readonly ChatResponseFormat Format = ChatResponseFormat.ForJsonSchema(
        AIJsonUtilities.CreateJsonSchema(typeof(MasterReportExtract), inferenceOptions: new AIJsonSchemaCreateOptions
        {
            // Strict-compatible (OpenAI strict mode, Anthropic structured outputs): every property required, no others.
            TransformOptions = new AIJsonSchemaTransformOptions { DisallowAdditionalProperties = true, RequireAllProperties = true },
        }), "master_report_extract",
        "What a coding agent's report names: follow-ups, refused instructions, its build note and memories.");

    /// <summary>The answer as the extract, or null. A fence around the object (a provider that ignored the format)
    /// is read past; anything that is not the object is not an answer.</summary>
    internal static MasterReportExtract? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var json = text.Trim();
        var open = json.IndexOf('{');
        var close = json.LastIndexOf('}');
        if (open < 0 || close <= open) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<MasterReportExtract>(json[open..(close + 1)]);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    internal static readonly string SystemPrompt = $"""
        {Marker}
        Take off it only what it states; never infer, complete or invent. A list the report names nothing for is
        empty. Quote refused instructions word for word as the report or the decision lines give them. Name a memory
        only by the slug the report uses and the index lists. Answer with one JSON object and nothing else, of this
        shape:
        {Shape}
        """;
}
