"use client";

import Link from "next/link";
import { useAccessToken } from "@/hooks/useAccessToken";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { signIn, signOut } from "@/lib/auth/session";

// 2026-08-25-4530: who is signed in, in the one place that is on every route.
// With no authority configured it renders NOTHING — a sign-out for a session
// that cannot exist and a name nobody has are furniture for a door that is not
// there, and today that is every installation.

export function RailIdentity() {
  const { auth } = useRuntimeSettings();
  if (!auth.authority) return null;
  return <Session />;
}

// Split so the hooks below mount only for an installation that HAS an authority:
// the identity read and the token subscription are pointless where nothing can
// sign in, and a refused request is a poor way to learn that.
function Session() {
  const token = useAccessToken();
  const { identity } = useCallerIdentity(token !== null);

  if (token === null) {
    return (
      <div className="mt-3 border-t border-[var(--line)] px-3 py-3" data-testid="rail-identity">
        <button
          type="button"
          onClick={() => void signIn()}
          data-testid="rail-sign-in"
          className="text-xs font-medium underline"
        >
          Sign in
        </button>
      </div>
    );
  }

  return (
    <div className="mt-3 border-t border-[var(--line)] px-3 py-3" data-testid="rail-identity">
      {/* The name links to the page that says what it was resolved from — the
          first question after "who am I" is "and what may I do". */}
      <Link href="/identity" data-testid="rail-identity-name" className="block text-xs">
        {identity?.subject ?? "signed in"}
      </Link>
      <button
        type="button"
        onClick={() => void signOut()}
        data-testid="rail-sign-out"
        className="mt-1 text-xs font-medium underline"
      >
        Sign out
      </button>
    </div>
  );
}
