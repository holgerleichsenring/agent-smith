using AgentSmith.Application.Services.Pocs.Variables;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: the one rule of a variable write — each refusal names the value, the reason and the key.</summary>
public sealed class PocVariableRulesTests
{
    private readonly PocVariableRules _rules = new();
    private readonly PocVariablesConfig _limits = new();

    [Fact]
    public void Problems_ALowerCaseName_IsRefused() =>
        Problems(Plain("log_level", "debug")).Should().ContainSingle().Which.Should().Contain("'log_level' is no PoC variable name");

    [Fact]
    public void Problems_65Names_AreRefusedNamingMaxNames()
    {
        var existing = Enumerable.Range(0, 64).Select(i => $"V{i}");

        _rules.Problems([Plain("ONE_MORE", "x")], existing, _limits).Should().ContainSingle()
            .Which.Should().Contain("65 variables").And.Contain("pocs.variables.max_names allows 64").And.Contain("1 MiB");
        _rules.Problems([Plain("ONE_MORE", "x"), new PocVariableEntry("V0", PocVariableKinds.Delete)], existing, _limits).Should().BeEmpty();
    }

    [Fact]
    public void Problems_A5KbValue_IsRefusedNamingMaxValueKb() =>
        Problems(Plain("BIG", new string('x', 5 * 1024))).Should().ContainSingle()
            .Which.Should().Contain("BIG is 5 KB").And.Contain("pocs.variables.max_value_kb allows 4 KB");

    [Fact]
    public void Problems_A201CharacterName_IsRefusedNamingTheColumnWidth() =>
        Problems(Plain(new string('A', 201), "x")).Should().ContainSingle()
            .Which.Should().Contain("201 characters").And.Contain("StoredSecrets.Name");

    [Fact]
    public void Problems_AFiveCharacterSecret_IsRefusedNamingTheMinimum_APlainOneIsNot()
    {
        Problems(new PocVariableEntry("API_KEY", PocVariableKinds.Secret, new SecretInput("12345"))).Should().ContainSingle()
            .Which.Should().Contain("at least 6 characters");
        Problems(Plain("LOG_LEVEL", "info")).Should().BeEmpty();
    }

    [Fact]
    public void Problems_TheVariablesBlock_RefusesZeroAndAnOversizedProduct()
    {
        new PocVariablesConfig { MaxNames = 0 }.Problems().Should().ContainSingle().Which.Should().StartWith("pocs.variables.max_names is 0");
        new PocVariablesConfig { MaxNames = 1000, MaxValueKb = 4 }.Problems().Should().ContainSingle().Which.Should().Contain("1 MiB");
    }

    private IReadOnlyList<string> Problems(params PocVariableEntry[] entries) => _rules.Problems(entries, [], _limits);

    private static PocVariableEntry Plain(string name, string value) => new(name, PocVariableKinds.Plain, Plain: value);
}
