using AgentSmith.Application.Models;
using AgentSmith.Application.Models.Registry;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Registry;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
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
                "https://pkgs.dev.azure.com/AcmeOrg/.../nuget/v3/index.json"));

        var pipeline = MakePipelineWithSandbox(out _);
        var written = CaptureWrites(reader);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        written.Should().ContainKey("/root/.nuget/NuGet/NuGet.Config");
        var content = written["/root/.nuget/NuGet/NuGet.Config"];
        content.Should().Contain("<MyPrivate>");
        content.Should().Contain($"value=\"{Token}\"");
        // p0374: the SOURCE is now defined globally too (not just credentials), so a
        // probe outside the repo tree can resolve the private feed instead of hitting
        // "no sources found" → NU1100 and wrongly deciding the package is unavailable.
        content.Should().Contain("<packageSources>");
        content.Should().Contain(
            "<add key=\"MyPrivate\" value=\"https://pkgs.dev.azure.com/AcmeOrg/.../nuget/v3/index.json\" />");
        content.Should().Contain("<add key=\"nuget.org\"");
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
                registry=https://pkgs.dev.azure.com/AcmeOrg/_packaging/Npm/npm/registry/
                always-auth=true
                """);

        var pipeline = MakePipelineWithSandbox(out _);
        var written = CaptureWrites(reader);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        written.Should().ContainKey("/root/.npmrc");
        var content = written["/root/.npmrc"];
        content.Should().Contain("//pkgs.dev.azure.com/AcmeOrg/_packaging/Npm/npm/registry/:_authToken=" + Token);
        // p0374: the registry MAPPING is now staged globally too (not only the auth
        // token), so an npm resolution outside the repo's own .npmrc still routes to
        // the private feed instead of the public registry → 404 → false "unavailable".
        content.Should().Contain("registry=https://pkgs.dev.azure.com/AcmeOrg/_packaging/Npm/npm/registry/");
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

    [Fact]
    public async Task SetupRegistryAuth_NuGetAndNpm_HandledByFastPath_NoLlmCall()
    {
        var stager = new RecordingStager();
        var handler = MakeHandler(out var reader, stager: stager);
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/nuget.config", "/work/.npmrc" });
        reader.Setup(r => r.TryReadAsync("/work/nuget.config", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NuGetConfigXml("Priv", $"https://{AzdoHost}/AcmeOrg/nuget/v3/index.json"));
        reader.Setup(r => r.TryReadAsync("/work/.npmrc", It.IsAny<CancellationToken>()))
            .ReturnsAsync($"registry=https://{AzdoHost}/AcmeOrg/_packaging/Npm/npm/registry/");
        var pipeline = MakePipelineWithSandbox(out _);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        stager.Calls.Should().Be(0,
            "NuGet and npm are handled by the deterministic fast-paths — the LLM fallback must never be spent on them");
    }

    [Fact]
    public async Task SetupRegistryAuth_UnrecognisedEcosystem_LlmTemplatedConfigWritten_TokenSubstitutedHostSide()
    {
        var stager = new RecordingStager(new RegistryAuthStagingResult(
            new[] { new StagedAuthFile(WidgetAuthPath, WidgetTemplate) }, new[] { WidgetHost }));
        var handler = MakeHandler(out var reader, registries: WidgetRegistries, stager: stager);
        SetupWidgetRepo(reader, contextYaml: "meta:\n  workdir: .\n");
        var pipeline = MakePipelineWithSandboxAndAgent(out _);
        var written = CaptureWrites(reader);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        stager.Calls.Should().Be(1);
        stager.LastUncovered.Select(u => u.Registry.Host).Should().Contain(WidgetHost);
        stager.LastUncovered.Single().MatchingPaths.Should().Contain("/work/widget.manifest");
        written.Should().ContainKey(WidgetAuthPath);
        written[WidgetAuthPath].Should().Contain(WidgetToken, "the real token is substituted host-side before writing");
        written[WidgetAuthPath].Should().NotContain(RegistryTokenPlaceholder.For(WidgetHost),
            "no placeholder may remain in the written file");
        // p0375 persist-once: the TEMPLATED result (placeholders only) lands in context.yaml.
        written.Should().ContainKey(ContextYamlPath);
        written[ContextYamlPath].Should().Contain("registry_auth");
        written[ContextYamlPath].Should().Contain(RegistryTokenPlaceholder.For(WidgetHost));
        written[ContextYamlPath].Should().NotContain(WidgetToken, "a secret must never land in the repo");
    }

    [Fact]
    public async Task SetupRegistryAuth_StagerFailure_LoudDecisionLineRecorded_RunProceeds()
    {
        var decisions = new Mock<IDecisionLogger>();
        var handler = MakeHandler(out var reader, registries: WidgetRegistries,
            stager: new ThrowingStager(), decisions: decisions);
        SetupWidgetRepo(reader, contextYaml: null);
        var pipeline = MakePipelineWithSandboxAndAgent(out _);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("stager failure is fail-soft — the run proceeds");
        decisions.Verify(d => d.LogAsync(
                It.IsAny<string?>(), It.IsAny<DecisionCategory>(),
                It.Is<string>(s => s.Contains(WidgetHost) && s.Contains("NOT staged")),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Once, "the gap must be visible on the run's decisions channel, never silent");
    }

    [Fact]
    public async Task SetupRegistryAuth_PersistedRegistryAuthSection_ReplayedWithoutLlm()
    {
        var stager = new RecordingStager();
        var handler = MakeHandler(out var reader, registries: WidgetRegistries, stager: stager);
        SetupWidgetRepo(reader, contextYaml: RegistryAuthContextYaml(WidgetTemplate));
        var pipeline = MakePipelineWithSandbox(out _);
        var written = CaptureWrites(reader);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        stager.Calls.Should().Be(0, "a persisted registry_auth section replays LLM-free");
        written.Should().ContainKey(WidgetAuthPath);
        written[WidgetAuthPath].Should().Contain(WidgetToken);
    }

    [Fact]
    public async Task SetupRegistryAuth_OperatorDeclaredSection_WinsOverLlm()
    {
        const string declaredTemplate = "declared-by-operator = \"__AS_TOKEN_registry.widget.example__\"";
        var stager = new RecordingStager(new RegistryAuthStagingResult(
            new[] { new StagedAuthFile(WidgetAuthPath, WidgetTemplate) }, new[] { WidgetHost }));
        var handler = MakeHandler(out var reader, registries: WidgetRegistries, stager: stager);
        SetupWidgetRepo(reader, contextYaml: RegistryAuthContextYaml(declaredTemplate));
        var pipeline = MakePipelineWithSandbox(out _);
        var written = CaptureWrites(reader);

        var result = await handler.ExecuteAsync(new SetupRegistryAuthContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        stager.Calls.Should().Be(0, "an operator-authored registry_auth section is authoritative");
        written[WidgetAuthPath].Should().Contain("declared-by-operator");
        written[WidgetAuthPath].Should().Contain(WidgetToken);
    }

    private const string WidgetHost = "registry.widget.example";
    private const string WidgetToken = "WIDGET-SECRET-9f3";
    private const string WidgetAuthPath = "/root/.config/widget/auth.toml";
    private const string ContextYamlPath = "/work/.agentsmith/contexts/default/context.yaml";
    private static readonly string WidgetTemplate =
        $"token = \"{RegistryTokenPlaceholder.For(WidgetHost)}\"";
    private static readonly IReadOnlyList<RegistryConfig> WidgetRegistries =
        new[] { new RegistryConfig(WidgetHost, "any", WidgetToken) };

    private static void SetupWidgetRepo(Mock<ISandboxFileReader> reader, string? contextYaml)
    {
        var listing = new List<string> { "/work/widget.manifest" };
        if (contextYaml is not null) listing.Add(ContextYamlPath);
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listing);
        reader.Setup(r => r.TryReadAsync("/work/widget.manifest", It.IsAny<CancellationToken>()))
            .ReturnsAsync($"[registries]\ndefault = \"https://{WidgetHost}/index\"\n");
        if (contextYaml is not null)
            reader.Setup(r => r.TryReadAsync(ContextYamlPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(contextYaml);
    }

    private static string RegistryAuthContextYaml(string template) => $"""
        meta:
          workdir: .
        registry_auth:
          files:
            - path: {WidgetAuthPath}
              content: '{template}'
        """;

    private SetupRegistryAuthHandler MakeHandler(
        out Mock<ISandboxFileReader> reader, IReadOnlyList<RegistryConfig>? registries = null,
        IRegistryAuthStager? stager = null, Mock<IDecisionLogger>? decisions = null)
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
        var applier = new GenericRegistryAuthApplier(
            new RegistryAuthTemplateStore(
                new ContextYamlRegistryAuthCodec(), NullLogger<RegistryAuthTemplateStore>.Instance),
            new RegistryHostGrep(NullLogger<RegistryHostGrep>.Instance),
            stager ?? new RecordingStager(),
            new StagedAuthFileWriter(
                new RegistryAuthPathGuard(), new SecretLeakGuard(), new RegistryTokenSubstitutor(),
                config, NullLogger<StagedAuthFileWriter>.Instance),
            new RegistryAuthFailureReporter(
                (decisions ?? new Mock<IDecisionLogger>()).Object,
                NullLogger<RegistryAuthFailureReporter>.Instance),
            config,
            NullLogger<GenericRegistryAuthApplier>.Instance);
        return new SetupRegistryAuthHandler(
            factory.Object, config, applier, NullLogger<SetupRegistryAuthHandler>.Instance);
    }

    /// <summary>
    /// Scripted <see cref="IRegistryAuthStager"/> that records invocations (to prove
    /// the fast-paths and template replay never reach the LLM) and returns a canned
    /// templated result for the generic path.
    /// </summary>
    private sealed class RecordingStager(RegistryAuthStagingResult? result = null) : IRegistryAuthStager
    {
        public int Calls { get; private set; }
        public IReadOnlyList<UncoveredRegistry> LastUncovered { get; private set; } = Array.Empty<UncoveredRegistry>();

        public Task<RegistryAuthStagingResult> StageAsync(
            ISandbox sandbox, string repoRoot, IReadOnlyList<UncoveredRegistry> uncovered,
            AgentConfig agent, CancellationToken cancellationToken)
        {
            Calls++;
            LastUncovered = uncovered;
            return Task.FromResult(result ?? RegistryAuthStagingResult.Empty);
        }
    }

    private sealed class ThrowingStager : IRegistryAuthStager
    {
        public Task<RegistryAuthStagingResult> StageAsync(
            ISandbox sandbox, string repoRoot, IReadOnlyList<UncoveredRegistry> uncovered,
            AgentConfig agent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("scripted stager failure");
    }

    private static PipelineContext MakePipelineWithSandboxAndAgent(out Mock<ISandbox> sandbox)
    {
        var pipeline = MakePipelineWithSandbox(out sandbox);
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("test-pipeline", new AgentConfig(), "skills", null));
        return pipeline;
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
