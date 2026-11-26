using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Newtonsoft.Json;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-26-49d3a: a failed Azure build's evidence from the host's recorded answers — the VSTest run's failed result leads
/// (name, message, stack), so a PublishTestResults failure never hides which test broke; without test results the log tail
/// is the FIRST failed task's.
/// </summary>
public sealed class AzureReposTestEvidenceTests
{
    private static readonly Guid ProjectId = Guid.Parse("6c4f0b3e-1111-4222-8333-944455556666");
    private static readonly JsonSerializerSettings Sdk = new() { ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };
    private readonly Mock<BuildHttpClient> _build = Client<BuildHttpClient>();
    private readonly Mock<TestManagementHttpClient> _tests = Client<TestManagementHttpClient>();

    public AzureReposTestEvidenceTests()
    {
        _build.Setup(c => c.GetBuildTimelineAsync(ProjectId, 812, It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<Guid?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonConvert.DeserializeObject<Timeline>(TimelineJson, Sdk));
        _build.Setup(c => c.GetBuildLogLinesAsync(ProjectId, 812, 9, It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Starting test execution", "Failed OrderImportTests.Imports_the_batch [2 s]", "Total tests: 214, Failed: 1"]);
        _build.Setup(c => c.GetBuildLogLinesAsync(ProjectId, 812, 11, It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Publishing test results", "##[error]There are one or more test failures."]);
        _tests.Setup(c => c.GetTestRunsAsync(ProjectId, "vstfs:///Build/Build/812", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonConvert.DeserializeObject<List<TestRun>>(Value(RunsJson), Sdk)!);
        _tests.Setup(c => c.GetTestResultsAsync(ProjectId, 1301, It.IsAny<ResultDetails?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<IEnumerable<TestOutcome>>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonConvert.DeserializeObject<List<TestCaseResult>>(Value(ResultsJson), Sdk)!);
    }

    [Fact]
    public async Task OneFailedVsTestResult_PublishTestResultsFailed_TheTestNameMessageAndStackLead()
    {
        var excerpt = await ExcerptAsync(withTests: true);

        excerpt.Should().StartWith("test failed: Shop.Integration.OrderImportTests.Imports_the_batch")
            .And.Contain("Expected 12 orders, found 11")
            .And.Contain("at Shop.Integration.OrderImportTests.Imports_the_batch() in /src/OrderImportTests.cs:line 48")
            .And.Contain("task failed: PublishTestResults")
            .And.NotContain(CiExcerpt.LogMark, "a failed test result says it; no log is read then");
        _build.Verify(c => c.GetBuildLogLinesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<long?>(),
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoTestResults_OrARefusedTestRead_TailTheFirstFailedTasksLog()
    {
        (await ExcerptAsync(withTests: false)).Should().EndWith("Total tests: 214, Failed: 1");

        _tests.Setup(c => c.GetTestRunsAsync(ProjectId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("TF400813: not authorized to access test runs"));
        (await ExcerptAsync(withTests: true)).Should().EndWith("Total tests: 214, Failed: 1");
    }

    private Task<string> ExcerptAsync(bool withTests) =>
        AzureReposBuildEvidence.ExcerptAsync(_build.Object, ProjectId, 812, new CiReadRequest(null, 4000, 5), CancellationToken.None,
            withTests ? _ => Task.FromResult(_tests.Object) : null);

    private static string Value(string list) => Newtonsoft.Json.Linq.JObject.Parse(list)["value"]!.ToString();

    private static Mock<T> Client<T>() where T : class =>
        new(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));

    // Recorded from GET _apis/build/builds/812/timeline (trimmed to the records the evidence reads).
    private const string TimelineJson = """
        {"records":[
          {"id":"0b6c1f2e-0000-4000-8000-000000000a01","parentId":"0b6c1f2e-0000-4000-8000-000000000b01","type":"Task","name":"VSTest","startTime":"2026-11-26T08:01:10Z","state":"completed","result":"failed",
           "log":{"id":9},"issues":[{"type":"error","message":"Test Run Failed."}],"order":5},
          {"id":"0b6c1f2e-0000-4000-8000-000000000a02","parentId":"0b6c1f2e-0000-4000-8000-000000000b01","type":"Task","name":"PublishTestResults","startTime":"2026-11-26T08:03:40Z","state":"completed","result":"failed",
           "log":{"id":11},"issues":[{"type":"error","message":"There are one or more test failures."}],"order":6},
          {"id":"0b6c1f2e-0000-4000-8000-000000000b01","type":"Job","name":"Build and test","state":"completed","result":"failed"}
        ],"id":"0b6c1f2e-0000-4000-8000-000000000071","changeId":14}
        """;

    // Recorded from GET _apis/test/runs?buildUri=vstfs:///Build/Build/812.
    private const string RunsJson = """
        {"count":1,"value":[{"id":1301,"name":"VSTest_TestResults_812","url":"https://dev.azure.com/example/demo/_apis/test/Runs/1301",
          "isAutomated":true,"state":"Completed","totalTests":214,"incompleteTests":0,"notApplicableTests":0,"passedTests":213,
          "unanalyzedTests":1,"webAccessUrl":"https://dev.azure.com/example/demo/_TestManagement/Runs?runId=1301"}]}
        """;

    // Recorded from GET _apis/test/Runs/1301/results?outcomes=Failed.
    private const string ResultsJson = """
        {"count":1,"value":[{"id":100000,"outcome":"Failed","testCaseTitle":"Imports_the_batch",
          "automatedTestName":"Shop.Integration.OrderImportTests.Imports_the_batch","automatedTestStorage":"Shop.Integration.Tests.dll",
          "errorMessage":"Expected 12 orders, found 11.","durationInMs":2140.0,
          "stackTrace":"   at Shop.Integration.OrderImportTests.Imports_the_batch() in /src/OrderImportTests.cs:line 48\n   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)\n"}]}
        """;
}
