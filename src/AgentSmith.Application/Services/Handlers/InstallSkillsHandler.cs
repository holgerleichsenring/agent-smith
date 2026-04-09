using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Copies approved skill drafts from the temporary draft directory
/// to the target skills installation path (e.g. config/skills/coding/).
/// </summary>
public sealed class InstallSkillsHandler(
    ILogger<InstallSkillsHandler> logger)
    : ICommandHandler<InstallSkillsContext>
{
    public Task<CommandResult> ExecuteAsync(
        InstallSkillsContext context, CancellationToken cancellationToken)
    {
        if (context.ApprovedSkills.Count == 0)
        {
            logger.LogInformation("No approved skills to install");
            return Task.FromResult(CommandResult.Ok("No skills to install"));
        }

        if (!Directory.Exists(context.DraftDirectory))
        {
            logger.LogWarning("Draft directory does not exist: {Dir}", context.DraftDirectory);
            return Task.FromResult(CommandResult.Fail($"Draft directory not found: {context.DraftDirectory}"));
        }

        Directory.CreateDirectory(context.InstallPath);
        var installed = 0;

        foreach (var evaluation in context.ApprovedSkills)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourcePath = Path.Combine(context.DraftDirectory, evaluation.Candidate.Name);
            var targetPath = Path.Combine(context.InstallPath, evaluation.Candidate.Name);

            if (!Directory.Exists(sourcePath))
            {
                logger.LogWarning("Draft not found for skill {Name} at {Path}", evaluation.Candidate.Name, sourcePath);
                continue;
            }

            CopyDirectory(sourcePath, targetPath);
            installed++;
            logger.LogInformation("Installed skill {Name} to {Path}", evaluation.Candidate.Name, targetPath);
        }

        logger.LogInformation("Installed {Count}/{Total} skills to {Path}",
            installed, context.ApprovedSkills.Count, context.InstallPath);

        return Task.FromResult(CommandResult.Ok($"{installed} skills installed to {context.InstallPath}"));
    }

    internal static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(targetDir, fileName), overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            CopyDirectory(subDir, Path.Combine(targetDir, dirName));
        }
    }
}
