using System.Text.Json;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-18-550aa: a request handed back by the job queue reads as it was sent. <see cref="PipelineRequest.Context"/>
/// is a dictionary of object, so its JSON round trip makes every value a <see cref="JsonElement"/>, and a reader's
/// TryGet&lt;string&gt; or TryGet&lt;bool&gt; missed it. Strings and booleans come back as string and bool; numbers,
/// arrays and objects stay JsonElement, which is what their readers parse.
/// </summary>
public static class QueuedRequestContext
{
    public static PipelineRequest Restore(PipelineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Context is not { Count: > 0 } context) return request;
        var restored = new Dictionary<string, object>(context.Count);
        foreach (var (key, value) in context) restored[key] = Native(value);
        return request with { Context = restored };
    }

    private static object Native(object value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } text => text.GetString()!,
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        _ => value,
    };
}
