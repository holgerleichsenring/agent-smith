using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0196: returns a benign template for any prompt name. Real prompts live in
/// the skills catalog; the harness exercises handlers (not prompt content).
/// </summary>
internal sealed class StubPromptCatalog : IPromptCatalog
{
    public string Get(string name) =>
        $"# Stub prompt for '{name}'\nYou are a test agent. Return an empty JSON object.";

    public string Render(string name, IReadOnlyDictionary<string, string> tokens)
    {
        var body = Get(name);
        foreach (var (k, v) in tokens)
            body = body.Replace("{" + k + "}", v);
        return body;
    }
}
