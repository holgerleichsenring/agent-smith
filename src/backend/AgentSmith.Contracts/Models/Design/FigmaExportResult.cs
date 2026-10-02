namespace AgentSmith.Contracts.Models.Design;

/// <summary>
/// 2026-10-01-7f7ac: one Figma node rendered as PNG — the downloaded bytes on success, the
/// failure otherwise. The failure is composed from statuses and checks alone: neither the
/// download url nor the token ever reaches it.
/// </summary>
public sealed record FigmaExportResult(byte[]? Png, FigmaReadFailure? Failure)
{
    public static FigmaExportResult Of(byte[] png) => new(png, null);

    public static FigmaExportResult Failed(FigmaReadFailure failure) => new(null, failure);

    public static FigmaExportResult Failed(string detail) =>
        new(null, new FigmaReadFailure(FigmaReadFailureKind.Unknown, detail));
}
