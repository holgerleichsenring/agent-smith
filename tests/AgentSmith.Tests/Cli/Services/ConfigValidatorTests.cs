using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Events;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Cli.Services;

/// <summary>
/// p0391b: `agentsmith config validate` exists so an operator learns what the server would
/// say about a configuration BEFORE rolling it out, instead of from a container that comes
/// up degraded. It runs the server's own rules — there is no CLI-side validator, because a
/// second one would be a second source of truth about what a valid configuration is.
/// </summary>
public sealed class ConfigValidatorTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(
        Path.GetTempPath(), $"agentsmith-validate-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void Cli_Validate_ReportsTheSameFindingsAsTheServer()
    {
        // A trigger without project_resolution: a rule the server's ConfigurationProbe owns.
        Write("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t, trigger_statuses: [open], done_status: closed }
            projects:
              demo:
                agent: a
                tracker: t
                repos: [r]
                github_trigger:
                  trigger_statuses: [open]
                  done_status: closed
            """);

        var findings = NewValidator(out var loader).Validate(_tempFile);

        // What the server would publish for the same file, from the same rule object.
        var serverFindings = new AgentSmithConfigValidator().Findings(loader.LoadConfig(_tempFile));

        serverFindings.Should().NotBeEmpty();
        findings.Select(f => (f.Project, f.Trigger, f.Field, f.Severity))
            .Should().Contain(serverFindings.Select(f => (f.Project, f.Trigger, f.Field, f.Severity)));
        findings.Should().Contain(f => f.Field == "project_resolution" && f.Project == "demo");
    }

    [Fact]
    public void Cli_Validate_ValidConfig_ExitsZero()
    {
        Write("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo:
                agent: a
                tracker: t
                repos: [r]
                resolution: { tag: demo }
                github_trigger:
                  trigger_statuses: [open]
                  done_status: closed
                  needs_clarification_status: question
            """);

        var findings = NewValidator(out _).Validate(_tempFile);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Cli_Validate_UnresolvableReference_NamesTheProjectAndField()
    {
        Write("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo: { agent: ghost, tracker: t, repos: [r] }
            """);

        var findings = NewValidator(out _).Validate(_tempFile);

        findings.Should().ContainSingle(f =>
            f.Project == "demo" && f.Field == "agent" && f.IsBlocking);
    }

    [Fact]
    public void Cli_Validate_UnparseableFile_IsOneFindingNotACrash()
    {
        File.WriteAllText(_tempFile, "agents: [ this is not: valid: yaml");

        var findings = NewValidator(out _).Validate(_tempFile);

        findings.Should().ContainSingle(f =>
            f.Subsystem == StartupSubsystems.ConfigFile && f.IsBlocking);
    }

    [Fact]
    public void Print_FindingsCarryTheirField_SoSixMistakesAreSixLines()
    {
        var findings = new List<StartupFinding>
        {
            new(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
                "first", "one", "github_trigger", "needs_clarification_status"),
            new(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
                "second", "two", Field: "agent"),
        };
        var writer = new StringWriter();

        StartupFindingPrinter.Print(findings, writer);

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3); // one per finding plus the summary
        lines[0].Should().Contain("one").And.Contain("needs_clarification_status");
        lines[1].Should().Contain("two").And.Contain("agent");
        lines[2].Should().Contain("2 blocking");
    }

    [Fact]
    public void Print_NoFindings_SaysSo()
    {
        var writer = new StringWriter();

        StartupFindingPrinter.Print([], writer);

        writer.ToString().Should().Contain("no findings");
    }

    private void Write(string yaml) => File.WriteAllText(_tempFile, yaml);

    private ConfigValidator NewValidator(out YamlConfigurationLoader loader)
    {
        var findings = new StartupFindings();
        loader = new YamlConfigurationLoader(
            new RawConfigMaterializer(
                new ProjectConfigNormalizer(findings: findings),
                new EffectiveTriggerBuilder(),
                new DeploymentDefaultsApplier(),
                new ConfigCatalogResolver(findings: findings),
                new AgentSmithPaths(),
                findings),
            new NoOpSystemEventPublisher());
        return new ConfigValidator(loader, findings, new AgentSmithConfigValidator());
    }
}
