using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.References;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-08-e8b9g: a conversation's uploads read over one test store, as the dialog view reads them.</summary>
internal static class TestUploads
{
    public static ConversationUploads Over(AgentSmithDbContext context) =>
        new(new ReferenceFileRepository(context), new ReferenceSetRepository(context),
            new ReferenceUsageRepository(context), new ApprovedSeriesRepository(context),
            new ReferenceNoteRepository(context));

    public static ConversationUploadAdmission Admission(AgentSmithDbContext context) =>
        new(new ReferenceUsageRepository(context));
}
