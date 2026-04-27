using AgentSmith.Infrastructure.Services.Security.Code;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class AuthBootstrapExtractorTests : IDisposable
{
    private readonly string _temp;
    private readonly AuthBootstrapExtractor _extractor = new(NullLogger<AuthBootstrapExtractor>.Instance);

    public AuthBootstrapExtractorTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }
    public void Dispose() { try { Directory.Delete(_temp, true); } catch { } }

    [Fact]
    public void DotNetProgramCs_ExtractsBlock()
    {
        File.WriteAllText(Path.Combine(_temp, "Program.cs"),
            "var builder = WebApplication.CreateBuilder(args);\n" +
            "builder.Services.AddAuthentication().AddJwtBearer(opt => { opt.Authority = \"x\"; });\n" +
            "var app = builder.Build();\napp.UseAuthentication();\napp.UseAuthorization();");
        var blocks = _extractor.ExtractAuthBootstrap(_temp);
        blocks.Should().NotBeEmpty();
        blocks[0].Content.Should().Contain("AddAuthentication");
    }

    [Fact]
    public void NestJs_ExtractsJwtModule()
    {
        File.WriteAllText(Path.Combine(_temp, "auth.module.ts"),
            "JwtModule.register({ secret: 'x', signOptions: { expiresIn: '1h' } });");
        var blocks = _extractor.ExtractAuthBootstrap(_temp);
        blocks.Should().NotBeEmpty();
    }

    [Fact]
    public void DotNetMiddleware_Extracted()
    {
        File.WriteAllText(Path.Combine(_temp, "Startup.cs"),
            "app.UseAuthentication();\napp.UseAuthorization();\napp.MapControllers();");
        var blocks = _extractor.ExtractSecurityMiddleware(_temp);
        blocks.Should().NotBeEmpty();
    }
}
