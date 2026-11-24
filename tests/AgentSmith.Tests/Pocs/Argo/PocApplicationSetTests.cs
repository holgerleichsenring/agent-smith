using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Pocs.Argo;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Argo;

/// <summary>2026-11-23-c81ec: the target's one ApplicationSet and the subset its install compares.</summary>
public sealed class PocApplicationSetTests
{
    private static JsonObject Render()
    {
        var target = PocsConfigTests.Sound().Targets[0];
        return new PocApplicationSet().Render(target, target.Argo!, "main");
    }

    [Fact]
    public void Render_DirectoriesGenerator_PocPrefix_AutomatedPruneSelfHeal_Finalizer_RequeueAfterSeconds()
    {
        var set = Render();

        (set["metadata"]!["name"]!.GetValue<string>(), set["metadata"]!["namespace"]!.GetValue<string>()).Should().Be(("pocs-main", "argocd"));
        var git = set["spec"]!["generators"]![0]!["git"]!;
        git["repoURL"]!.GetValue<string>().Should().Be("https://git.example.internal/platform/poc-src.git");
        git["revision"]!.GetValue<string>().Should().Be("main");
        git["directories"]![0]!["path"]!.GetValue<string>().Should().Be("deploy/*");
        git["requeueAfterSeconds"]!.GetValue<int>().Should().Be(30);
        var template = set["spec"]!["template"]!;
        template["metadata"]!["name"]!.GetValue<string>().Should().Be("poc-{{path.basename}}");
        template["metadata"]!["finalizers"]![0]!.GetValue<string>().Should().Be("resources-finalizer.argocd.argoproj.io");
        template["spec"]!["project"]!.GetValue<string>().Should().Be("pocs");
        template["spec"]!["source"]!["path"]!.GetValue<string>().Should().Be("{{path}}");
        template["spec"]!["destination"]!["namespace"]!.GetValue<string>().Should().Be("poc-{{path.basename}}");
        template["spec"]!["destination"]!["server"]!.GetValue<string>().Should().Be("https://kubernetes.default.svc");
        template["spec"]!["syncPolicy"]!["automated"]!.ToJsonString().Should().Be("""{"prune":true,"selfHeal":true}""");
    }

    [Fact]
    public void Match_ServerDefaultsBesideAnEqualSubset_Holds_AChangedValueOrAMissingStoreDoesNot()
    {
        var stored = (JsonObject)Render().DeepClone();
        stored["status"] = new JsonObject { ["conditions"] = new JsonArray() };
        ((JsonObject)stored["spec"]!["template"]!["metadata"]!)["labels"] = new JsonObject();
        ((JsonObject)stored["spec"]!["generators"]![0]!["git"]!)["files"] = new JsonArray();
        var match = new PocApplicationSetMatch();

        match.Holds(Render(), stored).Should().BeTrue();
        stored["spec"]!["template"]!["spec"]!["project"] = "default";
        match.Holds(Render(), stored).Should().BeFalse();
        match.Holds(Render(), null).Should().BeFalse();
    }
}
