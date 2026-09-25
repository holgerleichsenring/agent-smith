using System.Text.RegularExpressions;
using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-25-2fa7: a role the configuration offers and no code requests is a setting that
/// silently does nothing. TaskType.CodeMapGeneration was declared, defaulted, mapped by the
/// registry, rendered and patchable in the Config Studio and measured by the context-window
/// preflight — and its only appearance in src/ was the registry's own switch. An operator who set
/// it changed nothing and was told nothing.
/// <para>
/// The rule looks for a role being PASSED to the chat-client factory, not for the word anywhere.
/// A text scan would go green the moment a role joins ToolBearingTasks — present in a file that
/// requests nothing — which is the shape of a rule that retires itself the day it lands.
/// </para>
/// </summary>
public sealed class TaskRoleReachTests
{
    private static readonly Regex PassedToTheFactory = new(
        @"(?:Create|GetMaxOutputTokens|GetModel|GetContextWindowTokens)\s*\([^;]{0,200}?TaskType\.(\w+)",
        RegexOptions.Compiled);

    [Fact]
    public void TaskTypes_EveryDeclaredRole_IsPassedToTheFactorySomewhere()
    {
        var requested = Directory
            .EnumerateFiles(ArchitectureSources.BackendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => PassedToTheFactory.Matches(File.ReadAllText(f)))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Enum.GetNames<TaskType>().Where(name => !requested.Contains(name))
            .Should().BeEmpty(
                "a role the configuration accepts, the studio renders and preflight measures, "
                + "while nothing asks the factory for it, is a knob wired to nothing");
    }
}
