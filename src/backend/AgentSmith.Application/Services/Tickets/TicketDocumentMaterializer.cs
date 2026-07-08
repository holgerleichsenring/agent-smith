using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tickets;

/// <summary>
/// p0317: materializes downloaded ticket documents under
/// <c>{runRecordDir}/attachments/</c> in the sandbox. txt/md are written as-is;
/// pdf/docx are shipped in as base64 (the sandbox file surface writes strings),
/// decoded in-sandbox, then converted with markitdown (same in-sandbox pattern
/// as BootstrapDocumentHandler — the backend host cannot run markitdown).
/// Fail-soft per document.
/// </summary>
public sealed class TicketDocumentMaterializer(
    ISandboxFileReaderFactory readerFactory,
    ILogger<TicketDocumentMaterializer> logger) : ITicketDocumentMaterializer
{
    public async Task<IReadOnlyList<MaterializedTicketDocument>> MaterializeAsync(
        ISandbox sandbox, string runRecordDir,
        IReadOnlyList<TicketDocumentAttachment> documents,
        CancellationToken cancellationToken)
    {
        var results = new List<MaterializedTicketDocument>(documents.Count);
        var files = readerFactory.Create(sandbox);
        foreach (var document in documents)
        {
            try
            {
                var materialized = await MaterializeOneAsync(
                    sandbox, files, runRecordDir, document, cancellationToken);
                if (materialized is not null) results.Add(materialized);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to materialize ticket document '{File}' — continuing without it",
                    document.FileName);
            }
        }
        return results;
    }

    private async Task<MaterializedTicketDocument?> MaterializeOneAsync(
        ISandbox sandbox, ISandboxFileReader files, string runRecordDir,
        TicketDocumentAttachment document, CancellationToken cancellationToken)
    {
        var path = $"{runRecordDir}/attachments/{SanitizeFileName(document.FileName)}";
        if (document.IsPlainText)
        {
            await files.WriteAsync(path, Encoding.UTF8.GetString(document.Content), cancellationToken);
            return new MaterializedTicketDocument(path, document.FileName);
        }
        return await ConvertViaMarkitdownAsync(sandbox, files, path, document, cancellationToken);
    }

    private async Task<MaterializedTicketDocument?> ConvertViaMarkitdownAsync(
        ISandbox sandbox, ISandboxFileReader files, string path,
        TicketDocumentAttachment document, CancellationToken cancellationToken)
    {
        await files.WriteAsync($"{path}.b64", Convert.ToBase64String(document.Content), cancellationToken);
        var decode = await RunShellAsync(
            sandbox, $"base64 -d '{path}.b64' > '{path}' && rm -f '{path}.b64'", cancellationToken);
        if (decode.ExitCode != 0)
        {
            logger.LogWarning("In-sandbox decode of '{File}' failed (exit {Code})",
                document.FileName, decode.ExitCode);
            return null;
        }

        var convert = await RunShellAsync(sandbox, $"markitdown '{path}'", cancellationToken);
        if (convert.ExitCode != 0 || string.IsNullOrWhiteSpace(convert.Stdout))
        {
            logger.LogWarning(
                "markitdown conversion of '{File}' failed (exit {Code}) — the document stays listed as a binary",
                document.FileName, convert.ExitCode);
            return null;
        }

        var markdownPath = $"{path}.md";
        await files.WriteAsync(markdownPath, convert.Stdout, cancellationToken);
        return new MaterializedTicketDocument(markdownPath, document.FileName);
    }

    private async Task<(int ExitCode, string Stdout)> RunShellAsync(
        ISandbox sandbox, string command, CancellationToken cancellationToken)
    {
        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", command],
            TimeoutSeconds: 120);
        var stdout = new StringBuilder();
        // Synchronous IProgress — Progress<T> dispatches via the captured
        // SynchronizationContext / ThreadPool and races the await below (the
        // same fix SandboxStepRunner.RunAsync carries).
        var progress = new SyncProgress<StepEvent>(ev =>
        {
            if (ev.Kind == StepEventKind.Stdout) stdout.AppendLine(ev.Line);
        });
        var result = await sandbox.RunStepAsync(step, progress, cancellationToken);
        return (result.ExitCode, stdout.ToString());
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    // Attachment names are ticket-origin: collapse everything outside a safe
    // charset so they can neither escape the attachments dir nor break the shell.
    private static string SanitizeFileName(string fileName)
    {
        var safe = new string(fileName
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')
            .ToArray())
            .TrimStart('.');
        return safe.Length > 0 ? safe : "attachment";
    }
}
