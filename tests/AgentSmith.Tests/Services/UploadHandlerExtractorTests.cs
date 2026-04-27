using AgentSmith.Infrastructure.Services.Security.Code;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class UploadHandlerExtractorTests : IDisposable
{
    private readonly string _temp;
    private readonly UploadHandlerExtractor _extractor = new(NullLogger<UploadHandlerExtractor>.Instance);

    public UploadHandlerExtractorTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "up-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }
    public void Dispose() { try { Directory.Delete(_temp, true); } catch { } }

    [Fact]
    public void IFormFile_ExtractsBody()
    {
        File.WriteAllText(Path.Combine(_temp, "AvatarController.cs"),
            "[HttpPost(\"avatar\")]\npublic async Task<IActionResult> Upload(IFormFile file) {\n" +
            "    using var s = System.IO.File.Create(file.FileName);\n    await file.CopyToAsync(s);\n}");
        var blocks = _extractor.ExtractUploadHandlers(_temp);
        blocks.Should().NotBeEmpty();
        blocks[0].Content.Should().Contain("IFormFile");
    }

    [Fact]
    public void Multer_ExtractsBody()
    {
        File.WriteAllText(Path.Combine(_temp, "upload.js"),
            "const upload = multer({ dest: 'public/uploads/' });\napp.post('/avatar', upload.single('file'), handler);");
        var blocks = _extractor.ExtractUploadHandlers(_temp);
        blocks.Should().NotBeEmpty();
    }
}
