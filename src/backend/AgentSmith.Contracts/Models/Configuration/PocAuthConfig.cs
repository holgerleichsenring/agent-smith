namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-23-c81eb: how every PoC is put behind auth, from <c>pocs.targets[].auth</c> — the annotations the
/// target's ingress controller reads to call its auth proxy before it forwards a request (forward auth in annotation
/// mode; ingress-nginx's auth-url and auth-signin are one example). Set on each PoC's Ingress as they are.</summary>
public sealed class PocAuthConfig
{
    public Dictionary<string, string> IngressAnnotations { get; set; } = [];
}
