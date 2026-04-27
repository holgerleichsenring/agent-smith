using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Locates authentication and authorization bootstrap code in the repository
/// (e.g. Program.cs UseAuthentication / UseAuthorization, NestJS JwtModule,
/// FastAPI Depends(get_current_user), Spring SecurityFilterChain).
/// </summary>
public interface IAuthBootstrapExtractor
{
    /// <summary>
    /// Returns excerpts of code blocks that configure authentication.
    /// </summary>
    IReadOnlyList<SourceFileExcerpt> ExtractAuthBootstrap(string repoPath);

    /// <summary>
    /// Returns excerpts of registrations of security middleware
    /// (UseAuthentication, UseAuthorization, app.use(authMiddleware), etc.).
    /// </summary>
    IReadOnlyList<SourceFileExcerpt> ExtractSecurityMiddleware(string repoPath);
}
