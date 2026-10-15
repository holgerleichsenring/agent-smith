using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-10-15-4a4df: the access view carries what the Projects pane needs — the configured
/// projects, the project-scopable permissions and the binding findings — and the save refuses
/// only a binding that names no project.
/// </summary>
public sealed class ProjectBindingSurfaceTests
{
    [Fact]
    public async Task AccessSurface_View_CarriesConfiguredProjectsSortedAndTheScopableSet()
    {
        using var h = new AccessTestHarness(projects: ["web", "Billing", "api"]);

        var view = await h.Reader.ViewAsync(CancellationToken.None);

        view.Projects.Should().Equal("api", "Billing", "web");
        view.ProjectScopable.Should().BeEquivalentTo(
            ["runs.read", "runs.watch", "runs.control", "runs.delete", "projects.init", "dialog.write"]);
    }

    [Fact]
    public async Task AccessSurface_DegradedEmptyConfig_ViewRendersWithNoProjectsAndTheBindingNotConfigured()
    {
        using var h = new AccessTestHarness();
        h.Writer.Save(Routed(Binding("billing")), Actor);

        var view = await h.Reader.ViewAsync(CancellationToken.None);

        view.Projects.Should().BeEmpty();
        view.Findings.Should().ContainSingle().Which.Should().Contain("'billing' names no configured project");
    }

    [Fact]
    public async Task AccessSurface_BindingDifferingOnlyInCase_IsConfiguredAndCarriesNoFinding()
    {
        using var h = new AccessTestHarness(projects: ["billing"]);
        h.Writer.Save(Routed(Binding("Billing")), Actor);

        (await h.Reader.ViewAsync(CancellationToken.None)).Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task AccessSurface_View_ShowsTheBindingFindings()
    {
        using var h = new AccessTestHarness(projects: ["billing"]);
        var binding = Binding("billing");
        binding.Roles = ["auditor"];
        var mapping = Routed(binding);
        mapping.Roles["auditor"] = ["config.read"];
        h.Writer.Save(mapping, Actor);

        var findings = (await h.Reader.ViewAsync(CancellationToken.None)).Findings;

        findings.Should().ContainSingle().Which.Should().Contain("'billing' grants nothing");
    }

    [Fact]
    public async Task AccessSurface_SaveBindingWithBlankProject_IsRefusedAndStoresNothing()
    {
        using var h = new AccessTestHarness(projects: ["billing"]);

        var save = () => h.Writer.SaveAsync(Doc(Routed(Binding(" "))), Actor, default);

        await save.Should().ThrowAsync<ConfigurationException>().WithMessage("A project binding names no project.");
        h.Mapping.Current().Mapping.ProjectBindings.Should().BeEmpty();
    }

    [Fact]
    public async Task AccessSurface_SaveBindingForUnconfiguredProject_IsStoredWithAFinding()
    {
        using var h = new AccessTestHarness(projects: ["billing"]);

        await h.Writer.SaveAsync(Doc(Routed(Binding("retired"))), Actor, default);

        h.Mapping.Current().Mapping.ProjectBindings.Should().ContainSingle(b => b.Project == "retired");
        (await h.Reader.ViewAsync(CancellationToken.None)).Findings
            .Should().ContainSingle().Which.Should().Contain("'retired' names no configured project");
    }

    private static ChangeAttribution Actor => new("tester");

    private static ProjectBinding Binding(string project) => new()
    {
        Project = project,
        Groups = ["billing-team"],
        People = [new PersonRef { Claim = "sub", Value = "ada" }],
        Roles = ["operator"],
    };

    private static RoleMappingConfig Routed(ProjectBinding binding) =>
        new() { RoleClaim = "roles", GroupClaim = "groups", ProjectBindings = [binding] };

    private static JsonElement Doc(RoleMappingConfig mapping) =>
        JsonSerializer.SerializeToElement(mapping, new ConfigDocJson().Options);
}
