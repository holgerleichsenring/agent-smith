"use client";

import { useEffect, useState } from "react";
import { getAccessTokenStore } from "@/lib/auth/AccessTokenStore";
import { startAuthSession } from "@/lib/auth/session";

// 2026-08-25-4530: the React reading of the token store. 2026-08-25-2de1 left
// this unbuilt on purpose — the store is owned by plain modules (apiFetch and the
// hub client cannot call a hook), and what a React surface needs of it depends on
// what that surface renders. subscribe() is the primitive; this is the one hook
// over it, so no component owns the token and every one of them agrees.

/** The token this tab holds. Null means signed out, which is a state. */
export function useAccessToken(): string | null {
  const [token, setToken] = useState<string | null>(null);

  useEffect(() => {
    const store = getAccessTokenStore();
    const stop = store.subscribe(setToken);
    // The boot's silent sign-in is what puts the FIRST token in the store, and a
    // surface that mounted before it settled would have missed the announcement.
    void startAuthSession().then(() => setToken(store.read()));
    return stop;
  }, []);

  return token;
}
