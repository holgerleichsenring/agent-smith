"use client";

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
  const missing = missingHalf(requirements, auth.authority);
  if (!missing) return null;

  return (
    <aside
      role="alert"
      data-testid="auth-misconfiguration-banner"
      data-half={missing.half}
      className="border-b border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900"
    >
      <p className="font-medium">Sign-in is configured on one side only.</p>
      <p className="mt-1">{missing.reason}</p>
    </aside>
  );
}

type MissingHalf = { half: "dashboard" | "server" | "both"; reason: string };

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
): MissingHalf | null {
  if (!requirements) return null;
  const server = normalize(requirements.authority);
  const dashboard = normalize(configured);

  if (server === null && dashboard === null) return null;
  if (dashboard === null) return { half: "dashboard", reason: dashboardHalf(requirements) };
  if (server === null) return { half: "server", reason: serverHalf(dashboard) };
  if (server === dashboard) return null;
  return {
    half: "both",
    reason:
      `This dashboard signs in against ${dashboard}, and the server validates tokens from `
      + `${server}. A token minted by one is refused by the other; one of the two is wrong.`,
  };
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
