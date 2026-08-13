using System.Text;
using AgentSmith.Application.Models.Registry;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Implements <see cref="IRegistryAuthStager"/> as ONE Scout-role, read-only
/// agentic call: the model inspects the matched repo files, works out which
/// package manager references each uncovered host, and emits that manager's
/// GLOBAL auth-config file(s) with <c>__AS_TOKEN_&lt;host&gt;__</c> placeholders.
/// The prompt carries only the hosts + matching paths and the placeholder
/// convention — never a token — so the secret cannot enter the LLM prompt,
/// response, or history. Bounded by the existing LoopLimitsConfig per-call caps
/// (tool iterations, output tokens, wall time) — no new limits mechanism.
/// </summary>
public sealed class RegistryAuthStager(
    IChatClientFactory chatClientFactory,
    StagedAuthFileJsonReader jsonReader,
    IRunContextAccessor runContext,
    LoopLimitsConfig limits,
    AgenticToolSurface toolSurface,
    ILogger<RegistryAuthStager> logger) : IRegistryAuthStager
{
    public async Task<RegistryAuthStagingResult> StageAsync(
        ISandbox sandbox, string repoRoot,
        IReadOnlyList<UncoveredRegistry> uncovered, AgentConfig agent,
        CancellationToken cancellationToken)
    {
        if (uncovered.Count == 0) return RegistryAuthStagingResult.Empty;

        var fs = new FilesystemToolHost(sandbox, repoRoot);
        var chat = chatClientFactory.Create(
            agent, TaskType.Scout, maxIterations: limits.MaxToolCallsPerSkill);
        var options = new ChatOptions
        {
            Tools = toolSurface.Scout(fs),
            MaxOutputTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Scout),
        };
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt()),
            new(ChatRole.User, BuildUserPrompt(repoRoot, uncovered)),
        };

        using var timeBound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeBound.CancelAfter(TimeSpan.FromSeconds(limits.MaxSecondsPerSkillCall));
        using var _scope = runContext.BeginCallScope("registry-auth-stager", "SetupRegistryAuth");
        var response = await chat.GetResponseAsync(messages, options, timeBound.Token);
        var files = jsonReader.Read(response.Text);
        logger.LogInformation(
            "RegistryAuthStager emitted {Files} auth file(s) for host(s) [{Hosts}] ({In}+{Out} tokens).",
            files.Count, string.Join(", ", uncovered.Select(u => u.Registry.Host)),
            response.Usage?.InputTokenCount ?? 0, response.Usage?.OutputTokenCount ?? 0);

        return new RegistryAuthStagingResult(files, TargetedHosts(files, uncovered));
    }

    private static IReadOnlyList<string> TargetedHosts(
        IReadOnlyList<StagedAuthFile> files, IReadOnlyList<UncoveredRegistry> uncovered)
    {
        var placeholderHosts = files
            .SelectMany(f => RegistryTokenPlaceholder.HostsIn(f.Content))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return placeholderHosts.Count > 0
            ? placeholderHosts
            : uncovered.Select(u => u.Registry.Host).ToList();
    }

    private static string BuildSystemPrompt() => $$"""
        You stage private package-registry authentication inside a build sandbox.

        You are given one or more registry HOSTS, each with repo file paths that reference
        it. Inspect those files (read-only tools) to determine which package manager
        (cargo, go, maven, pip, gradle, composer, gem, ...) references each host, then emit
        the GLOBAL / user-scope auth-config file(s) that manager reads so authenticated
        restore works from anywhere in the sandbox — not only inside the repo tree.

        Token handling (MANDATORY):
        - You are NEVER given the real token and must NEVER invent, guess, or emit one.
        - Wherever the auth token belongs, write the literal placeholder
          {{RegistryTokenPlaceholder.Prefix}}<host>{{RegistryTokenPlaceholder.Suffix}}
          (for example {{RegistryTokenPlaceholder.For("registry.example.com")}}). The host
          substitutes the real secret for this placeholder before writing.
        - Emit ONLY user/global-scope absolute paths under the sandbox user's home
          (for example /root/.cargo/credentials.toml). Never write inside the repo checkout.

        Respond with ONLY a JSON object, no prose and no code fences:
        {"files":[{"path":"<absolute user-config path>","content":"<file body with placeholders>"}]}
        Return an empty "files" array if no ecosystem in this repo needs staging.
        """;

    private static string BuildUserPrompt(string repoRoot, IReadOnlyList<UncoveredRegistry> uncovered)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Repository root: {repoRoot}");
        sb.AppendLine("Registry hosts to stage (with the repo files that reference each):");
        foreach (var u in uncovered)
            sb.AppendLine($"- {u.Registry.Host}  (referenced by: {string.Join(", ", u.MatchingPaths)})");
        sb.AppendLine();
        sb.AppendLine("Inspect the referenced files, then emit the global auth-config file(s).");
        return sb.ToString();
    }
}
