using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Runs a Spectral lint scan against an OpenAPI/Swagger spec.
/// </summary>
public interface ISpectralScanner
{
    Task<SpectralResult> LintAsync(string swaggerPath, CancellationToken cancellationToken);
}
