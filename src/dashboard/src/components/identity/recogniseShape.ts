import type { AuthRequirements } from "@/lib/authRequirementsApi";
import type { T } from "@/lib/i18n/translate";

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
  t: T,
): Recognition | null {
  const expected = requirements.audience;
  const presented = requirements.presentedAudience;
  const version = requirements.presentedTokenVersion;

  // The token names the dashboard itself. Either an id_token travelled where an access token
  // belongs, or the API scope is missing and the authority minted one for the client.
  if (presented !== null && signInClientId !== "" && presented === signInClientId) {
    return {
      shape: t("identity.shape.signInClient"),
      waysOut: [t("identity.shape.signInClientWay")],
    };
  }

  // One directory, one sign-in, two access-token versions: the API's own registration decides
  // which. Recognised by the App ID URI form of the audience the server expects, plus the
  // version claim that says which half of the pair arrived.
  if (isAppIdUriOf(presented, expected) && version === "1.0") {
    return {
      shape: t("identity.shape.v1"),
      waysOut: [
        t("identity.shape.v1WayVersion", { expected: expected ?? "" }),
        t("identity.shape.v1WayServer", { presented: presented ?? "" }),
      ],
    };
  }

  // The mirror: a v2 token against a server configured for the v1 pair.
  if (isAppIdUriOf(expected, presented) && version === "2.0") {
    return {
      shape: t("identity.shape.v2"),
      waysOut: [
        t("identity.shape.v2WayServer", { presented: presented ?? "" }),
        t("identity.shape.v2WayVersion"),
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
