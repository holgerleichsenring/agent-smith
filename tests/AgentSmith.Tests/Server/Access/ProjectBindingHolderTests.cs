using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-10-15-4a4df: a project binding is a holder — forgetting a person takes them out of every
/// binding, and removing a role a binding still uses is refused with the binding named.
/// </summary>
public sealed class ProjectBindingHolderTests
{
    [Fact]
    public async Task PersonRemover_Forget_KeepsOtherProjectBindings()
    {
        using var h = new AccessTestHarness();
        h.Writer.Save(Routed(Binding("billing", "grace"), Binding("web", "linus")), Actor);

        await h.Remover.RemoveAsync("grace", Actor, default);

        h.Mapping.Current().Mapping.ProjectBindings.Should().ContainSingle()
            .Which.People.Should().ContainSingle(p => p.Value == "linus");
    }

    [Fact]
    public async Task PersonRemover_Forget_WithdrawsThePersonFromEveryBindingAndDropsAnEmptiedOne()
    {
        using var h = new AccessTestHarness();
        var shared = Binding("web", "grace");
        shared.Groups = ["web-team"];
        h.Writer.Save(Routed(Binding("billing", "grace"), shared), Actor);

        await h.Remover.RemoveAsync("grace", Actor, default);

        var kept = h.Mapping.Current().Mapping.ProjectBindings.Should().ContainSingle().Subject;
        kept.Project.Should().Be("web");
        kept.People.Should().BeEmpty();
        kept.Groups.Should().Equal("web-team");
    }

    [Fact]
    public async Task PersonRemover_PersonOnlyInABinding_ForgetAnswersTrueAndSaves()
    {
        using var h = new AccessTestHarness();
        h.Writer.Save(Routed(Binding("billing", "grace")), Actor);

        (await h.Remover.RemoveAsync("grace", Actor, default)).Should().BeTrue();

        h.Mapping.Current().Mapping.ProjectBindings.Should().BeEmpty();
    }

    [Fact]
    public async Task PersonRemover_Forget_KeepsABindingThatWasAlreadyMemberless()
    {
        using var h = new AccessTestHarness();
        var memberless = Binding("web");
        h.Writer.Save(Routed(Binding("billing", "grace"), memberless), Actor);

        await h.Remover.RemoveAsync("grace", Actor, default);

        h.Mapping.Current().Mapping.ProjectBindings.Should().ContainSingle(b => b.Project == "web");
    }

    [Fact]
    public async Task RoleRemovalGuard_RoleHeldOnlyByAProjectBinding_IsRefusedNamingTheBinding()
    {
        using var h = new AccessTestHarness();
        var binding = Binding("billing", "grace");
        binding.Groups = ["billing-team"];
        binding.Roles = ["deployer"];
        var mapping = Routed(binding);
        mapping.Roles["deployer"] = ["runs.control"];
        h.Writer.Save(mapping, Actor);
        mapping.Roles.Clear();

        var save = () => h.Writer.SaveAsync(Doc(mapping), Actor, default);

        await save.Should().ThrowAsync<ConfigurationException>().WithMessage(
            "*billing-team in billing (project binding), sub:grace in billing (project binding)*");
    }

    private static ChangeAttribution Actor => new("tester");

    private static ProjectBinding Binding(string project, params string[] people) => new()
    {
        Project = project,
        People = [.. people.Select(value => new PersonRef { Claim = "sub", Value = value })],
        Roles = ["operator"],
    };

    private static RoleMappingConfig Routed(params ProjectBinding[] bindings) =>
        new() { RoleClaim = "roles", GroupClaim = "groups", ProjectBindings = [.. bindings] };

    private static JsonElement Doc(RoleMappingConfig mapping) =>
        JsonSerializer.SerializeToElement(mapping, new ConfigDocJson().Options);
}
