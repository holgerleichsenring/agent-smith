namespace AgentSmith.Application.Services.Json;

/// <summary>
/// p0426: strips the markdown fence a model puts around JSON it was asked to emit bare.
/// Finding the JSON inside prose is a different job from mapping the JSON to a type, and
/// several readers need it.
/// </summary>
public static class FencedJson
{
    public static string Strip(string json)
    {
        if (!json.StartsWith("```")) return json;
        var firstNewline = json.IndexOf('\n');
        if (firstNewline > 0) json = json[(firstNewline + 1)..];
        if (json.EndsWith("```")) json = json[..^3].TrimEnd();
        return json;
    }
}
