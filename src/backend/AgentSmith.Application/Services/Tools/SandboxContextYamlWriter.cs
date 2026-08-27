using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-364f: puts a written context document on a sandbox's disk as a
/// READ-MODIFY-WRITE. The whole-file write it replaces serialised the typed
/// <c>ContextYamlDocument</c> and nothing else, so every section that record does not
/// model — state, methodology, integrations, data, decisions — was deleted the moment
/// a context was rewritten. Here the sections the document states replace their keys
/// and the rest of the file is carried over.
/// <para>
/// An existing file that does not parse is REFUSED and left on disk: overwriting it
/// would turn a recoverable edit into exactly the loss this class exists to stop.
/// </para>
/// </summary>
public sealed class SandboxContextYamlWriter(IContextYamlSectionUpsert upsert)
{
    /// <summary>The prefix a caller reads a successful write off.</summary>
    public const string WrittenPrefix = "context.yaml written:";

    private const int StepTimeoutSeconds = 30;

    public async Task<string> WriteAsync(
        ISandbox sandbox, string repo, string contextName, string documentYaml, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var path = $".agentsmith/contexts/{contextName}/context.yaml";
        var merged = upsert.Upsert(await ReadAsync(sandbox, path, ct), documentYaml);
        if (merged.Yaml is null)
            return $"Error: the existing {path} is not valid YAML, so it was left untouched — "
                 + $"{merged.ParseError}. Repair the file, then write the context again.";

        var result = await sandbox.RunStepAsync(WriteStep(path, merged.Yaml), progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: write failed — {result.ErrorMessage ?? "unknown"}"
            : $"{WrittenPrefix} {(string.IsNullOrEmpty(repo) ? string.Empty : repo + "/")}{path}";
    }

    private static async Task<string?> ReadAsync(ISandbox sandbox, string path, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            TimeoutSeconds: StepTimeoutSeconds, Path: path);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        // A missing file is the first init, not a failure — the document is written alone.
        return result.ExitCode == 0 ? result.OutputContent : null;
    }

    private static Step WriteStep(string path, string yaml) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            TimeoutSeconds: StepTimeoutSeconds, Path: path, Content: yaml);
}
