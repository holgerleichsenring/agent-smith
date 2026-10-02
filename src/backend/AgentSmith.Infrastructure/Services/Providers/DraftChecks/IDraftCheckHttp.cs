namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: sends one draft-check request — no redirects, ten seconds — and turns every
/// transport failure into a sentence of ours instead of an exception.
/// </summary>
public interface IDraftCheckHttp
{
    Task<DraftCheckAnswer> GetAsync(string url, DraftCheckAuth auth, CancellationToken cancellationToken);

    Task<DraftCheckAnswer> PostJsonAsync(
        string url, object body, DraftCheckAuth auth, CancellationToken cancellationToken);
}
