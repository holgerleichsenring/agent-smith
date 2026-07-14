using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// p0324: skills-catalog encodes the two historic silent killers — pin drift (stale
/// or missing skills.version) and the 200-char master-description limit whose
/// overflow used to drop the master and kill the run later with 'Prompt resource
/// not found'.
/// </summary>
public sealed class SkillsCatalogCheckTests
{
    [Fact]
    public async Task SkillsCatalogCheck_MasterDescriptionOverLimit_FailsWithFixHint()
    {
        var loader = new Mock<ISkillLoader>();
        loader.Setup(l => l.LoadRoleDefinitions(It.IsAny<string>()))
            .Throws(new InvalidOperationException(
                "skills/_masters/coding-agent-master/SKILL.md: description must be at most 200 chars; got 262"));
        var check = CreateCheck(loader.Object);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("description must be at most 200 chars");
        result.FixHint.Should().Contain("200").And.Contain("Prompt resource not found");
    }

    [Fact]
    public async Task RunAsync_ResolverFails_FailsWithPinHint()
    {
        var resolver = new Mock<ISkillsCatalogResolver>();
        resolver.Setup(r => r.EnsureResolvedAsync(It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("release tag 'v9.9.9' not found"));
        var check = CreateCheck(LoaderWith(AllRequiredMasters()), resolver.Object);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("v9.9.9");
        result.FixHint.Should().Contain("skills.version");
    }

    [Fact]
    public async Task RunAsync_RequiredMasterMissing_FailsNamingIt()
    {
        var masters = AllRequiredMasters();
        masters.RemoveAll(d => d.Name == "coding-agent-master");
        var check = CreateCheck(LoaderWith(masters));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("coding-agent-master");
        result.FixHint.Should().Contain("skills.version");
    }

    [Fact]
    public async Task RunAsync_AllRequiredMastersPresent_Passes()
    {
        var check = CreateCheck(LoaderWith(AllRequiredMasters()));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("all required masters present");
    }

    private static SkillsCatalogCheck CreateCheck(
        ISkillLoader loader, ISkillsCatalogResolver? resolver = null)
    {
        if (resolver is null)
        {
            var resolverMock = new Mock<ISkillsCatalogResolver>();
            resolverMock
                .Setup(r => r.EnsureResolvedAsync(It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogResolution(
                    "/catalog", "v3.16.1", SkillsSourceMode.Default, "https://skills.test", true));
            resolver = resolverMock.Object;
        }
        return new SkillsCatalogCheck(
            FakePreflightConfigSource.Of(new AgentSmithConfig()), resolver, loader);
    }

    private static ISkillLoader LoaderWith(List<RoleSkillDefinition> definitions)
    {
        var loader = new Mock<ISkillLoader>();
        loader.Setup(l => l.LoadRoleDefinitions(It.IsAny<string>())).Returns(definitions);
        return loader.Object;
    }

    private static List<RoleSkillDefinition> AllRequiredMasters() =>
        SkillCatalogPromptCatalog.RequiredMasterSkills
            .Select(name => new RoleSkillDefinition { Name = name, Role = "master" })
            .ToList();
}
