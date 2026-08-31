using System.Text.Json;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: the property names one schema fragment of a served description
/// declares, following local references and nesting to a bounded depth.
/// <para>
/// A fragment that cannot be read yields NO names rather than a guess: an invented
/// accepted property would be reported as over-exposed against every client that could
/// never have sent it.
/// </para>
/// </summary>
internal static class SchemaPropertyNames
{
    private const int MaxDepth = 6;
    private const string Properties = "properties";
    private const string Reference = "$ref";

    public static IReadOnlyList<string> In(string? fragment, SchemaRefResolver refs)
    {
        var document = Parse(fragment);
        if (document is null) return [];
        using (document)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            Collect(document.RootElement, refs, names, depth: 0);
            return [.. names];
        }
    }

    /// <summary>
    /// The endpoint's response fragment is the whole <c>responses</c> object; only the
    /// success responses describe what a client can read, so an error body's fields never
    /// become properties the interface is said to return.
    /// </summary>
    public static IReadOnlyList<string> InSuccessResponses(string? fragment, SchemaRefResolver refs)
    {
        var document = Parse(fragment);
        if (document is null) return [];
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var response in document.RootElement.EnumerateObject()
                         .Where(r => r.Name.StartsWith('2')))
                Collect(response.Value, refs, names, depth: 0);
            return [.. names];
        }
    }

    private static JsonDocument? Parse(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return null;
        try
        {
            return JsonDocument.Parse(fragment);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Collect(
        JsonElement element, SchemaRefResolver refs, HashSet<string> names, int depth)
    {
        if (depth > MaxDepth) return;
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) Collect(item, refs, names, depth + 1);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        foreach (var member in element.EnumerateObject())
        {
            if (member.NameEquals(Reference) && member.Value.ValueKind == JsonValueKind.String
                && refs.TryResolve(member.Value.GetString(), out var target))
                Collect(target, refs, names, depth + 1);
            else if (member.NameEquals(Properties) && member.Value.ValueKind == JsonValueKind.Object)
                CollectProperties(member.Value, refs, names, depth);
            else
                Collect(member.Value, refs, names, depth + 1);
        }
    }

    private static void CollectProperties(
        JsonElement properties, SchemaRefResolver refs, HashSet<string> names, int depth)
    {
        foreach (var property in properties.EnumerateObject())
        {
            names.Add(property.Name);
            Collect(property.Value, refs, names, depth + 1);
        }
    }
}
