using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Locates file-upload handler bodies in source code
/// (.NET IFormFile parameters, Express/Multer, FastAPI UploadFile,
/// Spring MultipartFile).
/// </summary>
public interface IUploadHandlerExtractor
{
    IReadOnlyList<SourceFileExcerpt> ExtractUploadHandlers(string repoPath);
}
