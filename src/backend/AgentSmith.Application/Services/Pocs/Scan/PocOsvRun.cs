using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: runs the pinned image's scanner by path (the sandbox agent runs commands, not the image's
/// ENTRYPOINT) in lockfile mode — one -L per file — with JSON written to a file and offline databases downloaded in the
/// sandbox, so no dependency list leaves it. Nothing is installed or executed from the upload.
/// </summary>
public sealed class PocOsvRun
{
    public const string Binary = "/osv-scanner";
    public const string DatabaseHost = "osv-vulnerabilities.storage.googleapis.com";
    public const string LockfileRoot = "lockfiles";
    public const string Report = "osv-report.json";

    public Task<StepResult> RunAsync(ISandbox sandbox, IReadOnlyList<string> paths, PocScanConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(config);
        var work = sandbox.WorkDirectory();
        string[] args = ["scan", "source", "--offline", "--download-offline-databases", "--format", "json",
            "--output", $"{work}/{Report}", .. paths.SelectMany(p => new[] { "-L", $"{work}/{LockfileRoot}/{p}" })];
        return sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: Binary, Args: args,
            Env: new Dictionary<string, string> { ["OSV_SCANNER_LOCAL_DB_CACHE_DIRECTORY"] = $"{work}/.osv-db" },
            TimeoutSeconds: config.TimeoutSeconds), null, ct);
    }
}
