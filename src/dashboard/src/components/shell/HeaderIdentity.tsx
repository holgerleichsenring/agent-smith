"use client";

import { useState } from "react";
import Link from "next/link";
import { useAccessToken } from "@/hooks/useAccessToken";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { signIn, signOut } from "@/lib/auth/session";

// 2026-08-27-1ed6: who is signed in, in the corner every other tool puts it in — moved
// out of the rail, where the operator called it a needle in a haystack.
//
// It keeps the two properties 2026-08-25-4530 gave it: with no authority configured it
// renders NOTHING (a sign-out for a session that cannot exist is furniture for a door
// that is not there), and the hooks mount only inside the inner component, so an
// installation that cannot sign anybody in never reads an identity or subscribes to a
// token it will not get.

export function HeaderIdentity() {
  const { auth } = useRuntimeSettings();
  if (!auth.authority) return null;
  return <Account />;
}

function Account() {
  const token = useAccessToken();
  const { identity } = useCallerIdentity(token !== null);
  const [open, setOpen] = useState(false);

  if (token === null) {
    return (
      <button type="button" onClick={() => void signIn()} data-testid="header-sign-in" className="tb-btn">
        Sign in
      </button>
    );
  }

  const name = identity?.subject ?? "signed in";
  return (
    <div className="tb-account" data-testid="header-identity">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        data-testid="header-account"
        aria-haspopup="menu"
        aria-expanded={open}
        className="tb-who"
      >
        <span className="tb-avatar" aria-hidden="true">
          {name.slice(0, 1).toUpperCase()}
        </span>
        <span data-testid="header-identity-name">{name}</span>
      </button>
      {open && <AccountMenu onLeave={() => setOpen(false)} />}
    </div>
  );
}

// What the account offers: the page that says what the session was resolved from — the
// first question after "who am I" is "and what may I do" — and the way out.
function AccountMenu({ onLeave }: { onLeave: () => void }) {
  return (
    <div className="tb-menu" role="menu" data-testid="header-account-menu">
      <Link href="/identity" role="menuitem" data-testid="header-identity-link" onClick={onLeave}>
        Your identity
      </Link>
      <button
        type="button"
        role="menuitem"
        data-testid="header-sign-out"
        onClick={() => {
          onLeave();
          void signOut();
        }}
      >
        Sign out
      </button>
    </div>
  );
}
