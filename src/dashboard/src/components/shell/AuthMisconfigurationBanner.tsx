"use client";

import { useAccessToken } from "@/hooks/useAccessToken";
import { useAuthRequirements } from "@/hooks/useAuthRequirements";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import type { AuthRequirements } from "@/lib/authRequirementsApi";

/**
 * 2026-08-25-4530: the two halves of sign-in are configured on two machines, and
 * neither can diagnose the other. An installation whose server enforces and whose
 * dashboard has no authority answers 401 to every call and renders nothing — no
 * error, no clue, and the settings that would explain it are not in the same
 * place. This is the only place that holds both, so this is where they are
 * compared.
 *
 * A BANNER, never a blocking page. p0391a already put "what is wrong with this
 * installation" above every route and p0503e measured a short-circuit to be
 * either a lie or unreadable — the routes underneath keep rendering whatever they
 * can still reach.
 *
 * Both halves agreeing says nothing. Both halves unconfigured says nothing, which
 * is every installation today.
 */
export function AuthMisconfigurationBanner() {
  const requirements = useAuthRequirements();
  const { auth } = useRuntimeSettings();
  const token = useAccessToken();
  const missing = missingHalf(requirements, auth.authority, token !== null);
  if (!missing) return null;

  return (
    <aside
      role="alert"
      data-testid="auth-misconfiguration-banner"
      data-half={missing.half}
      className="border-b border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900"
    >
      <p className="font-medium">{missing.heading}</p>
      <p className="mt-1">{missing.reason}</p>
    </aside>
  );
}

type MissingHalf = {
  half: "dashboard" | "server" | "both";
  /** 2026-09-21-291b: the heading belongs to the CASE. "configured on one side
   *  only" stood above all three, and the case that renders most often is the one
   *  where both sides are configured and disagree — where it is simply untrue. */
  heading: string;
  reason: string;
};

const ONE_SIDE = "Sign-in is configured on one side only.";
const TWO_AUTHORITIES = "The two halves of sign-in name different authorities.";

// Trailing slashes are how the same issuer is written two ways — an authority
// copied out of a discovery document carries one and the one typed by hand does
// not, and a banner that called those two different authorities would be noise.
function normalize(authority: string | null): string | null {
  const trimmed = authority?.trim().replace(/\/+$/, "") ?? "";
  return trimmed === "" ? null : trimmed;
}

function missingHalf(
  requirements: AuthRequirements | null,
  configured: string,
  holdsToken: boolean,
): MissingHalf | null {
  if (!requirements) return null;
  const server = normalize(requirements.authority);
  const dashboard = normalize(configured);

  if (server === null && dashboard === null) return null;
  if (dashboard === null) {
    return { half: "dashboard", heading: ONE_SIDE, reason: dashboardHalf(requirements) };
  }
  if (server === null) {
    return { half: "server", heading: ONE_SIDE, reason: serverHalf(dashboard) };
  }
  if (server === dashboard) return null;
  if (!twoAuthoritiesAreAProblem(requirements, holdsToken, server, dashboard)) return null;
  return {
    half: "both",
    heading: TWO_AUTHORITIES,
    reason:
      `This dashboard signs in against ${dashboard}, and the server validates tokens from `
      + `${server}. A token minted by one is refused by the other; one of the two is wrong.`,
  };
}

// 2026-09-14-c72e: two authority strings differing is not the question — whether a token
// minted by one survives the other is, and that is answered by tokens rather than by
// strings. A directory's v2 sign-in issuing an id_token beside a v1 resource validating an
// access token is ONE directory, correctly configured, writing two different strings.
//
// A token this tab HOLDS that was not refused is proof the two halves agree. The holding is
// the half the browser contributes: the server's tokenRefusal is null both for a token it
// accepted and for a request that carried none, and before the first sign-in that null
// proves nothing — going quiet there would silence the banner exactly while an operator is
// working out why they cannot sign in.
//
// And only the two checks that read an authority may accuse one. An expired token says
// nothing about either, and a server that cannot reach its own authority refuses tokens for
// a reason that is not the operator's configuration at all.
function twoAuthoritiesAreAProblem(
  requirements: AuthRequirements,
  holdsToken: boolean,
  server: string,
  dashboard: string,
): boolean {
  const refusal = requirements.tokenRefusal;
  if (refusal === null) return !holdsToken && !oneDirectoryOnTwoEndpoints(server, dashboard);
  return refusal === "audience" || refusal === "issuer";
}

// 2026-09-21-291b: one directory publishes two endpoints and they write two
// strings — the sign-in endpoint carries a version suffix the resource endpoint
// does not. recogniseShape names that very pair as a way OUT of a fault: "the
// audience api://{id} and the authority without its /v2.0 suffix, whose discovery
// document names the v1 issuer". Accusing it is accusing the fixed state.
//
// This belongs HERE and not in normalize(), for two mechanical reasons. missingHalf
// returns on equal strings BEFORE it consults the refusal, so normalising the suffix
// away would silence the banner for a token actually refused on its issuer; and the
// sentence prints the normalised strings, so an operator would be shown an authority
// they never configured. The question the suffix answers is only ever "is a
// difference we have no token to test a reason to accuse", which is this branch.
//
// LITERAL, never pattern-shaped. A realm at .../realms/v2 is a different realm from
// .../realms, and a version-shaped pattern would read a real two-realm mistake as one
// directory. And this is not a claim that the two endpoints interoperate — whether a
// v2-minted token is accepted by a v1-configured resource is decided by the API
// registration's requested token version, which no browser can see. It is a refusal to
// call the pair broken on evidence that cannot settle it: let such a token be minted
// and refused, and tokenRefusal names the issuer or the audience and the banner speaks.
const VERSION_ENDPOINT_SUFFIX = "/v2.0";

function oneDirectoryOnTwoEndpoints(server: string, dashboard: string): boolean {
  return withoutVersionEndpoint(server) === withoutVersionEndpoint(dashboard);
}

function withoutVersionEndpoint(authority: string): string {
  return authority.endsWith(VERSION_ENDPOINT_SUFFIX)
    ? authority.slice(0, -VERSION_ENDPOINT_SUFFIX.length)
    : authority;
}

function dashboardHalf(requirements: AuthRequirements): string {
  const authority = requirements.authority ?? "an authority";
  return requirements.enforced
    ? `The server enforces sign-in against ${authority} and this dashboard has no authority `
      + "configured, so every call it makes is refused. Set the authority in the dashboard's "
      + "runtime settings."
    : `The server validates tokens from ${authority} and this dashboard has no authority `
      + "configured, so nobody can sign in. Nothing is refused yet — enforcement is off.";
}

function serverHalf(dashboard: string): string {
  return (
    `This dashboard signs in against ${dashboard} and the server has no authority configured, `
    + "so it validates no token and reads every caller as anonymous. Set the authority in the "
    + "server's auth configuration."
  );
}
