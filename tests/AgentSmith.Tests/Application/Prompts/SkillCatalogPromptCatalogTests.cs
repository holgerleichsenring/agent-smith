using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.PromptCatalog;

public sealed class SkillCatalogPromptCatalogTests
{
    private static SkillCatalogPromptCatalog Build(
        IReadOnlyList<RoleSkillDefinition> skills,
        IDictionary<string, string> embeddedFallback,
        bool catalogReady = true)
    {
        var inner = new StubInnerPromptCatalog(embeddedFallback);
        var loader = new StubSkillLoader(skills);
        var path = new StubCatalogPath(catalogReady ? "/tmp/fake-root" : null);
        return new SkillCatalogPromptCatalog(
            inner, loader, path, new VerbatimBodyResolver(),
            NullLogger<SkillCatalogPromptCatalog>.Instance);
    }

    [Fact]
    public void Get_AgentExecuteSystem_RoutesToCodingAgentMaster()
    {
        var sut = Build(
            skills: [Master("coding-agent-master", "MASTER_BODY")],
            embeddedFallback: new Dictionary<string, string> { ["agent-execute-system"] = "EMBEDDED" });

        sut.Get("agent-execute-system").Should().Be("MASTER_BODY");
    }

    [Fact]
    public void Get_AgentPlanSystem_StaysOnEmbeddedUntilSliceC()
    {
        // p0179a: only agent-execute-system routes to coding-agent-master.
        // agent-plan-system remains embedded until p0179c collapses Plan +
        // Execute + Verify into one unified body — combining a JSON-returning
        // plan prompt with a multi-turn execute prompt would confuse the LLM.
        var sut = Build(
            skills: [Master("coding-agent-master", "MASTER_BODY")],
            embeddedFallback: new Dictionary<string, string> { ["agent-plan-system"] = "EMBEDDED_PLAN" });

        sut.Get("agent-plan-system").Should().Be("EMBEDDED_PLAN");
    }

    [Fact]
    public void Get_UnknownName_FallsBackToEmbedded()
    {
        var sut = Build(
            skills: [],
            embeddedFallback: new Dictionary<string, string> { ["triage-system"] = "TRIAGE_PROMPT" });

        sut.Get("triage-system").Should().Be("TRIAGE_PROMPT");
    }

    [Fact]
    public void Get_NameMappedButMasterNotInCatalog_ThrowsLoud()
    {
        // p0205: a migrated prompt whose master is absent from the catalog must
        // FAIL LOUD — no silent embedded fallback that masks version drift.
        var sut = Build(
            skills: [],
            embeddedFallback: new Dictionary<string, string> { ["knowledge-system"] = "KNOWLEDGE_EMBEDDED" });

        var act = () => sut.Get("knowledge-system");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*knowledge-master*");
    }

    [Fact]
    public void Get_CatalogNotReady_MigratedPrompt_ThrowsLoud()
    {
        // p0205: catalog not ready → a migrated prompt has no source; fail loud
        // rather than quietly serving the embedded copy.
        var sut = Build(
            skills: [Master("coding-agent-master", "MASTER_BODY")],
            embeddedFallback: new Dictionary<string, string> { ["agent-execute-system"] = "EMBEDDED" },
            catalogReady: false);

        var act = () => sut.Get("agent-execute-system");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*coding-agent-master*");
    }

    [Fact]
    public void Render_AppliesTokenSubstitution_AfterMasterHit()
    {
        var sut = Build(
            skills: [Master("coding-agent-master", "Hello {Name}, target is {Target}")],
            embeddedFallback: new Dictionary<string, string>());

        var rendered = sut.Render(
            "agent-execute-system",
            new Dictionary<string, string> { ["Name"] = "Master", ["Target"] = "auth.svc" });

        rendered.Should().Be("Hello Master, target is auth.svc");
    }

    [Fact]
    public void Render_AppliesTokenSubstitution_OnEmbeddedFallback()
    {
        var sut = Build(
            skills: [],
            embeddedFallback: new Dictionary<string, string> { ["triage-system"] = "Hello {Name}" });

        var rendered = sut.Render(
            "triage-system",
            new Dictionary<string, string> { ["Name"] = "Triage" });

        rendered.Should().Be("Hello Triage");
    }

    [Fact]
    public void GetMasterCatalog_ReadsFromSkillsSubpath_NotTarballRoot()
    {
        // p0179g: SkillCatalogPromptCatalog must pass {Root}/skills, not
        // {Root}, to the skill loader. The tarball extracts to a wrapper
        // directory with baselines/ patterns/ skills/ siblings; the actual
        // skills tree (including _masters/) lives one level deeper.
        // ExecutePipelineUseCase already composes Path.Combine(Root, "skills");
        // this test pins the same composition here so the next refactor of
        // ISkillsCatalogPath cannot silently regress the master lookup path.
        var inner = new StubInnerPromptCatalog(
            new Dictionary<string, string> { ["agent-execute-system"] = "EMBEDDED" });
        var loader = new StubSkillLoader([Master("coding-agent-master", "MASTER_BODY")]);
        var path = new StubCatalogPath("/tmp/fake-root");
        var sut = new SkillCatalogPromptCatalog(
            inner, loader, path, new VerbatimBodyResolver(),
            NullLogger<SkillCatalogPromptCatalog>.Instance);

        // Triggering Get materialises the master catalog and records the path
        // the loader was called with.
        sut.Get("agent-execute-system").Should().Be("MASTER_BODY");

        loader.LastLoadDirectory.Should().Be(
            Path.Combine("/tmp/fake-root", "skills"),
            "the loader must walk the skills subtree, not the tarball wrapper");
    }

    [Fact]
    public void Get_OnlyNonMasterSkillsLoaded_MigratedPrompt_ThrowsLoud()
    {
        // A non-master skill must not satisfy a master-mapped name even if the
        // names collide — and with no real master present, p0205 fails loud
        // rather than serving the embedded copy.
        var sut = Build(
            skills:
            [
                new RoleSkillDefinition
                {
                    Name = "coding-agent-master",
                    Role = "investigator",
                    Rules = "WRONG_BODY",
                },
            ],
            embeddedFallback: new Dictionary<string, string> { ["agent-execute-system"] = "EMBEDDED" });

        var act = () => sut.Get("agent-execute-system");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*coding-agent-master*");
    }

    private static RoleSkillDefinition Master(string name, string body) => new()
    {
        Name = name,
        Role = "master",
        Description = "test master",
        Rules = body,
    };

    private sealed class StubInnerPromptCatalog(IDictionary<string, string> map) : IPromptCatalog
    {
        public string Get(string name) =>
            map.TryGetValue(name, out var content) ? content :
                throw new InvalidOperationException($"Embedded miss for '{name}'");
        public string Render(string name, IReadOnlyDictionary<string, string> tokens)
        {
            var c = Get(name);
            foreach (var (k, v) in tokens) c = c.Replace("{" + k + "}", v);
            return c;
        }
    }

    private sealed class StubSkillLoader(IReadOnlyList<RoleSkillDefinition> skills) : ISkillLoader
    {
        public string? LastLoadDirectory { get; private set; }
        public IReadOnlyList<RoleSkillDefinition> LoadRoleDefinitions(string skillsDirectory)
        {
            LastLoadDirectory = skillsDirectory;
            return skills;
        }
        public SkillConfig? LoadProjectSkills(string agentSmithDirectory) => null;
        public IReadOnlyList<RoleSkillDefinition> GetActiveRoles(
            IReadOnlyList<RoleSkillDefinition> allRoles, SkillConfig projectSkills) => allRoles;
        public ConceptVocabulary LoadVocabulary(string skillsDirectory) => ConceptVocabulary.Empty;
    }

    private sealed class StubCatalogPath(string? root) : ISkillsCatalogPath
    {
        public string Root => root ?? throw new InvalidOperationException("Catalog not ready");
    }

    private sealed class VerbatimBodyResolver : ISkillBodyResolver
    {
        public string ResolveBody(RoleSkillDefinition skill, SkillRole role) => skill.Rules;
    }
}
