using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using AgentSmith.Tests.TestHelpers;
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
        references.Setup(f => f.Create(
                Project, "s-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReferenceCoverage?>()))
            .Returns((ResolvedProject _, string _, string address, string _, ReferenceCoverage? _) => Scope(address));

        var opened = await Scopes(sets, references.Object, db).OpenAsync(Project, "s-1", CancellationToken.None);

        opened.Keys.Should().Equal("reference:my-site", "reference:my-site-2");
        references.Verify(f => f.Create(Project, "s-1", "reference:my-site", first.SetId, It.IsAny<ReferenceCoverage?>()));
        references.Verify(f => f.Create(Project, "s-1", "reference:my-site-2", second.SetId, It.IsAny<ReferenceCoverage?>()));
    }

    [Fact]
    public async Task SpecDialogReadOnlyScopes_Stamp_RecordsTemplatesOnly()
    {
        var project = new ResolvedProject { Name = "p", Templates = [new ProjectTemplate("server", "default", "v1", new RepoConnection { Name = "server-repo" })] };
        var opened = new Dictionary<string, ISourceScopeSandbox>
        {
            ["template:server"] = Scope("server-repo"),
            ["reference:site"] = Scope("reference:site"),
        };

        var stamped = await Scopes(new ReferenceSetRepository(Mock.Of<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>()),
                Mock.Of<IReferenceSetSandboxFactory>())
            .StampAsync(new AnswerOutcome(), project, opened, CancellationToken.None);

        stamped.Templates.Should().ContainSingle().Which.Address.Should().Be("template:server",
            "an uploaded website is not a template the work was built after");
    }

    // 2026-11-04-96a0b: the keeper names a set the turn did not hold by comparing set ids, so the stamp carries every
    // upload the turn had open, on an answer and with no template open at all.
    [Fact]
    public async Task SpecDialogReadOnlyScopes_Stamp_CarriesEveryUploadTheTurnHadOpen()
    {
        var factory = new ReferenceSandboxFixture().Factory(AgentSmith.Tests.Sandbox.Holds.None());
        var opened = new Dictionary<string, ISourceScopeSandbox>
        {
            ["reference:site"] = factory.Create(Project, "s-1", "reference:site", "set-a"),
            ["reference:docs"] = factory.Create(Project, "s-1", "reference:docs", "set-b"),
        };

        var stamped = await Scopes(new ReferenceSetRepository(Mock.Of<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>()),
                Mock.Of<IReferenceSetSandboxFactory>())
            .StampAsync(new AnswerOutcome(), Project, opened, CancellationToken.None);

        stamped.Uploads.Should().BeEquivalentTo(
            [new UploadProvenance("reference:site", "set-a"), new UploadProvenance("reference:docs", "set-b")]);
        stamped.Templates.Should().BeEmpty();
    }

    // 2026-11-04-96a0c: a stored read of a path that is no file of the set — the marker the materialiser writes, or
    // a file since removed — is not counted, and a file read_file cannot open is counted apart.
    [Fact]
    public async Task SpecDialogReadOnlyScopes_OpenAsync_CountsOnlyPathsThatAreFilesOfTheSet()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var set = await sets.AddAsync("s-1", [Css("app/a.css"), Css("app/b.css"), Binary("app/logo.png")], CancellationToken.None);
        await new ReferenceReadRepository(db).AddAsync("s-1", set.SetId, ["app/a.css", ".agentsmith-reference", "app/logo.png"], default);
        var references = new Mock<IReferenceSetSandboxFactory>();
        ReferenceCoverage? handed = null;
        references.Setup(f => f.Create(
                Project, "s-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReferenceCoverage?>()))
            .Callback((ResolvedProject _, string _, string _, string _, ReferenceCoverage? coverage) => handed = coverage)
            .Returns((ResolvedProject _, string _, string address, string _, ReferenceCoverage? _) => Scope(address));

        await Scopes(sets, references.Object, db).OpenAsync(Project, "s-1", CancellationToken.None);

        handed!.Opened.Should().BeEquivalentTo(["app/a.css"]);
        handed.Files.Should().Be(2);
        handed.Unreadable.Should().Be(1);
        handed.Unopened.Should().Equal("app/b.css");
    }

    private static SpecDialogReadOnlyScopes Scopes(
        ReferenceSetRepository sets, IReferenceSetSandboxFactory references,
        AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork? store = null)
    {
        store ??= Mock.Of<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>();
        return new(new ProjectTemplateScopes(Mock.Of<ISourceScopeSandboxFactory>(), NullLogger<ProjectTemplateScopes>.Instance),
            sets, references, ReferenceReads.Coverage(store), ReferenceReads.Recorder(store),
            NullLogger<SpecDialogReadOnlyScopes>.Instance);
    }

    private static ISourceScopeSandbox Scope(string name) =>
        Mock.Of<ISourceScopeSandbox>(s => s.RepoName == name && s.ResolvedSha == "sha" && s.IsMaterialized == true);

    private static ReferenceFile Css(string path) => new() { RelativePath = path, MediaType = "text/css", Content = [0x61] };

    private static ReferenceFile Binary(string path) =>
        new() { RelativePath = path, MediaType = "image/png", Content = [0x89, 0x50, 0x4E, 0x47, 0xFF, 0xFE] };
}
