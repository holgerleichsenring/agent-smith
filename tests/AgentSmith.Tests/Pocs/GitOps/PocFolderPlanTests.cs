using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: what a deploy folder plan writes, deletes, keeps and misses.</summary>
public sealed class PocFolderPlanTests
{
    private readonly FakeGitOpsBranch _branch = new();
    private static readonly IReadOnlyList<PocRenderedFile> Rendered = [new("deployment.yaml", "kind: Deployment\n")];

    private async Task<PocFolderChanges> PlanAsync(bool unchanged = false, bool secrets = false)
    {
        var (plan, refused) = await new PocFolderPlan().PlanAsync(_branch.Factory().Create(null!), new BranchName("main"), "deploy/shop",
            Rendered, PocRenderRig.Seal(secrets, unchanged), 500, default);
        return plan ?? throw new InvalidOperationException(refused);
    }

    [Fact]
    public async Task Plan_AnUnchangedFolder_ChangesNothing()
    {
        _branch.Files["deploy/shop/deployment.yaml"] = "kind: Deployment\n";
        _branch.Files["deploy/shop/poc-pull.sealed.yaml"] = "kind: SealedSecret # old\n";

        (await PlanAsync(unchanged: true)).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Plan_AFileTheTemplateDropped_IsDeleted()
    {
        _branch.Files["deploy/shop/old.yaml"] = "kind: Old\n";

        (await PlanAsync()).Changes.Should().Contain(c => c.Delete && c.Path == "deploy/shop/old.yaml");
    }

    [Fact]
    public async Task Plan_AnUnchangedSeal_KeepsTheSealedFilesAsTheBranchHoldsThem()
    {
        _branch.Files["deploy/shop/poc-pull.sealed.yaml"] = "kind: SealedSecret # old\n";

        var plan = await PlanAsync(unchanged: true);

        plan.Changes.Should().NotContain(c => c.Path.EndsWith("sealed.yaml"));
        plan.Missing.Should().BeEmpty();
    }

    [Fact]
    public async Task Plan_AnUnchangedSealWithPocSecretsMissing_ReportsIt()
    {
        _branch.Files["deploy/shop/poc-pull.sealed.yaml"] = "kind: SealedSecret\n";

        (await PlanAsync(unchanged: true, secrets: true)).Missing.Should().Equal("poc-secrets.sealed.yaml");
    }

    [Fact]
    public async Task Plan_NothingLeftToSeal_DeletesTheSealedFiles()
    {
        _branch.Files["deploy/shop/poc-secrets.sealed.yaml"] = "kind: SealedSecret\n";

        (await PlanAsync()).Changes.Should().Contain(c => c.Delete && c.Path == "deploy/shop/poc-secrets.sealed.yaml");
    }
}
