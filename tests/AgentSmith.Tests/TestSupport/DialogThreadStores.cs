using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.Tests.TestSupport;

/// <summary>A dialog thread store over one database, for tests that keep turns; its model states no window, so the
/// dialog window decides whether a thread applies.</summary>
internal static class DialogThreadStores
{
    internal static SpecDialogThreadStore Over(IUnitOfWork db, params string[] secrets) =>
        new(new SpecDialogThreadRepository(db), Codec(secrets), new SilentChatModel.SilentChatClients(), TimeProvider.System);

    internal static DialogThreadCodec Codec(params string[] secrets) =>
        new(new SecretMasker(new LoadedSecretValues(new AgentSmithConfig
        {
            Secrets = secrets.Select((value, i) => (Name: $"secret-{i}", value))
                .ToDictionary(s => s.Name, s => s.value),
        })));
}
