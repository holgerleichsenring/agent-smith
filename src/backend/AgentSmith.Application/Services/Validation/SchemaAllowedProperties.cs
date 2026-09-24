using Json.Pointer;
using Json.Schema;

namespace AgentSmith.Application.Services.Validation;

/// <summary>2026-09-24-3907: the way out of a closed block, for a refusal that would otherwise
/// name only the dead end.</summary>
internal static class SchemaAllowedProperties
{
    /// <summary>
/// 2026-09-24-3907: what the block DOES allow, appended to the refusal. "This property is not
/// allowed here" names only the dead end, and a reader told to fix exactly what the error
/// names has nothing to aim at — three live design turns answered it by guessing another key.
/// The closed node is the evaluation path with its trailing additionalProperties dropped;
/// where it cannot be resolved the sentence stays as it was rather than inventing a list.
/// </summary>
internal static string Of(JsonSchema schema, EvaluationResults detail)
{
    try
    {
        var path = detail.EvaluationPath;
        if (path.Count < 2) return string.Empty;
        var prefix = JsonPointer.Parse(
            "/" + string.Join("/", path.Take(path.Count - 1).Select(segment => segment.ToString())));
        var node = ((IBaseDocument)schema).FindSubschema(prefix, EvaluationOptions.Default);
        var names = node?.GetProperties()?.Keys.ToList();
        return names is { Count: > 0 }
            ? $" — it allows {string.Join(", ", names)}"
            : string.Empty;
    }
    catch (Exception)
    {
        // A refusal that cannot name the alternatives is still a usable refusal; one that
        // throws while formatting loses the error it was reporting.
        return string.Empty;
    }
}
}
