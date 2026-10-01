using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-01-283dd: the one point through which a tool's image reaches the model. A tool
/// deposits while it runs; the tool loop places what was deposited right after the tool
/// result — once, in the loop's history — on providers that deliver it, and tells the model
/// an image exists and was not shown everywhere else. Keyed on what is deposited, never on
/// a tool name. At most two images per tool call.
/// </summary>
public interface IToolImageDeposit
{
    /// <summary>Hands the running tool call's loop one image; a refusal says why.</summary>
    ToolImageDepositResult Deposit(ToolImage image);
}
