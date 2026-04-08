namespace AgentSmith.Contracts.Dialogue;

public interface IDialogueTrail
{
    Task RecordAsync(DialogQuestion question, DialogAnswer answer);
    IReadOnlyList<DialogTrailEntry> GetAll();
}
