using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security.Code;

/// <summary>
/// Locates auth bootstrap and security middleware blocks in source.
/// Frameworks: ASP.NET Core (Program.cs), NestJS (JwtModule), FastAPI (Depends),
/// Spring (SecurityFilterChain).
/// </summary>
public sealed class AuthBootstrapExtractor(ILogger<AuthBootstrapExtractor> logger)
    : IAuthBootstrapExtractor
{
    private static readonly Regex AuthBootstrapPattern = new(
        @"(AddAuthentication|AddJwtBearer|AddCookie|AddIdentity|JwtModule\.register|" +
        @"new\s+JwtStrategy|fastapi_users|HTTPBearer|OAuth2PasswordBearer|" +
        @"SecurityFilterChain|HttpSecurity|@EnableWebSecurity)",
        RegexOptions.Compiled);

    private static readonly Regex SecurityMiddlewarePattern = new(
        @"(UseAuthentication|UseAuthorization|app\.use\s*\(\s*passport\.|" +
        @"app\.use\s*\(\s*authMiddleware|UseCors|app\.UseCors|" +
        @"app\.add_middleware|@UseGuards|addFilters)",
        RegexOptions.Compiled);

    public IReadOnlyList<SourceFileExcerpt> ExtractAuthBootstrap(string repoPath) =>
        ExtractMatching(repoPath, AuthBootstrapPattern, "auth bootstrap");

    public IReadOnlyList<SourceFileExcerpt> ExtractSecurityMiddleware(string repoPath) =>
        ExtractMatching(repoPath, SecurityMiddlewarePattern, "security middleware");

    private List<SourceFileExcerpt> ExtractMatching(string repoPath, Regex pattern, string reason)
    {
        var found = new List<SourceFileExcerpt>();
        foreach (var file in SourceFileEnumerator.EnumerateSourceFiles(repoPath))
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }

            var match = pattern.Match(text);
            if (!match.Success) continue;

            var startLine = SourceSnippetReader.LineNumberFromOffset(text, match.Index);
            var (s, e, content) = SourceSnippetReader.Read(file, startLine - 5, SourceSnippetReader.DefaultBlockLines);
            found.Add(new SourceFileExcerpt(file, s, e, content, reason));
        }
        logger.LogDebug("AuthBootstrapExtractor: {Reason} → {Count} excerpts", reason, found.Count);
        return found;
    }
}
