using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SpawnFixHandlerTests : IDisposable
{
    private readonly SpawnFixHandler _sut;
    private readonly string _tempDir;

    public SpawnFixHandlerTests()
    {
        var reporter = new Mock<IProgressReporter>();
        reporter.Setup(r => r.AskYesNoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new SpawnFixHandler(
            NullLogger<SpawnFixHandler>.Instance,
            Mock.Of<IDialogueTransport>(),
            Mock.Of<IDialogueTrail>(),
            reporter.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-fix-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_SkipsWithOk()
    {
        var config = new AutoFixConfig { Enabled = false };
        var pipeline = new PipelineContext();
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task ExecuteAsync_NoRepository_SkipsWithOk()
    {
        var config = new AutoFixConfig { Enabled = true };
        var pipeline = new PipelineContext();
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No repository");
    }

    [Fact]
    public async Task ExecuteAsync_NoFindings_SkipsWithOk()
    {
        var config = new AutoFixConfig { Enabled = true };
        var pipeline = CreatePipelineWithRepo();
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No findings");
    }

    [Fact]
    public async Task ExecuteAsync_OnlyLowFindings_SkipsWithOk()
    {
        var config = new AutoFixConfig { Enabled = true };
        var pipeline = CreatePipelineWithRepo();
        var findings = new List<Finding>
        {
            new("LOW", "src/app.cs", 10, null, "Info leak", "Minor issue", 5),
            new("MEDIUM", "src/app.cs", 20, null, "Weak hash", "Uses MD5", 6)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No fixable findings");
    }

    [Fact]
    public async Task ExecuteAsync_CriticalAndHighFindings_WritesFixFiles()
    {
        var config = new AutoFixConfig { Enabled = true, MaxConcurrent = 5 };
        var pipeline = CreatePipelineWithRepo();
        var findings = new List<Finding>
        {
            new("CRITICAL", "src/auth.cs", 10, null, "Hardcoded password", "Found hardcoded password [CWE-798]", 9),
            new("HIGH", "src/db.cs", 20, null, "SQL injection", "Possible SQL injection [CWE-89]", 8),
            new("MEDIUM", "src/log.cs", 5, null, "Info leak", "Information leakage", 6)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("2 fix requests");

        var fixesDir = Path.Combine(_tempDir, ".agentsmith", "security", "fixes");
        Directory.Exists(fixesDir).Should().BeTrue();
        Directory.GetFiles(fixesDir, "*.yaml").Should().HaveCount(2);

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
        requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_MaxConcurrent_LimitsGroups()
    {
        var config = new AutoFixConfig { Enabled = true, MaxConcurrent = 1 };
        var pipeline = CreatePipelineWithRepo();
        var findings = new List<Finding>
        {
            new("CRITICAL", "src/a.cs", 10, null, "Hardcoded password", "desc [CWE-798]", 9),
            new("HIGH", "src/b.cs", 20, null, "SQL injection", "desc [CWE-89]", 8),
            new("HIGH", "src/c.cs", 30, null, "XSS vulnerability", "desc [CWE-79]", 8)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
        requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludedPattern_FiltersOut()
    {
        var config = new AutoFixConfig
        {
            Enabled = true,
            ExcludedPatterns = ["test/", "vendor/"]
        };
        var pipeline = CreatePipelineWithRepo();
        var findings = new List<Finding>
        {
            new("CRITICAL", "test/auth.cs", 10, null, "Hardcoded password", "desc", 9),
            new("HIGH", "src/db.cs", 20, null, "SQL injection", "desc", 8)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
        requests.Should().HaveCount(1);
        requests![0].FilePath.Should().Be("src/db.cs");
    }

    [Fact]
    public async Task ExecuteAsync_CriticalOnlyThreshold_FiltersHighOut()
    {
        var config = new AutoFixConfig { Enabled = true, SeverityThreshold = "Critical" };
        var pipeline = CreatePipelineWithRepo();
        var findings = new List<Finding>
        {
            new("CRITICAL", "src/auth.cs", 10, null, "Hardcoded password", "desc", 9),
            new("HIGH", "src/db.cs", 20, null, "SQL injection", "desc", 8)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
        requests.Should().HaveCount(1);
        requests![0].FilePath.Should().Be("src/auth.cs");
    }

    [Fact]
    public async Task ExecuteAsync_GroupsByFileAndCategory()
    {
        var config = new AutoFixConfig { Enabled = true, MaxConcurrent = 10 };
        var pipeline = CreatePipelineWithRepo();
        var findings = new List<Finding>
        {
            new("HIGH", "src/auth.cs", 10, null, "Hardcoded password", "desc1", 9),
            new("HIGH", "src/auth.cs", 20, null, "Hardcoded secret", "desc2", 8),
            new("HIGH", "src/db.cs", 30, null, "SQL injection", "desc3", 8)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());
        var context = new SpawnFixContext(config, pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
        // "Hardcoded password" and "Hardcoded secret" share file=src/auth.cs, category="Hardcoded"
        requests.Should().HaveCount(2);
        var authRequest = requests!.First(r => r.FilePath == "src/auth.cs");
        authRequest.Items.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("CRITICAL", true)]
    [InlineData("HIGH", true)]
    [InlineData("MEDIUM", false)]
    [InlineData("LOW", false)]
    public void GetIncludedSeverities_DefaultThreshold_IncludesCriticalAndHigh(
        string severity, bool expected)
    {
        var severities = SecurityFixRequestBuilder.GetIncludedSeverities("High");

        severities.Contains(severity).Should().Be(expected);
    }

    [Fact]
    public void GetIncludedSeverities_CriticalThreshold_OnlyCritical()
    {
        var severities = SecurityFixRequestBuilder.GetIncludedSeverities("Critical");

        severities.Should().Contain("CRITICAL");
        severities.Should().NotContain("HIGH");
    }

    [Theory]
    [InlineData("Hardcoded password found", "Hardcoded")]
    [InlineData("SQL injection possible", "SQL")]
    [InlineData("XSS", "XSS")]
    public void ExtractCategory_ReturnsFirstWord(string title, string expected)
    {
        SecurityFixRequestBuilder.ExtractCategory(title).Should().Be(expected);
    }

    [Theory]
    [InlineData("Found issue [CWE-798]", "798")]
    [InlineData("SQL injection [CWE-89] detected", "89")]
    [InlineData("No CWE here", null)]
    [InlineData("", null)]
    public void ExtractCweId_ExtractsFromDescription(string description, string? expected)
    {
        SecurityFixRequestBuilder.ExtractCweId(description).Should().Be(expected);
    }

    [Fact]
    public void GenerateBranchName_WithCwe_IncludesCweId()
    {
        var finding = new Finding("HIGH", "src/auth.cs", 10, null,
            "Hardcoded password", "desc [CWE-798]", 9);

        var branch = SecurityFixRequestBuilder.GenerateBranchName(finding);

        branch.Should().StartWith("security-fix/cwe-798-");
        branch.Should().Contain("hardcoded");
    }

    [Fact]
    public void GenerateBranchName_WithoutCwe_OmitsCwePrefix()
    {
        var finding = new Finding("HIGH", "src/auth.cs", 10, null,
            "Hardcoded password", "desc without cwe", 9);

        var branch = SecurityFixRequestBuilder.GenerateBranchName(finding);

        branch.Should().StartWith("security-fix/hardcoded");
        branch.Should().NotContain("cwe-");
    }

    [Fact]
    public void GenerateBranchName_LongTitle_TruncatesSlug()
    {
        var finding = new Finding("HIGH", "src/auth.cs", 10, null,
            "This is a very long title that should be truncated to keep branch names reasonable",
            "desc", 9);

        var branch = SecurityFixRequestBuilder.GenerateBranchName(finding);

        // Branch name slug portion should be at most 40 chars
        var slugPart = branch.Replace("security-fix/", "");
        slugPart.Length.Should().BeLessThanOrEqualTo(40);
    }

    [Fact]
    public void SanitizeFileName_ReplacesSlashes()
    {
        SecurityFixRequestBuilder.SanitizeFileName("security-fix/cwe-798-hardcoded")
            .Should().Be("security-fix-cwe-798-hardcoded");
    }

    [Fact]
    public void IsExcluded_MatchingPattern_ReturnsTrue()
    {
        SecurityFixRequestBuilder.IsExcluded("test/auth.cs", ["test/", "vendor/"])
            .Should().BeTrue();
    }

    [Fact]
    public void IsExcluded_NoMatch_ReturnsFalse()
    {
        SecurityFixRequestBuilder.IsExcluded("src/auth.cs", ["test/", "vendor/"])
            .Should().BeFalse();
    }

    [Fact]
    public void SerializeFixRequest_ProducesValidYaml()
    {
        var request = new SecurityFixRequest(
            FilePath: "src/auth.cs",
            Category: "Hardcoded",
            SuggestedBranch: "security-fix/cwe-798-hardcoded-password",
            Items:
            [
                new SecurityFixItem("CRITICAL", "Hardcoded password", "Found password [CWE-798]", "798", 10),
                new SecurityFixItem("HIGH", "Hardcoded secret", "Found API key", null, 25)
            ]);

        var yaml = SecurityFixRequestBuilder.SerializeFixRequest(request);

        yaml.Should().Contain("file_path: src/auth.cs");
        yaml.Should().Contain("category: Hardcoded");
        yaml.Should().Contain("suggested_branch: security-fix/cwe-798-hardcoded-password");
        yaml.Should().Contain("items:");
        yaml.Should().Contain("  - severity: CRITICAL");
        yaml.Should().Contain("    cwe_id: 798");
        yaml.Should().Contain("    cwe_id:");
        yaml.Should().Contain("    line: 10");
        yaml.Should().Contain("    line: 25");
    }

    [Fact]
    public void SerializeFixRequest_EscapesQuotes()
    {
        var request = new SecurityFixRequest(
            FilePath: "src/app.cs",
            Category: "Injection",
            SuggestedBranch: "security-fix/injection",
            Items:
            [
                new SecurityFixItem("HIGH", "SQL \"injection\"", "Uses \"raw\" query", null, 5)
            ]);

        var yaml = SecurityFixRequestBuilder.SerializeFixRequest(request);

        yaml.Should().Contain("\\\"injection\\\"");
        yaml.Should().Contain("\\\"raw\\\"");
    }

    private PipelineContext CreatePipelineWithRepo()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.Repository, repo);
        return pipeline;
    }
}
