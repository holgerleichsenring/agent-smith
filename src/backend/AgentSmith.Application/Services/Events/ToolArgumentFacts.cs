using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// p0423: reads the operator-visible facts out of a tool invocation — the arguments'
/// size, their identity hash, the one-liner that names what was touched, and the size of
/// what came back. Extracted from <see cref="EventPublishingAIFunction"/>, which
/// publishes events; deciding what a blob is worth SAYING about is a different job from
/// saying it.
/// <para>
/// Nothing here returns content. The full arg and result blobs stay inside the process
/// (they carry source, and often the sensitive part is the first 200 characters of a
/// write) — what leaves is a length, a hash, and a whitelisted identifier.
/// </para>
/// </summary>
internal static class ToolArgumentFacts
{
    // p0175-fix: pull operator-visible identifiers out of the args so the activity row
    // reads "read_file src/Foo.cs" instead of "read_file (47B)". Whitelist-only — never
    // serialise the full arg dict. Capped at 120 chars to stay inside one row.
    private const int SummaryCap = 120;

    private static readonly string[] SummaryKeys =
        ["path", "paths", "file", "files", "url", "target", "dir", "directory", "pattern"];

    public static (int Length, string Json) Serialize(
        AIFunctionArguments arguments, JsonSerializerOptions options)
    {
        try
        {
            var json = JsonSerializer.Serialize(arguments, options);
            return (json.Length, json);
        }
        catch { return (0, ""); }
    }

    public static string Hash(string argsJson)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(argsJson)), 0, 8);

    public static string? Summarize(AIFunctionArguments arguments)
    {
        foreach (var key in SummaryKeys)
        {
            if (!arguments.TryGetValue(key, out var raw) || raw is null) continue;
            var rendered = Render(raw);
            if (string.IsNullOrWhiteSpace(rendered)) continue;
            return rendered.Length > SummaryCap ? rendered[..SummaryCap] : rendered;
        }
        return null;
    }

    public static int ResultLength(object? result, JsonSerializerOptions options)
    {
        if (result is null) return 0;
        if (result is string s) return s.Length;
        try { return JsonSerializer.Serialize(result, options).Length; }
        catch { return 0; }
    }

    private static string? Render(object value) => value switch
    {
        string s => s,
        System.Collections.IEnumerable e when value is not string =>
            string.Join(", ", e.Cast<object?>().Where(x => x is not null).Select(x => x!.ToString())),
        _ => value.ToString(),
    };
}
