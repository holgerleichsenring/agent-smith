using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SpawnFixHandlerTests
{
    private readonly SpawnFixHandler _sut;
    private readonly Mock<ISandboxFileReader> _readerMock = new();
    private readonly List<(string Path, string Content)> _written = new();

    public SpawnFixHandlerTests()
    {
        var reporter = new Mock<IProgressReporter>();
        reporter.Setup(r => r.AskYesNoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _readerMock.Setup(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((p, c, _) => _written.Add((p, c)))
            .Returns(Task.CompletedTask);

        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(_readerMock.Object);

        _sut = new SpawnFixHandler(
            factory.Object,
            NullLogger<SpawnFixHandler>.Instance,
            Mock.Of<IDialogueTransport>(),
            Mock.Of<IDialogueTrail>(),
            reporter.Object);
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
    public async Task ExecuteAsync_NoObservations_SkipsWithOk()
    {
        var config = new AutoFixConfig { Enabled = true };
        var pipeline = CreatePipelineWithRepo();
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No observations");
    }

    [Fact]
    public async Task ExecuteAsync_OnlyLowObservations_SkipsWithOk()
    {
        var config = new AutoFixConfig { Enabled = true };
        var pipeline = CreatePipelineWithRepo();
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("LOW", "src/app.cs", 10, "Info leak", "Minor issue", 5),
            ObservationFactory.Make("MEDIUM", "src/app.cs", 20, "Weak hash", "Uses MD5", 6)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No fixable findings");
    }

    [Fact]
    public async Task ExecuteAsync_HighObservations_WritesFixFiles()
    {
        var config = new AutoFixConfig { Enabled = true, MaxConcurrent = 5 };
        var pipeline = CreatePipelineWithRepo();
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/auth.cs", 10, "Hardcoded password", "Found hardcoded password [CWE-798]", 9),
            ObservationFactory.Make("HIGH", "src/db.cs", 20, "SQL injection", "Possible SQL injection [CWE-89]", 8),
            ObservationFactory.Make("MEDIUM", "src/log.cs", 5, "Info leak", "Information leakage", 6)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("2 fix requests");

        _written.Should().HaveCount(2);
        _written.Should().OnlyContain(w => w.Path.Contains(".agentsmith/security/fixes")
            && w.Path.EndsWith(".yaml"));

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
        requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_MaxConcurrent_LimitsGroups()
    {
        var config = new AutoFixConfig { Enabled = true, MaxConcurrent = 1 };
        var pipeline = CreatePipelineWithRepo();
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/a.cs", 10, "Hardcoded password", "desc [CWE-798]", 9),
            ObservationFactory.Make("HIGH", "src/b.cs", 20, "SQL injection", "desc [CWE-89]", 8),
            ObservationFactory.Make("HIGH", "src/c.cs", 30, "XSS vulnerability", "desc [CWE-79]", 8)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
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
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "test/auth.cs", 10, "Hardcoded password", "desc", 9),
            ObservationFactory.Make("HIGH", "src/db.cs", 20, "SQL injection", "desc", 8)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        var context = new SpawnFixContext(config, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
        requests.Should().HaveCount(1);
        requests![0].FilePath.Should().Be("src/db.cs");
    }

    [Fact]
    public async Task ExecuteAsync_GroupsByFileAndCategory()
    {
        var config = new AutoFixConfig { Enabled = true, MaxConcurrent = 10 };
        var pipeline = CreatePipelineWithRepo();
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/auth.cs", 10, "Hardcoded password", "desc1", 9, category: "secrets"),
            ObservationFactory.Make("HIGH", "src/auth.cs", 20, "Hardcoded secret", "desc2", 8, category: "secrets"),
            ObservationFactory.Make("HIGH", "src/db.cs", 30, "SQL injection", "desc3", 8, category: "injection")
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);
        var context = new SpawnFixContext(config, pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<IReadOnlyList<SecurityFixRequest>>(
            ContextKeys.SecurityFixRequests, out var requests).Should().BeTrue();
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
        var obs = ObservationFactory.Make("HIGH", "src/auth.cs", 10,
            "Hardcoded password", "desc [CWE-798]", 9);

        var branch = SecurityFixRequestBuilder.GenerateBranchName(obs);

        branch.Should().StartWith("security-fix/cwe-798-");
        branch.Should().Contain("hardcoded");
    }

    [Fact]
    public void GenerateBranchName_WithoutCwe_OmitsCwePrefix()
    {
        var obs = ObservationFactory.Make("HIGH", "src/auth.cs", 10,
            "Hardcoded password", "desc without cwe", 9);

        var branch = SecurityFixRequestBuilder.GenerateBranchName(obs);

        branch.Should().StartWith("security-fix/hardcoded");
        branch.Should().NotContain("cwe-");
    }

    [Fact]
    public void GenerateBranchName_LongTitle_TruncatesSlug()
    {
        var obs = ObservationFactory.Make("HIGH", "src/auth.cs", 10,
            "This is a very long title that should be truncated to keep branch names reasonable",
            "desc", 9);

        var branch = SecurityFixRequestBuilder.GenerateBranchName(obs);

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
        var repo = new Repository(new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.Repository, repo);
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return pipeline;
    }
}
