using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283dc: a design turn's read-only scopes are its templates plus one reference scope
/// per uploaded website — and only the templates are a ticket's provenance.
/// </summary>
public sealed class SpecDialogReadOnlyScopesTests
{
    private static readonly ResolvedProject Project = new() { Name = "p" };

    [Fact]
    public async Task SpecDialogReadOnlyScopes_OpenAsync_AddsOneReferenceScopePerSet()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var first = await sets.AddAsync("s-1", [Css("My Site/a.css")], CancellationToken.None);
        var second = await sets.AddAsync("s-1", [Css("my-site/b.css")], CancellationToken.None);
        var references = new Mock<IReferenceSetSandboxFactory>();
        references.Setup(f => f.Create(Project, "s-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((ResolvedProject _, string _, string address, string _, string? _) => Scope(address));

        var opened = await Scopes(sets, references.Object).OpenAsync(Project, "s-1", CancellationToken.None);

        opened.Keys.Should().Equal("reference:my-site", "reference:my-site-2");
        references.Verify(f => f.Create(Project, "s-1", "reference:my-site", first.SetId, null));
        references.Verify(f => f.Create(Project, "s-1", "reference:my-site-2", second.SetId, null));
    }

    // 2026-10-02-075dd: a note written in one turn reaches the next turn's scope with its set.
    [Fact]
    public async Task SpecDialogReadOnlyScopes_OpenAsync_HandsEachSetItsNote()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var set = await sets.AddAsync("s-1", [Css("app/a.css")], CancellationToken.None);
        await new ReferenceNoteRepository(db).SetAsync("s-1", set.SetId, "open index.html", CancellationToken.None);
        var references = new Mock<IReferenceSetSandboxFactory>();
        references.Setup(f => f.Create(Project, "s-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((ResolvedProject _, string _, string address, string _, string? _) => Scope(address));

        await new SpecDialogReadOnlyScopes(
                new ProjectTemplateScopes(Mock.Of<ISourceScopeSandboxFactory>(), NullLogger<ProjectTemplateScopes>.Instance),
                sets, references.Object, NullLogger<SpecDialogReadOnlyScopes>.Instance, new ReferenceNoteRepository(db))
            .OpenAsync(Project, "s-1", CancellationToken.None);

        references.Verify(f => f.Create(Project, "s-1", "reference:app", set.SetId, "open index.html"));
    }

    [Fact]
    public void SpecDialogReadOnlyScopes_Stamp_RecordsTemplatesOnly()
    {
        var project = new ResolvedProject { Name = "p", Templates = [new ProjectTemplate("server", "default", "v1", new RepoConnection { Name = "server-repo" })] };
        var opened = new Dictionary<string, ISourceScopeSandbox>
        {
            ["template:server"] = Scope("server-repo"),
            ["reference:site"] = Scope("reference:site"),
        };

        var stamped = Scopes(new ReferenceSetRepository(Mock.Of<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>()),
                Mock.Of<IReferenceSetSandboxFactory>())
            .Stamp(new AnswerOutcome(), project, opened);

        stamped.Templates.Should().ContainSingle().Which.Address.Should().Be("template:server",
            "an uploaded website is not a template the work was built after");
    }

    private static SpecDialogReadOnlyScopes Scopes(ReferenceSetRepository sets, IReferenceSetSandboxFactory references) =>
        new(new ProjectTemplateScopes(Mock.Of<ISourceScopeSandboxFactory>(), NullLogger<ProjectTemplateScopes>.Instance),
            sets, references, NullLogger<SpecDialogReadOnlyScopes>.Instance);

    private static ISourceScopeSandbox Scope(string name) =>
        Mock.Of<ISourceScopeSandbox>(s => s.RepoName == name && s.ResolvedSha == "sha" && s.IsMaterialized == true);

    private static ReferenceFile Css(string path) => new() { RelativePath = path, MediaType = "text/css", Content = [0x61] };
}
