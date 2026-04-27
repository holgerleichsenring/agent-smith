using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security.Code;

/// <summary>
/// Locates file-upload handler bodies. Frameworks: ASP.NET Core (IFormFile),
/// Express (Multer), FastAPI (UploadFile), Spring (MultipartFile).
/// </summary>
public sealed class UploadHandlerExtractor(ILogger<UploadHandlerExtractor> logger)
    : IUploadHandlerExtractor
{
    private static readonly Regex UploadPattern = new(
        @"(IFormFile\s+\w+|IFormFileCollection|" +
        @"multer\s*\(|upload\.(single|array|fields)|" +
        @"UploadFile\s*=\s*File\s*\(|UploadFile\b|" +
        @"MultipartFile\s+\w+|@RequestPart)",
        RegexOptions.Compiled);

    public IReadOnlyList<SourceFileExcerpt> ExtractUploadHandlers(string repoPath)
    {
        var found = new List<SourceFileExcerpt>();
        foreach (var file in SourceFileEnumerator.EnumerateSourceFiles(repoPath))
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }

            foreach (Match match in UploadPattern.Matches(text))
            {
                var startLine = SourceSnippetReader.LineNumberFromOffset(text, match.Index);
                var (s, e, content) = SourceSnippetReader.Read(file, startLine - 2, SourceSnippetReader.DefaultBlockLines);
                found.Add(new SourceFileExcerpt(file, s, e, content, "upload handler"));
            }
        }
        logger.LogDebug("UploadHandlerExtractor: {Count} upload handler excerpts", found.Count);
        return found;
    }
}
