using System.Diagnostics;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Converts a document to Markdown via MarkItDown, detects contract type, loads legal skills.
/// AcquireSourceHandler has already copied the source document into the sandbox at /work/&lt;file&gt;,
/// so this handler picks it up via SandboxFileReader.
/// </summary>
public sealed class BootstrapDocumentHandler(
    IChatClientFactory chatClientFactory,
    ISkillLoader skillLoader,
    IPromptCatalog prompts,
    ISandboxFileReaderFactory readerFactory,
    ILogger<BootstrapDocumentHandler> logger) : ICommandHandler<BootstrapDocumentContext>
{
    private const int ContractTypeDetectionMaxChars = 2000;

    public async Task<CommandResult> ExecuteAsync(
        BootstrapDocumentContext context, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var workspace = context.Repository.LocalPath;

        var entries = await reader.ListAsync(workspace, maxDepth: 1, cancellationToken);
        var sourceFiles = entries
            .Where(e => !e.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sourceFiles.Count == 0)
            return CommandResult.Fail("No source document found in workspace");

        var sourceFile = sourceFiles[0];
        var fileName = Path.GetFileNameWithoutExtension(sourceFile);

        var markdown = await ConvertToMarkdownAsync(sandbox, sourceFile, cancellationToken);
        if (markdown is null)
            return CommandResult.Fail($"MarkItDown conversion failed for {LastSegment(sourceFile)}");

        var markdownPath = Path.Combine(workspace, $"{fileName}.md");
        await reader.WriteAsync(markdownPath, markdown, cancellationToken);
        context.Pipeline.Set(ContextKeys.DocumentMarkdown, markdown);
        logger.LogInformation("Converted {File} to Markdown ({Chars} chars)", LastSegment(sourceFile), markdown.Length);

        var contractType = await DetectContractTypeAsync(markdown, context.Agent, cancellationToken);
        context.Pipeline.Set(ContextKeys.ContractType, contractType);
        logger.LogInformation("Detected contract type: {ContractType}", contractType);

        var roles = skillLoader.LoadRoleDefinitions(context.SkillsPath);
        context.Pipeline.Set(ContextKeys.AvailableRoles, roles);
        logger.LogInformation("Loaded {Count} legal skill roles", roles.Count);

        var principlesPath = Path.Combine(context.SkillsPath, "legal-principles.md");
        var principlesContent = await reader.TryReadAsync(
            Path.Combine(context.Repository.LocalPath, principlesPath), cancellationToken);
        if (principlesContent is not null)
            context.Pipeline.Set(ContextKeys.DomainRules, principlesContent);

        return CommandResult.Ok($"Bootstrapped {LastSegment(sourceFile)} as {contractType}");
    }

    private async Task<string?> ConvertToMarkdownAsync(
        ISandbox sandbox, string sourcePath, CancellationToken cancellationToken)
    {
        // markitdown runs inside the sandbox and writes the markdown back via stdout capture.
        // Local-process Process.Start would not see /work in Docker/K8s mode.
        var step = new global::AgentSmith.Sandbox.Wire.Step(
            SchemaVersion: global::AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: global::AgentSmith.Sandbox.Wire.StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", $"markitdown \"{sourcePath}\""],
            TimeoutSeconds: 120);

        var sb = new System.Text.StringBuilder();
        var progress = new Progress<global::AgentSmith.Sandbox.Wire.StepEvent>(ev =>
        {
            if (ev.Kind == global::AgentSmith.Sandbox.Wire.StepEventKind.Stdout)
                sb.AppendLine(ev.Line);
        });

        try
        {
            var result = await sandbox.RunStepAsync(step, progress, cancellationToken);
            if (result.ExitCode != 0)
            {
                logger.LogError("MarkItDown failed (exit {Code}): {Error}", result.ExitCode, result.ErrorMessage);
                return null;
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run MarkItDown for {File}", sourcePath);
            return null;
        }
    }

    private async Task<string> DetectContractTypeAsync(
        string markdown, AgentConfig agentConfig, CancellationToken cancellationToken)
    {
        var snippet = markdown.Length > ContractTypeDetectionMaxChars
            ? markdown[..ContractTypeDetectionMaxChars]
            : markdown;

        try
        {
            var chat = chatClientFactory.Create(agentConfig, TaskType.Scout);
            var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Scout);
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, prompts.Get("contract-classifier-system")),
                new(ChatRole.User, snippet),
            };
            var response = await chat.GetResponseAsync(messages,
                new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
            var contractType = (response.Text ?? string.Empty).Trim().ToLowerInvariant();

            string[] validTypes = ["nda", "werkvertrag", "dienstleistungsvertrag", "saas-agb", "kaufvertrag", "mietvertrag"];
            return validTypes.Contains(contractType) ? contractType : "unknown";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Contract type detection failed, defaulting to 'unknown'");
            return "unknown";
        }
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}
