using System.Text;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: a PoC's root Dockerfile, checked — its size (pocs.archive.max_dockerfile_kb), a FROM, the
/// blocklist, a numeric final USER and exactly one TCP EXPOSE in the final stage — and what it says about the app:
/// the port, the final stage's ENTRYPOINT and CMD, and the final FROM image.
/// </summary>
public sealed class DockerfileRules(
    DockerfileStages stages, DockerfileBlocklist blocklist, DockerfileUser user, DockerfileExpose expose)
{
    public const string FileName = "Dockerfile";

    public PocDockerfileCheck Check(byte[] content, PocArchiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(limits);
        if (content.LongLength > MaxBytes(limits)) return Refused(TooLarge(content.LongLength, limits));
        var document = stages.Parse(Encoding.UTF8.GetString(content));
        if (document.FinalStage.Count == 0)
            return Refused(new PocRuleRefusal(PocArchiveChecks.Dockerfile,
                "the Dockerfile has no FROM; a PoC is built from its Dockerfile, which starts from an image."));
        if ((blocklist.RefusalOf(document) ?? user.RefusalOf(document)) is { } refusal) return Refused(refusal);
        var (port, exposeRefusal) = expose.PortOf(document);
        return exposeRefusal is not null
            ? Refused(exposeRefusal)
            : new PocDockerfileCheck(null, new PocDockerfile(port, Entrypoint(document), document.BaseImage));
    }

    internal static long MaxBytes(PocArchiveLimits limits) => limits.MaxDockerfileKb * 1024L;

    /// <summary>The refusal of a Dockerfile of <paramref name="bytes"/> over pocs.archive.max_dockerfile_kb.</summary>
    internal static PocRuleRefusal TooLarge(long bytes, PocArchiveLimits limits) =>
        new(PocArchiveChecks.Dockerfile, $"the Dockerfile is {(bytes + 1023) / 1024} KB, over "
            + $"{PocArchiveLimits.MaxDockerfileKbKey} {limits.MaxDockerfileKb}, the bound on the file read before anything "
            + "else is unpacked");

    // The final stage's last ENTRYPOINT and last CMD in one comparable string, or null when neither is there.
    private static string? Entrypoint(DockerfileDocument document)
    {
        var final = document.FinalStage;
        string[] parts = [.. new[] { "ENTRYPOINT", "CMD" }
            .Select(k => final.LastOrDefault(i => i.Keyword == k) is { } i ? $"{k} {string.Join(' ', i.Tokens)}" : null)
            .OfType<string>()];
        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    private static PocDockerfileCheck Refused(PocRuleRefusal refusal) => new(refusal, null);
}
