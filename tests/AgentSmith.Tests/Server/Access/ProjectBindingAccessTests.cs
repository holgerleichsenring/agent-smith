using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Models;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-10-15-4a4da: project bindings survive every write the access surface makes — a save
/// carrying them, a save from a dashboard that does not know them yet, a person's removal —
/// and a binding is never a route to admin.
/// </summary>
public sealed class ProjectBindingAccessTests
{
    [Fact]
    public async Task Access_SaveWithProjectBindings_ReadsBackVerbatim()
    {
        using var h = new AccessTestHarness();

        await h.Writer.SaveAsync(Doc(Routed(Binding())), Actor, default);

        var view = await h.Reader.ViewAsync(CancellationToken.None);
        view.Document.ProjectBindings.Should().BeEquivalentTo([Binding()]);
    }

    [Fact]
    public async Task Access_SaveWithoutProjectBindingsField_KeepsTheStoredMappingValid()
    {
        using var h = new AccessTestHarness();
        var old = JsonDocument.Parse("""{"roleClaim":"roles","groupClaim":"groups","personGrants":[]}""");

        await h.Writer.SaveAsync(old.RootElement, Actor, default);

        h.Mapping.Current().Mapping.ProjectBindings.Should().BeEmpty();
        h.Mapping.Current().Mapping.RoleClaim.Should().Be("roles");
    }

    [Fact]
    public async Task PersonRemoval_OfAGrantedPerson_KeepsEveryProjectBinding()
    {
        using var h = new AccessTestHarness();
        var mapping = Routed(Binding());
        mapping.PersonGrants = [new PersonGrant { Claim = "sub", Value = "grace", Roles = ["operator"] }];
        h.Writer.Save(mapping, Actor);

        (await h.Remover.RemoveAsync("grace", Actor, default)).Should().BeTrue();

        h.Mapping.Current().Mapping.PersonGrants.Should().BeEmpty();
        h.Mapping.Current().Mapping.ProjectBindings.Should().BeEquivalentTo([Binding()]);
    }

    [Fact]
    public void Invariant_AdminOnlyInAProjectBinding_IsNoRouteToAdmin()
    {
        using var h = new AccessTestHarness();
        var adminInBinding = Binding();
        adminInBinding.Roles = ["admin"];
        var mapping = new RoleMappingConfig { RoleClaim = "", GroupClaim = "groups", ProjectBindings = [adminInBinding] };

        var save = () => h.Writer.Save(mapping, Actor);

        save.Should().Throw<ConfigurationException>().WithMessage("*no way to reach the admin role*");
    }

    private static ChangeAttribution Actor => new("tester");

    private static ProjectBinding Binding() => new()
    {
        Project = "billing",
        Groups = ["billing-team"],
        People = [new PersonRef { Claim = "sub", Value = "ada" }],
        Roles = ["operator"],
    };

    private static RoleMappingConfig Routed(ProjectBinding binding) =>
        new() { RoleClaim = "roles", GroupClaim = "groups", ProjectBindings = [binding] };

    private static JsonElement Doc(RoleMappingConfig mapping) =>
        JsonSerializer.SerializeToElement(mapping, new ConfigDocJson().Options);
}
