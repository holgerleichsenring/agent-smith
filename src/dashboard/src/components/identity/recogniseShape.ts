import type { AuthRequirements } from "@/lib/authRequirementsApi";

// 2026-09-14-c72e: the closed set of token shapes this dashboard recognises, and the ways
// out of each.
//
// It lives in the BROWSER and not in the anonymous requirements body on purpose. The server
// validates one authority so that a realm which is not Entra is a first-class case; naming a
// vendor's shape inside a route every installation serves would put one directory's
// vocabulary where every other one has to read past it. The payload carries the two value
// pairs — facts — and the naming happens where the person is.
//
// Every recognition is a pure function of what the server expected and what the token
// carried. A shape nobody here recognises is not a failure: the comparison above it already
// shows the difference, and the sentence is the shortcut, never the diagnosis.

export interface Recognition {
  /** What this shape IS, in one sentence. */
  readonly shape: string;
  /** The ways out, most direct first. Each stands alone — they are alternatives, not steps. */
  readonly waysOut: readonly string[];
}

export function recogniseShape(
  requirements: AuthRequirements,
  signInClientId: string,
): Recognition | null {
  const expected = requirements.audience;
  const presented = requirements.presentedAudience;
  const version = requirements.presentedTokenVersion;

  // The token names the dashboard itself. Either an id_token travelled where an access token
  // belongs, or the API scope is missing and the authority minted one for the client.
  if (presented !== null && signInClientId !== "" && presented === signInClientId) {
    return {
      shape:
        "This token was issued for the sign-in client rather than for this API, so it was never "
        + "addressed to the server that just refused it.",
      waysOut: [
        "Add this API's scope to the dashboard's configured scopes — without it the authority "
        + "mints a token for the client and the server refuses every call.",
      ],
    };
  }

  // One directory, one sign-in, two access-token versions: the API's own registration decides
  // which. Recognised by the App ID URI form of the audience the server expects, plus the
  // version claim that says which half of the pair arrived.
  if (isAppIdUriOf(presented, expected) && version === "1.0") {
    return {
      shape:
        "This is a version 1 access token: its audience is the API's identifier URI and its "
        + "issuer is the directory's v1 endpoint, while this server is configured for the "
        + "version 2 pair. The resource decides the version, never the endpoint the sign-in "
        + "used — in Microsoft Entra that is requestedAccessTokenVersion on the API's own app "
        + "registration, which mints version 1 while it is unset.",
      waysOut: [
        `Set requestedAccessTokenVersion to 2 on the API registration, after which the audience `
        + `arrives as ${expected}.`,
        `Or configure this server for the version 1 pair: the audience ${presented} and the `
        + "authority without its /v2.0 suffix, whose discovery document names the v1 issuer.",
      ],
    };
  }

  // The mirror: a v2 token against a server configured for the v1 pair.
  if (isAppIdUriOf(expected, presented) && version === "2.0") {
    return {
      shape:
        "This is a version 2 access token, whose audience is the API's bare client id, while "
        + "this server is configured for the version 1 pair.",
      waysOut: [
        `Configure this server with the audience ${presented} and the authority carrying its `
        + "/v2.0 suffix.",
        "Or set the API registration's accepted access-token version back to 1, which mints "
        + "the identifier-URI form again.",
      ],
    };
  }

  return null;
}

// "api://{id}" is the identifier-URI form of the bare id. Compared as whole strings so a
// suffix — a scope name appended to the URI, which is a configuration error of its own —
// does not read as the same resource.
function isAppIdUriOf(uri: string | null, bare: string | null): boolean {
  return uri !== null && bare !== null && bare !== "" && uri === `api://${bare}`;
}
