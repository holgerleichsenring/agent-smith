using System.Text.Json;

namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: one content part of a message. <c>Type</c> is <c>text</c>, <c>reasoning</c>,
/// <c>tool_call</c>, <c>tool_result</c> — or <c>unsupported</c>, which is deliberate:
/// a part the bridge cannot render (an image, a provider-specific content type) is
/// declared as dropped rather than silently omitted, so a worker is never quietly shown
/// less than the provider would have received.
/// </summary>
public sealed record WorkerContentPart(
    string Type,
    string? Text = null,
    string? CallId = null,
    string? Name = null,
    JsonElement? Arguments = null,
    string? Result = null,
    string? ClrType = null);
