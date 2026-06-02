using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0198: tolerance + correctness contract for SetupRegistryAuthHandler. The
/// no-block / no-sandbox / no-config / no-match paths MUST all return Ok so
/// docs-only repos and public-only projects don't fail this step.
/// </summary>
public sealed class SetupRegistryAuthHandlerTests
{
    private const string Token = "secret-token-xyz";
    private const string AzdoHost = "pkgs.dev.azure.com";

    [Fact]
    public async Task NoRegistriesConfigured_ReturnsOk_SkipsCleanly()
    {
        var handler = MakeHandler(out _, registries: Array.Empty<RegistryConfig>());
        var pipeline = MakePipelineWithSandbox(out _);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No registries configured");
    }

    [Fact]
    public async Task NoSandboxesPublished_ReturnsOk_SkipsCleanly()
    {
        var handler = MakeHandler(out _);
        var pipeline = new PipelineContext();

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No sandboxes");
    }

    [Fact]
    public async Task NoNuGetConfigOrNpmrc_ReturnsOk_WritesNothing()
    {
        var handler = MakeHandler(out var reader);
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/src/Program.cs", "/work/README.md" });
        var pipeline = MakePipelineWithSandbox(out _);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        reader.Verify(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NuGetSourceMatchesRegistry_WritesUserLevelCredentials()
    {
        var handler = MakeHandler(out var reader);
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/nuget.config" });
        reader.Setup(r => r.TryReadAsync("/work/nuget.config", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NuGetConfigXml("MyPrivate",
                "https://pkgs.dev.azure.com/RhenusITPD/.../nuget/v3/index.json"));

        var pipeline = MakePipelineWithSandbox(out _);
        var written = CaptureWrites(reader);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        written.Should().ContainKey("/root/.nuget/NuGet/NuGet.Config");
        var content = written["/root/.nuget/NuGet/NuGet.Config"];
        content.Should().Contain("<MyPrivate>");
        content.Should().Contain($"value=\"{Token}\"");
    }

    [Fact]
    public async Task NuGetSourceNoMatch_LogsAndSkips_NoCredentialWritten()
    {
        var handler = MakeHandler(out var reader);
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/nuget.config" });
        reader.Setup(r => r.TryReadAsync("/work/nuget.config", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NuGetConfigXml("Public", "https://api.nuget.org/v3/index.json"));

        var pipeline = MakePipelineWithSandbox(out _);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        reader.Verify(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NpmRegistryMatchesRegistry_WritesAuthTokenLine()
    {
        var handler = MakeHandler(out var reader);
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/.npmrc" });
        reader.Setup(r => r.TryReadAsync("/work/.npmrc", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                registry=https://pkgs.dev.azure.com/RhenusITPD/_packaging/Npm/npm/registry/
                always-auth=true
                """);

        var pipeline = MakePipelineWithSandbox(out _);
        var written = CaptureWrites(reader);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        written.Should().ContainKey("/root/.npmrc");
        var content = written["/root/.npmrc"];
        content.Should().Contain("//pkgs.dev.azure.com/RhenusITPD/_packaging/Npm/npm/registry/:_authToken=" + Token);
        content.Should().Contain("always-auth=true");
    }

    [Fact]
    public async Task DotBoundaryMatch_PartialLabel_DoesNotMatch()
    {
        // Security: registry host 'pkgs.dev.azure.com' must NOT match
        // 'evil-pkgs.dev.azure.com' (same suffix, different label).
        var handler = MakeHandler(out var reader);
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/nuget.config" });
        reader.Setup(r => r.TryReadAsync("/work/nuget.config", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NuGetConfigXml("Spoof", "https://evilpkgs.dev.azure.com/x/nuget/v3/index.json"));

        var pipeline = MakePipelineWithSandbox(out _);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        reader.Verify(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SetupRegistryAuthHandler MakeHandler(
        out Mock<ISandboxFileReader> reader, IReadOnlyList<RegistryConfig>? registries = null)
    {
        reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);

        var config = new AgentSmithConfig
        {
            Registries = registries ?? new[] { new RegistryConfig(AzdoHost, "any", Token) },
        };
        return new SetupRegistryAuthHandler(
            factory.Object, config, NullLogger<SetupRegistryAuthHandler>.Instance);
    }

    private static PipelineContext MakePipelineWithSandbox(out Mock<ISandbox> sandbox)
    {
        sandbox = new Mock<ISandbox>();
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["repo"] = sandbox.Object });
        return pipeline;
    }

    private static Dictionary<string, string> CaptureWrites(Mock<ISandboxFileReader> reader)
    {
        var written = new Dictionary<string, string>();
        reader.Setup(r => r.WriteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((path, content, _) => written[path] = content)
            .Returns(Task.CompletedTask);
        return written;
    }

    private static string NuGetConfigXml(string sourceName, string sourceUrl) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <add key="{sourceName}" value="{sourceUrl}" />
          </packageSources>
        </configuration>
        """;
}
