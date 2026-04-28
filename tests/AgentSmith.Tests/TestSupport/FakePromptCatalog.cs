using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// Test stub for <see cref="IPromptCatalog"/> that returns placeholder strings.
/// Tests using this aren't asserting prompt content — they're exercising the
/// handlers that consume the catalog.
/// </summary>
internal sealed class FakePromptCatalog : IPromptCatalog
{
    private readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal);

    public FakePromptCatalog WithPrompt(string name, string content)
    {
        _overrides[name] = content;
        return this;
    }

    public string Get(string name) =>
        _overrides.TryGetValue(name, out var content) ? content : $"<prompt:{name}>";

    public string Render(string name, IReadOnlyDictionary<string, string> tokens)
    {
        var content = Get(name);
        foreach (var (key, value) in tokens)
        {
            content = content.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }
        return content;
    }
}
