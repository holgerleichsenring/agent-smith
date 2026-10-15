namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-14-dbcej: the bounds of a proposed list of tickets. Code constants until the operator
/// ratifies them; twenty realistic ticket texts measure about 5,100 to 6,800 tokens, inside the
/// design reply's 8192-token output cap.
/// </summary>
public static class TicketGraphLimits
{
    public const int MinTickets = 2;
    public const int MaxTickets = 20;
    public const int MaxTitle = 120;
    public const int MaxRequirement = 1200;
    public const int MaxDoneItems = 8;
    public const int MaxDoneItem = 240;
}
