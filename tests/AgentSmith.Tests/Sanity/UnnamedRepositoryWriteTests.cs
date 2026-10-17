using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop.Sanity;
using FluentAssertions;

namespace AgentSmith.Tests.Sanity;

/// <summary>2026-10-17-a196a: a file write into a repository the spec does not name is refused before it runs.</summary>
public sealed class UnnamedRepositoryWriteTests
{
    private static readonly Dictionary<string, string> TwoRepositories = new()
    {
        ["shop-dotnet"] = "shop",
        ["shop-node"] = "shop",
        ["billing"] = "billing",
    };

    [Fact]
    public async Task RepositoryScopedWriteTool_WriteIntoUnnamedRepository_IsRefusedAndRecorded()
    {
        var pipeline = SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories);

        var text = await SanityCalls.Text(pipeline,
            "write_file", "Wrote 12 bytes to src/Invoice.cs", ("path", "billing/src/Invoice.cs"), ("content", "x"));

        text.Should().StartWith("Error: write_file into src/Invoice.cs of repository 'billing' was refused")
            .And.Contain("written into shop alone").And.Contain("Nothing was written");
        RefusedWriteLog.Of(pipeline).Refused.Should().ContainSingle().Which.Should().Contain("billing");
    }

    [Fact]
    public async Task RepositoryScopedWriteTool_WriteIntoNamedRepositoryOrRunRecord_Runs()
    {
        var pipeline = SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories);

        (await SanityCalls.Text(pipeline, "edit", "Edited", ("path", "shop-node/web/app.ts"))).Should().Be("Edited");
        (await SanityCalls.Text(pipeline, "write_file", "Wrote",
            ("path", "billing/.agentsmith/runs/r1/plan.md"))).Should().Be("Wrote",
            "the skill lets the master keep its run record in any repository");
        RefusedWriteLog.Of(pipeline).Refused.Should().BeEmpty();
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_SingleRepositoryBarePathOfAnUnnamedRepository_IsRefused()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["billing"]), "edit", "Edited src/a.cs", ("path", "src/a.cs"));

        text.Should().StartWith("Error: edit into src/a.cs of repository 'shop' was refused");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_SpecNamesNoRepositories_IsOff()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(map: TwoRepositories),
            "write_file", "Wrote", ("path", "billing/src/Invoice.cs"));

        text.Should().Be("Wrote");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_MultiEditDryRun_Runs()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "multi_edit", "dry_run: would apply 2 edit(s)", ("path", "billing/src/Invoice.cs"), ("dry_run", true));

        text.Should().Be("dry_run: would apply 2 edit(s)");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_UnprefixedPathOnAMultiRepositoryRun_IsNotJudged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "write_file", "Wrote", ("path", "src/Invoice.cs"));

        text.Should().Be("Wrote");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_ReadTool_IsNeverJudged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "read_file", "content", ("path", "billing/src/Invoice.cs"));

        text.Should().Be("content");
    }

    [Fact]
    public void MasterStepResult_RefusedWrites_AreNamed()
    {
        var pipeline = SanityCalls.Pipeline();
        RefusedWriteLog.OpenFresh(pipeline).Record("write_file into src/a.cs of repository 'billing' was refused");

        var result = MasterStepResult.Completed(pipeline, "coding-agent-master", 3);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().StartWith("Master 'coding-agent-master' completed: 3 files changed; 1 write(s) refused")
            .And.Contain("repository 'billing'");
        MasterStepResult.Completed(SanityCalls.Pipeline(), "m", 0).Message.Should().Be("Master 'm' completed: 0 files changed");
    }

    [Fact]
    public async Task RefusedWriteLog_ConcurrentRecords_AreAllKept()
    {
        var log = RefusedWriteLog.OpenFresh(SanityCalls.Pipeline());

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => log.Record($"r{i}"))));

        log.Refused.Should().HaveCount(200);
    }
}
