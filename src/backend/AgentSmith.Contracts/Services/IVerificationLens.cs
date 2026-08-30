using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-30-0ea8: which entries of the ingested standard apply to one station of a
/// request — every one of them at the level floor, since 2026-08-30-03e1 retired the
/// selection bound together with the hand-out that needed it.
/// <para>
/// The run is a parameter rather than an afterthought: consulting the catalogue records
/// the version that answered, so an id cited later can be looked up against the release
/// that issued it.
/// </para>
/// </summary>
public interface IVerificationLens
{
    VerificationSelection For(PipelineContext run, VerificationStation station);
}
