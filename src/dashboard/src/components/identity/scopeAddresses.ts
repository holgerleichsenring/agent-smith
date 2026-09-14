// 2026-09-14-d4e8: whether a configured API scope names the resource this server validates.
//
// It may only ever say "your scope names X, this server validates Y". It may NOT say "no
// scope names a resource": that is the shape of every realm without an api:// form, which
// this product supports as a first-class case — the repository's own example configuration
// pairs the audience 'agent-smith' with the scopes 'openid profile', and a rule firing there
// would accuse a correct installation. The security reference states the contract the other
// way round for the server ("no list, no prefix rule"), and a prefix rule applied to the
// scope side is that rule wearing a hat.
//
// So BOTH sides are recognised before anything is said, and everything else is silence.

/** The two resources, as each side names them. */
export interface ScopeMismatch {
  /** The resource the dashboard's API scope names. */
  readonly asked: string;
  /** The audience the server says it validates. */
  readonly validated: string;
}

const API_SCHEME = "api://";

/**
 * The mismatch worth saying out loud, or null. Null covers far more than "they agree":
 * an authority that partitions by audience, a realm with no API scopes, a server that
 * validates no audience, and any shape this rule does not recognise.
 */
export function scopeMismatch(
  effectiveScopes: string,
  dashboardAudience: string,
  serverAudience: string | null,
): ScopeMismatch | null {
  // An audience configured HERE means the resource is named by the request parameter —
  // the shape createAuthorityClient sends it for. The scopes then carry plain permission
  // names and this rule is reading the wrong half.
  if (dashboardAudience.trim() !== "") return null;

  const validated = normalize(serverAudience ?? "");
  if (validated === "") return null;

  const asked = apiResources(effectiveScopes);
  if (asked.length === 0) return null;
  if (asked.some((resource) => names(resource, validated))) return null;

  return { asked: asked[0], validated: (serverAudience ?? "").trim() };
}

/** The resource part of every scope that is recognisably an API scope: api://{id}/{name}. */
function apiResources(scopes: string): string[] {
  return scopes
    .split(/\s+/)
    .filter((scope) => scope.startsWith(API_SCHEME) && scope.lastIndexOf("/") > API_SCHEME.length)
    .map((scope) => scope.slice(0, scope.lastIndexOf("/")));
}

// The server's audience is the identifier URI for a version 1 access token and the bare id
// for a version 2 one, and BOTH are the same resource — the difference is a token version,
// which no comparison of configuration can see and this rule does not pretend to.
function names(resource: string, validated: string): boolean {
  const asked = normalize(resource);
  return asked === validated || asked === `${API_SCHEME}${validated}`;
}

// Normalised the way the misconfiguration banner normalises an authority: one copied out of
// a discovery document carries a trailing slash and one typed by hand does not, and a
// resource identifier written in two cases is one resource.
function normalize(value: string): string {
  return value.trim().replace(/\/+$/, "").toLowerCase();
}
