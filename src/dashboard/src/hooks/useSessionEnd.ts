"use client";

import { useEffect, useState } from "react";
import { getAccessTokenStore, type SessionEndReason } from "@/lib/auth/AccessTokenStore";
import { startAuthSession } from "@/lib/auth/session";

// 2026-08-28-0f46: the token answers "is anybody signed in". It cannot answer
// "did a session end here", and every surface that reads only the token renders
// the same bare button whether the person has never signed in or has just been
// signed out by an expiry they did not see. This is a second reading of the same
// one store — a hook per question, rather than one hook returning two things.

/** Why the session this tab had ended, or null when none has. */
export function useSessionEnd(): SessionEndReason | null {
  const [ended, setEnded] = useState<SessionEndReason | null>(null);

  useEffect(() => {
    const store = getAccessTokenStore();
    const stop = store.subscribe((state) => setEnded(state.ended));
    // A restore that ended the session did so while this surface was mounting.
    void startAuthSession().then(() => setEnded(store.state().ended));
    return stop;
  }, []);

  return ended;
}
