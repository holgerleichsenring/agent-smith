using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// p0428: the canonical HOME of every sandbox accepts a write, proven by writing.
/// <para>
/// Two runs died at step 6 on "Read-only file system : '/root'" — the path the very
/// next step stages feed credentials into. The round trip also proves the canonical
/// path RESOLVES in whichever backend is running (in-process rebases /root, containers
/// own it natively): a separate resolution check would re-implement SandboxPathMap and
/// drift from it, while a probe file that comes back is the map working.
/// </para>
/// <para>
/// The working tree is deliberately NOT probed. A probe file under /work would enter
/// the diff the delivery gate measures, and the tree is already proven writable by the
/// checkout that just filled it.
/// </para>
/// </summary>
public sealed class SandboxHomeWritableCheck(
    ISandboxFileReaderFactory readerFactory,
    ILogger<SandboxHomeWritableCheck> logger) : IRunPreflightCheck
{
    private const string HomeRoot = "/root";
    private const string ProbePath = HomeRoot + "/.agentsmith-preflight";
    private const string Lever =
        "the toolchain's home is where feed credentials and git config are staged — "
        + "give the sandbox a writable /root (a writable volume, or HOME pointed at one) "
        + "rather than a read-only root filesystem";

    public string Name => "sandbox-home-writable";

    public async Task<RunPreflightFinding> RunAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null || sandboxes.Count == 0)
            return RunPreflightFinding.Pass(Name, "no sandboxes in this run — nothing to probe");

        var unwritable = new List<string>();
        foreach (var (key, sandbox) in sandboxes)
            if (!await AcceptsAWriteAsync(key, sandbox, cancellationToken))
                unwritable.Add(key);

        return unwritable.Count == 0
            ? RunPreflightFinding.Pass(Name, $"{sandboxes.Count} sandbox home(s) writable")
            : RunPreflightFinding.Fail(
                Name,
                $"the home directory {HomeRoot} is not writable in sandbox(es): "
                + string.Join(", ", unwritable),
                Lever);
    }

    private async Task<bool> AcceptsAWriteAsync(string key, ISandbox sandbox, CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString("N");
        try
        {
            var reader = readerFactory.Create(sandbox);
            await reader.WriteAsync(ProbePath, token, ct);
            var readBack = await reader.TryReadAsync(ProbePath, ct);
            return readBack is not null && readBack.Contains(token, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Home write probe failed in sandbox {Key} — reported as not writable", key);
            return false;
        }
    }
}
