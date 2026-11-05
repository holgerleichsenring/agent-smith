using AgentSmith.Contracts.Constants;

namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// 2026-11-02-2b26a: the demo needs only an LLM key. When no config file is found, one agent is written
/// beside the workspace — never inside it, the materializer replaces the workspace — from whichever key is
/// set. A file: three loaders read the config by path, and the reader keeps something to copy.
/// </summary>
internal static class DemoConfigFile
{
    private const string OpenAi = """
        agents:
          demo-openai:
            type: openai
            models:
              scout:         { model: gpt-4.1-mini }
              primary:       { model: gpt-4.1 }
              planning:      { model: gpt-4.1 }
              summarization: { model: gpt-4.1-mini }
        """;

    private const string Claude = """
        agents:
          demo-claude:
            type: claude
            models:
              scout:         { model: claude-haiku-4-5-20251001 }
              primary:       { model: claude-sonnet-4-6 }
              planning:      { model: claude-sonnet-4-6 }
              summarization: { model: claude-haiku-4-5-20251001 }
        """;

    /// <summary>The config's path; an existing file is returned untouched. Null only when neither key is set.</summary>
    public static string? TryWrite(Func<string, string?> environment, string workspaceDir)
    {
        var path = PathFor(workspaceDir);
        if (File.Exists(path)) return path;
        var yaml = IsSet(environment, AgentEnvKeys.OpenAiApiKey) ? OpenAi
            : IsSet(environment, AgentEnvKeys.AnthropicApiKey) ? Claude
            : null;
        if (yaml is null) return null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml + "\n");
        return path;
    }

    public static string PathFor(string workspaceDir) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceDir)) + ".agentsmith.yml";

    public static string NoKeyLine() =>
        $"No config found ({string.Join(", ", ConfigDiscovery.Paths)}) and neither {AgentEnvKeys.OpenAiApiKey} "
        + $"nor {AgentEnvKeys.AnthropicApiKey} is set — export one of them, or write a config, and re-run.";

    private static bool IsSet(Func<string, string?> environment, string name) =>
        !string.IsNullOrEmpty(environment(name));
}
