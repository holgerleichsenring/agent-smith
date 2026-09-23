using AgentSmith.Contracts.Constants;
using GitHub.Copilot;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: the postures a Copilot runtime and its sessions are opened with, separated
/// from the calls that open them so every one of them can be asserted without a runtime process
/// or a Copilot seat. Each setting here overrides a default that is wrong for a server.
/// </summary>
internal static class CopilotSessionFactory
{
    /// <summary>
    /// The session's posture, separated from the call that opens it so it can be asserted without
    /// a runtime process or a Copilot seat.
    /// </summary>
    internal static SessionConfig BuildSessionConfig(CopilotSessionRequest request)
    {
        var config = new SessionConfig
        {
            Model = request.Model,
            ReasoningEffort = request.ReasoningEffort,
            // The seat lives on the SESSION, not on the shared client: Copilot rejects org-owned
            // PATs, so a token is a person's, and two agents naming two secrets are two people.
            GitHubToken = request.SeatToken,
            // An allowlist, documented "only these tools will be available when specified".
            // Empty therefore means the model reaches nothing at all — not "no filter".
            AvailableTools = request.ToolNames.ToList(),
            Streaming = true,
            // Both spend model calls INSIDE the session that our decorator chain never sees: no
            // LlmCall pair, no trace entry, no rate-limit acquisition — and their tokens land in
            // the usage accumulators, so the next delta would price them as ours. Compaction is
            // CompactingChatClient's job, as it is for every other provider.
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            ToolSearch = new ToolSearchConfig { Enabled = false },
        };

        if (!string.IsNullOrWhiteSpace(request.SystemMessage))
            config.SystemMessage = new SystemMessageConfig
            {
                // Replace, never Append: appending would put Copilot's coding-agent prompt in
                // front of the run's own and there would be no parity left to speak of.
                Mode = SystemMessageMode.Replace,
                Content = request.SystemMessage,
            };

        return config;
    }

    /// <summary>
    /// The runtime's own posture. UseLoggedInUser is false unconditionally: its default reads a
    /// developer's stored OAuth tokens or gh CLI auth, which is right on a laptop and wrong in a
    /// cluster, and every seat this runtime answers on arrives per session anyway.
    /// </summary>
    internal static CopilotClientOptions BuildClientOptions(string? runtimePath, string baseDirectory)
    {
        var options = new CopilotClientOptions
        {
            // The default is CopilotCli, whose own documentation says "Do not use this mode for
            // server-based multi-user applications".
            Mode = CopilotClientMode.Empty,
            BaseDirectory = baseDirectory,
            UseLoggedInUser = false,
        };
        if (!string.IsNullOrWhiteSpace(runtimePath))
        {
            // A path that was configured and is not there is an operator mistake worth naming:
            // the image was built without the Copilot runtime, or the mount is missing. Falling
            // back to the "bundled" runtime would be a lie, because this repository's build
            // deliberately never downloads one.
            if (!File.Exists(runtimePath))
                throw new InvalidOperationException(
                    $"{AgentEnvKeys.CopilotCliPath} points at '{runtimePath}', which does not exist. "
                    + "Build the image with --build-arg COPILOT_CLI_VERSION=<version> to include the "
                    + "Copilot CLI runtime, or point the variable at a binary you provide.");
            options.Connection = RuntimeConnection.ForStdio(runtimePath, null);
        }
        return options;
    }
}
