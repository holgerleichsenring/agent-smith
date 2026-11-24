using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Variables;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: the poc-variables step — over the real store — refuses another project's PoC, writes a
/// secret and a plain value, moves a name from plain to secret, removes its seed, and logs names only.</summary>
public sealed class WritePocVariablesHandlerTests : IDisposable
{
    private const string Value = "sk-live-value-933dg";
    private readonly SecretStoreRig _rig = new();
    private readonly RecordingLoggerProvider _logs = new();

    [Fact]
    public async Task Execute_APocOfAnotherProject_IsRefused_AndNothingIsWritten()
    {
        var poc = await _rig.PocAsync("shop", "poc-bob");

        var result = await RunAsync("poc-ann", Write(poc.Id, Secret("OPENAI_API_KEY", Value)));

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain($"PoC {poc.Id} is no PoC of project 'poc-ann'");
        (await _rig.Vault().StatesAsync(SecretScope.Poc(poc.Id), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_ASecretAndAPlainValue_AreWritten_SeedGone_NamesOnlyInTheLog()
    {
        var poc = await _rig.PocAsync("todo", "poc-ann");
        var context = Context("poc-ann", Write(poc.Id, Secret("OPENAI_API_KEY", Value), Plain("LOG_LEVEL", "debug")));

        var result = await Handler().ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        context.Pipeline.Has(ContextKeys.PocVariableWrite).Should().BeFalse();
        (await _rig.PocValues().ReadAsync(poc.Id, default)).Values["OPENAI_API_KEY"].Reveal().Should().Be(Value);
        (await _rig.Variables().ReadAsync(poc.Id, default))!.Plain.Single().Should().Match<PocPlainVariable>(p => p.Name == "LOG_LEVEL" && p.Value == "debug" && p.SetBy == "ann-sub");
        result.Message.Should().Contain("OPENAI_API_KEY (secret)").And.NotContain(Value);
        _logs.Entries.Should().NotContain(e => e.Message.Contains(Value)).And.Contain(e => e.Message.Contains("OPENAI_API_KEY"));
    }

    [Fact]
    public async Task Execute_APlainNameWrittenAsSecret_DeletesThePlainOne_AndBack()
    {
        var poc = await _rig.PocAsync("todo", "poc-ann");
        await RunAsync("poc-ann", Write(poc.Id, Plain("API_URL", "https://x")));

        await RunAsync("poc-ann", Write(poc.Id, Secret("API_URL", Value)));
        (await _rig.Variables().ReadAsync(poc.Id, default))!.Plain.Should().BeEmpty();
        (await _rig.Vault().StatesAsync(SecretScope.Poc(poc.Id), default)).Single().Name.Should().Be("API_URL");

        await RunAsync("poc-ann", Write(poc.Id, Plain("API_URL", "https://y")));
        (await _rig.Vault().StatesAsync(SecretScope.Poc(poc.Id), default)).Should().BeEmpty();
    }

    private Task<AgentSmith.Domain.Models.CommandResult> RunAsync(string project, PocVariableWrite write) =>
        Handler().ExecuteAsync(Context(project, write), default);

    private WritePocVariablesHandler Handler()
    {
        var vault = _rig.Vault();
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSecrets()).Returns([]);
        var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddProvider(_logs));
        return new WritePocVariablesHandler(_rig.Variables(), vault, new PocVariableRules(),
            new PocVariableWriter(new SecretWrite(vault, store.Object), vault, TimeProvider.System), new PocsConfigSource(),
            factory.CreateLogger<WritePocVariablesHandler>());
    }

    private static WritePocVariablesContext Context(string project, PocVariableWrite write)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PocVariableWrite, write);
        pipeline.Set(ContextKeys.RunId, "run-v1");
        return new WritePocVariablesContext(pipeline, new ResolvedProject { Name = project });
    }

    private static PocVariableWrite Write(long pocId, params PocVariableEntry[] entries) => new(pocId, entries, "ann-sub");

    private static PocVariableEntry Secret(string name, string value) => new(name, PocVariableKinds.Secret, new SecretInput(value));

    private static PocVariableEntry Plain(string name, string value) => new(name, PocVariableKinds.Plain, Plain: value);

    public void Dispose() => _rig.Dispose();
}
