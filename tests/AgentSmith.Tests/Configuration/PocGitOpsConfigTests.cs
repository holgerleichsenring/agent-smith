using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>2026-11-23-c81ea: pocs.gitops defaults and its refusals, each naming key, value and reason.</summary>
public sealed class PocGitOpsConfigTests
{
    [Fact]
    public void Problems_Defaults_None()
    {
        var config = new PocGitOpsConfig();

        config.CommitAttempts.Should().Be(3);
        config.MaxFolderFiles.Should().Be(500);
        config.Problems().Should().BeEmpty();
    }

    [Fact]
    public void Problems_ZeroCommitAttempts_RefusedNamingKeyValueAndReason()
    {
        new PocGitOpsConfig { CommitAttempts = 0 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("pocs.gitops.commit_attempts is 0").And.Contain("because");
    }

    [Fact]
    public void Problems_ZeroMaxFolderFiles_RefusedNamingKeyValueAndReason()
    {
        new PocGitOpsConfig { MaxFolderFiles = 0 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("pocs.gitops.max_folder_files is 0").And.Contain("handful of manifests");
    }
}
