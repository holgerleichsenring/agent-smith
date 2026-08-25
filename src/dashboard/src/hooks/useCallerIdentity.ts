"use client";

import { useEffect, useState } from "react";
import { refusalIn, type ApiRefusal } from "@/lib/apiResponse";
import { fetchIdentity, type CallerIdentity } from "@/lib/identityApi";
import { useAccessToken } from "./useAccessToken";

// 2026-08-25-4530: what the server made of this caller's token, refreshed when
// the token changes — a sign-in mid-session is a different caller, and a page
// still showing the previous answer would be showing somebody else's roles.

export interface CallerIdentityState {
  identity: CallerIdentity | null;
  /** The server refused the read. Rendered as a state, never as a fault. */
  refusal: ApiRefusal | null;
  /** Anything else that went wrong, which is a fault and reads as one. */
  failure: Error | null;
  loading: boolean;
}

const IDLE: CallerIdentityState = {
  identity: null,
  refusal: null,
  failure: null,
  loading: false,
};

/**
 * @param enabled false leaves the endpoint alone. The app rail asks only once a
 * token is held: spending a refused request on a person who is simply not signed
 * in would answer a question the rail never asked.
 */
export function useCallerIdentity(enabled = true): CallerIdentityState {
  const token = useAccessToken();
  const [state, setState] = useState<CallerIdentityState>(IDLE);

  useEffect(() => {
    if (!enabled) {
      setState(IDLE);
      return;
    }
    const controller = new AbortController();
    setState({ ...IDLE, loading: true });
    fetchIdentity(controller.signal)
      .then((identity) => setState({ ...IDLE, identity }))
      .catch((thrown: Error) => {
        if (thrown.name === "AbortError") return;
        const refusal = refusalIn(thrown);
        setState({ ...IDLE, refusal, failure: refusal ? null : thrown });
      });
    return () => controller.abort();
  }, [enabled, token]);

  return state;
}
