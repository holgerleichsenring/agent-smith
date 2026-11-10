using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.References;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What a conversation holds beside its turns, added to its view: uploads, mocks, ratified versions, its work
/// picture and the ticket text it was grounded on. Carved out of <see cref="SpecDialogViewReader"/> by
/// 2026-11-10-ccd9, which needed the reader's room for the awaiting proposal and the draft under review.
/// </summary>
public sealed class SpecDialogHoldings(
    SpecDialogTicketTextRepository ticketText, ConversationUploads uploads,
    AgentSmith.Contracts.Sandbox.IConversationMockReader? mocks = null)
{
    public async Task<ConversationUploadsView> UploadsAsync(ConversationState state, CancellationToken ct) =>
        await uploads.ReadAsync(state.JobId, ct, state.Project);

    /// <summary>2026-09-27-481bc: one indexed row, and only for a conversation that HAS one.</summary>
    public async Task<SpecDialogTicketView?> TicketAsync(ConversationState state, CancellationToken ct) =>
        await ticketText.GetAsync(state.JobId, ct) is { } held
            ? new SpecDialogTicketView(held.TicketId, held.Title, held.Text, held.ReadAt, held.Truncated)
            : null;

    // What the conversation holds beside its turns: uploads, mocks, ratified versions and 2026-11-05-862ea its work picture.
    public async Task<SpecDialogSessionView> WithAsync(
        SpecDialogSessionView session, ConversationState state, ConversationUploadsView held, CancellationToken ct) =>
        session with
        {
            References = held.References,
            UploadBytes = held.Bytes,
            UploadCapBytes = ReferenceUploadLimits.MaxConversationBytes,
            SeesImages = held.Sight?.Images ?? true,
            SeesUploadedImages = held.Sight?.UploadedImages ?? true,
            ImageCount = held.ImageCount,
            Mocks = await MocksAsync(state.JobId, ct), // 2026-10-13-8762r: no bytes
            RatifiedMocks = RatifiedMocks.Of(state.Transcript) is { Count: > 0 } ratified ? ratified : null,
            WorkPicture = WorkPictureView.Of(state.WorkPicture),
        };

    private async Task<IReadOnlyList<SpecDialogMockView>?> MocksAsync(string sessionId, CancellationToken ct) =>
        mocks is null ? null : (await mocks.ListAsync(sessionId, ct)) is { Count: > 0 } held
            ? [.. held.Select(m => new SpecDialogMockView(m.SpecId, m.Version, m.Name, m.At, m.HasShot))] : null;
}
