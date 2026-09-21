"use client";

import { useEffect, type ReactNode } from "react";
import { useAccessToken } from "@/hooks/useAccessToken";
import { useAuthRequirements } from "@/hooks/useAuthRequirements";
import { useSessionEnd } from "@/hooks/useSessionEnd";
import { useSignInSettled } from "@/hooks/useSignInSettled";
import { isSilentReturnFrame } from "@/lib/auth/silentReturnFrame";
import { signIn } from "@/lib/auth/session";
import {
  forgetTheAuthorityWasReached,
  rememberTheAuthorityWasReached,
  theAuthorityWasReached,
} from "@/lib/auth/signInSentinel";
import type { SessionEndReason } from "@/lib/auth/AccessTokenStore";

/**
 * 2026-09-21-291b: an installation that refuses every call to an anonymous caller
 * used to answer them with itself — an empty dashboard, a grey "offline" dot and a
 * banner accusing its own configuration. Nothing on that screen was true, and the
 * one action that resolves it sat in a corner.
 *
 * The redirect AuthSession's header declined is now answerable, because the server
 * says whether it enforces. What that header's objection still buys is the shape of
 * this component: it acts on TWO answers (the server's and this tab's) and renders
 * its children until both are in — it sits above every route, and holding the tree
 * back for a silent attempt's ten-second timeout would put that wait in front of
 * every installation, including the ones that enforce nothing.
 */
export function SignInGate({ children }: { children: ReactNode }) {
  const requirements = useAuthRequirements();
  const token = useAccessToken();
  const ended = useSessionEnd();
  const settled = useSignInSettled();

  // A token landed, so whatever this tab did to get one is spent. Forgetting it
  // here rather than at the redirect's return is what makes a later sign-out or
  // expiry start from a clean tab.
  useEffect(() => {
    if (token !== null) forgetTheAuthorityWasReached();
  }, [token]);

  const barred = settled && token === null && requirements?.enforced === true;
  // A deliberate end is not a missing beginning. Walking somebody back through a
  // live directory session would undo their sign-out; yanking them to an authority
  // mid-work would be the answer to an expiry they did not ask about.
  const theirs = ended !== null || theAuthorityWasReached();
  const sendThem = barred && !theirs && !isSilentReturnFrame();

  useEffect(() => {
    if (!sendThem) return;
    // Written before the redirect leaves: a redirect that fails AT the authority
    // never returns to write anything, which is what a loop is made of.
    rememberTheAuthorityWasReached();
    void signIn();
  }, [sendThem]);

  if (!barred || isSilentReturnFrame()) return <>{children}</>;
  return <SignedOut ended={ended} leaving={sendThem} />;
}

const WHY_IT_ENDED: Record<SessionEndReason, string> = {
  expired: "Your session expired.",
  "renewal-refused": "Your session ended.",
};

function SignedOut({ ended, leaving }: { ended: SessionEndReason | null; leaving: boolean }) {
  return (
    <div className="p-10" data-testid="sign-in-gate" data-leaving={leaving ? "yes" : "no"}>
      <div className="mx-auto max-w-md rounded-xl border border-slate-200 bg-slate-50 p-6 text-left">
        <p className="text-sm font-semibold text-slate-900">
          {ended !== null ? WHY_IT_ENDED[ended] : "You are signed out."}
        </p>
        <p className="mt-1 text-xs text-slate-700">
          {leaving
            ? "Taking you to your organisation's sign-in page."
            : "This installation asks for a sign-in before it shows anything."}
        </p>
        {!leaving && (
          <button
            type="button"
            onClick={() => void signIn()}
            data-testid="sign-in-gate-button"
            className="mt-3 rounded border border-slate-300 bg-white px-2.5 py-1 text-xs font-medium text-slate-900 hover:bg-slate-100"
          >
            Sign in
          </button>
        )}
      </div>
    </div>
  );
}
