using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Access;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-10-15-4a4df: the one producer of binding findings, over a mapping and the configured
/// project names.
/// </summary>
public sealed class ProjectBindingFindingsTests
{
    private readonly ProjectBindingFindings _findings = new();

    [Fact]
    public void ProjectBindingFindings_RolesCarryingNoScopablePermission_GrantNothing()
    {
        var mapping = With(Binding("auditor"));
        mapping.Roles["auditor"] = ["config.read", "secrets.read"];

        _findings.Of(ResolvedRoleMapping.From(mapping, "sub"), ["billing"]).Should().Equal(
            "The project binding for 'billing' grants nothing: none of its roles carries a run, init or dialog permission.");
    }

    [Fact]
    public void ProjectBindingFindings_PersonRefOnAnotherClaim_IsReported()
    {
        var binding = Binding("operator");
        binding.People = [new PersonRef { Claim = "email", Value = "ada@example.com" }];

        _findings.Of(ResolvedRoleMapping.From(With(binding), "sub"), ["billing"]).Should().Equal(
            "A person in the project binding for 'billing' is written against the claim 'email', but callers "
            + "are named by 'sub'; it grants nothing until it is written again.");
    }

    [Fact]
    public void ProjectBindingFindings_RoleCarryingGlobalOnlyPermissions_IsNoFinding()
    {
        // admin carries config.* and secrets.* beside the run permissions; the binding drops
        // them silently and the pane shows them as ignored there.
        _findings.Of(ResolvedRoleMapping.From(With(Binding("admin")), "sub"), ["billing"]).Should().BeEmpty();
    }

    [Fact]
    public void ProjectBindingFindings_NoBindings_IsNoFinding()
    {
        _findings.Of(ResolvedRoleMapping.From(new RoleMappingConfig(), "sub"), []).Should().BeEmpty();
    }

    private static ProjectBinding Binding(string role) => new()
    {
        Project = "billing",
        Groups = ["billing-team"],
        People = [new PersonRef { Claim = "sub", Value = "ada" }],
        Roles = [role],
    };

    private static RoleMappingConfig With(ProjectBinding binding) =>
        new() { RoleClaim = "roles", ProjectBindings = [binding] };
}
