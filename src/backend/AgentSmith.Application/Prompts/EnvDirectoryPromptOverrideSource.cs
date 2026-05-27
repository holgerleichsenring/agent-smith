using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Prompts;

/// <summary>
/// Reads prompt overrides from a directory pointed at by the
/// <c>AGENTSMITH_PROMPT_OVERRIDES</c> environment variable. When the variable
/// is unset or the directory is missing, every lookup returns false and the
/// embedded resource wins.
/// </summary>
public sealed class EnvDirectoryPromptOverrideSource : IPromptOverrideSource
{
    private const string EnvVarName = "AGENTSMITH_PROMPT_OVERRIDES";
    private readonly string? _directory;

    public EnvDirectoryPromptOverrideSource(ILogger<EnvDirectoryPromptOverrideSource> logger)
    {
        var raw = Environment.GetEnvironmentVariable(EnvVarName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            _directory = null;
            return;
        }

        if (!Directory.Exists(raw))
        {
            logger.LogWarning(
                "{EnvVar} is set to '{Path}' but the directory does not exist; overrides disabled",
                EnvVarName, raw);
            _directory = null;
            return;
        }

        _directory = raw;
        logger.LogInformation("Prompt overrides active from {Path}", raw);
    }

    public bool TryGet(string name, out string content)
    {
        if (_directory is null)
        {
            content = string.Empty;
            return false;
        }

        var path = Path.Combine(_directory, name + ".md");
        if (!File.Exists(path))
        {
            content = string.Empty;
            return false;
        }

        content = File.ReadAllText(path);
        return true;
    }
}
