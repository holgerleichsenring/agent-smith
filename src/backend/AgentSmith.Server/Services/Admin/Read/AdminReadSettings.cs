using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>
/// 2026-11-17-f3a8b: how much one admin read may return — dialogue.admin_read_limit rows and
/// dialogue.admin_result_max_chars characters, read from the configuration as the loader holds it now.
/// </summary>
public sealed class AdminReadSettings(IConfigurationLoader loader, ServerContext serverContext)
{
    /// <summary>The rows a read returns: what was asked, at least one, at most the configured limit.</summary>
    public int Rows(int? asked)
    {
        var limit = Math.Max(1, Dialogue().AdminReadLimit);
        return asked is > 0 ? Math.Min(asked.Value, limit) : limit;
    }

    /// <summary>The characters one result carries.</summary>
    public int Chars => Math.Max(1, Dialogue().AdminResultMaxChars);

    private DialogueGlobalConfig Dialogue() => loader.LoadConfig(serverContext.ConfigPath).Dialogue;
}
