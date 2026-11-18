namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-18-78efa: what the person whose dialog message this scope dispatches holds beyond the dialog — secrets.write,
/// which applying the deletion of a stored secret value needs. The dashboard's dispatch sets it from the caller's
/// principal on the scope it opens for the message; a scope nobody set it on holds nothing, so a chat channel or any
/// other path is refused rather than trusted.
/// </summary>
public sealed class DialogDeciderGrants
{
    public bool MayWriteSecrets { get; private set; }

    public void Hold(bool mayWriteSecrets) => MayWriteSecrets = mayWriteSecrets;
}
