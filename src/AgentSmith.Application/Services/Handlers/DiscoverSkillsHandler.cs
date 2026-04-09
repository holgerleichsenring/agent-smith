using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Discovers skill candidates by scanning a configurable skill_sources directory.
/// Each subdirectory containing a SKILL.md is treated as a candidate.
/// Already-installed skills are excluded.
/// </summary>
public sealed class DiscoverSkillsHandler(
    ILogger<DiscoverSkillsHandler> logger)
    : ICommandHandler<DiscoverSkillsContext>
{
    private const string SkillFileName = "SKILL.md";

    public Task<CommandResult> ExecuteAsync(
        DiscoverSkillsContext context, CancellationToken cancellationToken)
    {
        // Check if explicit candidates were already provided in the pipeline
        if (context.Pipeline.TryGet<IReadOnlyList<SkillCandidate>>(ContextKeys.SkillCandidates, out var existing)
            && existing is { Count: > 0 })
        {
            logger.LogInformation("Using {Count} pre-loaded skill candidates from pipeline context", existing.Count);
            return Task.FromResult(CommandResult.Ok($"{existing.Count} pre-loaded candidates found"));
        }

        var sourcesPath = context.SkillSourcesPath;
        if (!Directory.Exists(sourcesPath))
        {
            logger.LogInformation("Skill sources path does not exist: {Path}", sourcesPath);
            context.Pipeline.Set(ContextKeys.SkillCandidates, (IReadOnlyList<SkillCandidate>)[]);
            return Task.FromResult(CommandResult.Ok("No skill sources directory found"));
        }

        var installedSet = new HashSet<string>(context.InstalledSkillNames, StringComparer.OrdinalIgnoreCase);
        var candidates = new List<SkillCandidate>();

        foreach (var dir in Directory.GetDirectories(sourcesPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillFile = Path.Combine(dir, SkillFileName);
            if (!File.Exists(skillFile))
                continue;

            var name = Path.GetFileName(dir);
            if (installedSet.Contains(name))
            {
                logger.LogDebug("Skipping already-installed skill: {Name}", name);
                continue;
            }

            var content = File.ReadAllText(skillFile);
            var description = ExtractDescription(content);

            candidates.Add(new SkillCandidate(
                Name: name,
                Description: description,
                SourceUrl: dir,
                Content: content,
                Version: null,
                Commit: null));
        }

        context.Pipeline.Set(ContextKeys.SkillCandidates, (IReadOnlyList<SkillCandidate>)candidates.AsReadOnly());

        logger.LogInformation("Discovered {Count} skill candidates from {Path}", candidates.Count, sourcesPath);
        return Task.FromResult(CommandResult.Ok($"{candidates.Count} skill candidates discovered"));
    }

    internal static string ExtractDescription(string content)
    {
        // Take the first non-heading, non-empty line as a description
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;
            return trimmed.Length > 200 ? trimmed[..200] : trimmed;
        }

        return "No description available";
    }
}
