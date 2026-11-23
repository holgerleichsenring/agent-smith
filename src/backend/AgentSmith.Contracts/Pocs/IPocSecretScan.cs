using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dc: scans a PoC revision's files in process with every pattern of the skills catalog — every text
/// file in full, no ignore rule of the upload applied. Strict for the 'secrets' catalog: none loaded, one dropped as
/// invalid or a match timeout fails the scan.
/// </summary>
public interface IPocSecretScan
{
    PocSecretScanResult Scan(IReadOnlyList<ReferenceSetFile> files);
}
