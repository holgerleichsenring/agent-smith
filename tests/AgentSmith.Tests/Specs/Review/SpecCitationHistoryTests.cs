using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762n: what git says about a citation — real git, a bare origin and one clone.</summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SpecCitationHistoryTests
{
    private const string Code = "src/Api/Orders.cs";
    private const string Token = Code + ":2-3";

    private static Dictionary<string, ISandbox> Only(CitationRepo repo) => new() { [repo.Name] = repo.Sandbox };

    [Fact]
    public async Task SpecCitationHistory_RealGitRepo_FindsWritingCommitAndOldText()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        await repo.WriteAsync(Code, CitationRepo.Numbered("old", 5));
        await repo.CommitAsync("code", CitationRepo.At(1));
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, "README.md"));
        await repo.CommitAsync("spec added", CitationRepo.At(2));
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, Token));
        var written = await repo.CommitAsync("cite", CitationRepo.At(3));
        await repo.WriteAsync(Code, CitationRepo.Numbered("new", 5));
        await repo.CommitAsync("code changed", CitationRepo.At(4));
        var history = CitationRepo.History();

        var write = await history.WriteAsync(Only(repo), CitationRepo.SpecId, Token, CancellationToken.None);

        write.Should().Be(new SpecCitationWrite("shop", written, CitationRepo.At(3), IsAdded: false));
        var at = await history.BaseAsync(repo.Sandbox, "shop", write!, approvedAt: null, CancellationToken.None);
        at.Should().Be(written);
        (await history.OldTextAsync(repo.Sandbox, at!, Code, CancellationToken.None)).Should().Be(CitationRepo.Numbered("old", 5));
        (await history.UnchangedAsync(repo.Sandbox, at!, Code, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task SpecCitationHistory_RealGitRepo_OtherSpecFileRewritingTheToken_DoesNotMoveTheWritingCommit()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, Token));
        var written = await repo.CommitAsync("spec", CitationRepo.At(1));
        await repo.WriteAsync(CitationRepo.SpecPath("2026-10-14-1111b"), CitationRepo.SpecYaml("2026-10-14-1111b", Token));
        await repo.CommitAsync("another spec cites it", CitationRepo.At(2));
        await repo.WriteAsync(CitationRepo.SpecPath("2026-10-14-1111b"), CitationRepo.SpecYaml("2026-10-14-1111b", Code + ":7-8"));
        await repo.CommitAsync("another spec re-grounds it", CitationRepo.At(3));

        var write = await CitationRepo.History().WriteAsync(Only(repo), CitationRepo.SpecId, Token, CancellationToken.None);

        write!.Sha.Should().Be(written, "only the spec's own file dates its citation");
        write.IsAdded.Should().BeTrue();
    }

    [Fact]
    public async Task SpecCitationHistory_RealGitRepo_LongerTokenContainingTheToken_DoesNotMoveTheWritingCommit()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, Code + ":12"));
        var written = await repo.CommitAsync("spec", CitationRepo.At(1));
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, Code + ":12", Code + ":120-125"));
        await repo.CommitAsync("a longer token", CitationRepo.At(2));
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, Code + ":12", Code + ":12,14"));
        await repo.CommitAsync("a token with a second line", CitationRepo.At(3));

        var write = await CitationRepo.History().WriteAsync(Only(repo), CitationRepo.SpecId, Code + ":12", CancellationToken.None);

        write!.Sha.Should().Be(written, "':12' inside ':120-125' or ':12,14' is not the token");
    }

    [Fact]
    public async Task SpecCitationHistory_RealGitRepo_ApprovedFileAddedAtFiling_BaseIsDefaultBranchAtApproval()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        await repo.WriteAsync(Code, CitationRepo.Numbered("read", 5));
        var read = await repo.CommitAsync("the code the partner read", CitationRepo.At(1));
        await repo.WriteAsync(Code, CitationRepo.Numbered("later", 5));
        await repo.CommitAsync("landed after the approval", CitationRepo.At(3));
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, Token));
        await repo.CommitAsync("filed", CitationRepo.At(4));
        var history = CitationRepo.History();

        var write = await history.WriteAsync(Only(repo), CitationRepo.SpecId, Token, CancellationToken.None);
        var at = await history.BaseAsync(repo.Sandbox, "shop", write!, CitationRepo.At(2), CancellationToken.None);

        write!.IsAdded.Should().BeTrue();
        at.Should().Be(read, "the approved text describes the default branch as of the approval");
    }

    [Fact]
    public async Task SpecCitationHistory_RealGitRepo_FeatureCommitMergedAfterApproval_IsNotTheBase()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        await repo.WriteAsync(Code, CitationRepo.Numbered("main", 5));
        var main = await repo.CommitAsync("main", CitationRepo.At(1));
        await repo.GitAsync(null, "checkout", "-q", "-b", "feature");
        await repo.WriteAsync("src/Feature.cs", "feature\n");
        var feature = await repo.CommitAsync("feature work dated before the approval", CitationRepo.At(2), push: false);
        await repo.GitAsync(null, "checkout", "-q", "main");
        await repo.GitAsync(CitationRepo.At(4), "merge", "-q", "--no-ff", "-m", "merged after the approval", "feature");
        await repo.GitAsync(null, "push", "-q", "origin", "HEAD:main");
        await repo.WriteAsync(CitationRepo.SpecPath(), CitationRepo.SpecYaml(CitationRepo.SpecId, Token));
        await repo.CommitAsync("filed", CitationRepo.At(5));
        var history = CitationRepo.History();

        var write = await history.WriteAsync(Only(repo), CitationRepo.SpecId, Token, CancellationToken.None);
        var at = await history.BaseAsync(repo.Sandbox, "shop", write!, CitationRepo.At(3), CancellationToken.None);

        at.Should().Be(main).And.NotBe(feature, "the default branch never held the feature commit's tree at the approval");
    }

    [Fact]
    public async Task SpecCitationHistory_RealGitRepo_ElevenCandidates_NotFound()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        const string line = "public const int MaxOrderLines = 40;";
        for (var i = 1; i <= 11; i++) await repo.WriteAsync($"src/Copy{i}.cs", $"// copy {i}\n{line}\n");
        await repo.CommitAsync("eleven copies", CitationRepo.At(1));

        var grepped = await CitationRepo.History().CandidatesAsync(repo.Sandbox, line, CancellationToken.None);

        grepped.Should().HaveCount(11);
        SpecCitationLocator.Candidates([], grepped!, Code).Should().BeNull("more than ten candidate files is not found");
        (await CitationRepo.History().CandidatesAsync(repo.Sandbox, "nowhere at all in this tree", CancellationToken.None))
            .Should().BeEmpty();
    }
}
