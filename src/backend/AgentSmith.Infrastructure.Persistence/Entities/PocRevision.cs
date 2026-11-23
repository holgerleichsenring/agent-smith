namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-23-933db: one uploaded archive of a PoC, numbered per PoC. The import writes it admitted, naming where the
/// archive is stored (the ReferenceFile byte store under <see cref="UploadSession"/> and set <see cref="UploadId"/>);
/// the poc-deploy run's intake records it validated (with its manifest, port, entrypoint and base image) or refused
/// (with the reason), once; 2026-11-23-933dc: the code scan records it blocked or overruled with its verdict, and the
/// commit its sha. A retried Start of the same upload finds its revision by <see cref="UploadId"/>.
/// </summary>
public sealed class PocRevision : EntityBase
{
    public long Id { get; set; }

    public long PocId { get; set; }

    /// <summary>1 for a PoC's first upload, the latest + 1 after it.</summary>
    public int Number { get; set; }

    /// <summary>The owner-keyed session key 'poc-upload:&lt;16 hex&gt;' the archive is stored under.</summary>
    public string UploadSession { get; set; } = string.Empty;

    /// <summary>The set id the byte store minted for the archive.</summary>
    public string UploadId { get; set; } = string.Empty;

    /// <summary>One of PocRevisionStates.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>The principal who imported it.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>The poc-deploy run that took it in; null until the run's claim.</summary>
    public string? RunId { get; set; }

    /// <summary>Path (after the ignore list and the top-folder strip) -> sha256, crc32, size; set once validated.</summary>
    public string? ManifestJson { get; set; }

    public int? Port { get; set; }

    public string? Entrypoint { get; set; }

    public string? BaseImage { get; set; }

    /// <summary>Why the intake refused it.</summary>
    public string? Reason { get; set; }

    /// <summary>2026-11-23-933dc: the code scan's verdict (PocScanVerdict JSON: blocks and notes, masked snippets only).</summary>
    public string? VerdictJson { get; set; }

    /// <summary>2026-11-23-933dc: the agent's explanation of a blocked verdict — untrusted text no rule reads.</summary>
    public string? Explanation { get; set; }

    /// <summary>2026-11-23-933dc: the finding ids overrules lifted, keyed by stage ({"code": [...], "image": [...]}).</summary>
    public string? OverruledIdsJson { get; set; }

    /// <summary>2026-11-23-933dc: the source-repo commit holding the revision's files.</summary>
    public string? CommitSha { get; set; }
}
