using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-16-4df5: how a template declaration is ADDRESSED, stated once.
/// <para>
/// Three places rebuilt this name from the prefix and the context by hand, and they behaved
/// differently on the same input: the scope selection discarded a duplicate, the spec-dialog
/// stamp RAISED on it, and the proof report keyed its evidence by it and proved the wrong
/// template. A project whose Client and BackgroundWorker both declare 'default' hit all three.
/// </para>
/// </summary>
public sealed class ProjectTemplateScopeNameTests
{
    [Fact]
    public void Scopes_UnqualifiedDeclaration_KeepsTodaysName() =>
        TemplateScopeName.For(Template("default", contextRepo: null))
            .Should().Be("template:default",
                "every stored declaration names no repository, and it must stay addressed "
                + "exactly as it is — the pinned prompt assertions read this shape");

    [Fact]
    public void Scopes_QualifiedDeclaration_IsNamedAfterItsRepo() =>
        TemplateScopeName.For(Template("default", "Sample.Client"))
            .Should().Be("template:Sample.Client/default");

    [Fact]
    public void Scopes_TwoReposDeclaringOneContextName_AreTwoNames()
    {
        var client = TemplateScopeName.For(Template("default", "Sample.Client"));
        var worker = TemplateScopeName.For(Template("default", "Sample.Worker"));

        client.Should().NotBe(worker,
            "one address for both is what silently dropped the second declaration");
    }

    [Fact]
    public void Scopes_BlankContextRepo_ReadsAsUnqualified() =>
        TemplateScopeName.For(Template("default", "   "))
            .Should().Be("template:default",
                "a field the operator opened and left empty says nothing, not an empty repo");

    private static ProjectTemplate Template(string context, string? contextRepo) =>
        new(context, "server", Revision: null, new RepoConnection { Name = "reference" }, contextRepo);
}
