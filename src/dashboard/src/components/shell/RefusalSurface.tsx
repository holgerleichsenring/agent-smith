"use client";

import { signIn } from "@/lib/auth/session";
import type { ApiRefusal } from "@/lib/apiResponse";

// 2026-08-25-4530: what a refused surface looks like. The server answers a
// refusal in exactly two flavours and each has exactly ONE action that resolves
// it, so this offers that one and nothing else.
//
// A missing permission is offered NO sign-in. Signing in again returns the same
// token carrying the same roles, so the button would be a loop with a promise in
// it — the permission comes from the directory, and the name below is what an
// operator takes there.
//
// The control the caller was refused stays where it is, everywhere else. A
// button that vanishes teaches nobody what they are missing; this panel does.

interface Props {
  refusal: ApiRefusal;
  /** The surface that was refused, in the words an operator would use for it. */
  surface?: string;
}

export function RefusalSurface({ refusal, surface }: Props) {
  const what = surface ?? "this";
  return (
    <div
      role="alert"
      data-testid="refusal-surface"
      data-refusal={refusal.kind}
      className="rounded-xl border border-slate-200 bg-slate-50 p-5 text-left"
    >
      {refusal.kind === "sign-in" ? (
        <SignedOut what={what} />
      ) : (
        <MissingPermission missing={refusal.missingPermissions} what={what} />
      )}
      <p className="mt-2 font-mono text-xs text-slate-500" data-testid="refusal-path">
        {refusal.path}
      </p>
    </div>
  );
}

function SignedOut({ what }: { what: string }) {
  return (
    <>
      <p className="text-sm font-semibold text-slate-900">You are signed out.</p>
      <p className="mt-1 text-xs text-slate-700">
        This installation asks for a sign-in before it shows {what}.
      </p>
      <button
        type="button"
        onClick={() => void signIn()}
        data-testid="refusal-sign-in"
        className="mt-3 rounded border border-slate-300 bg-white px-2.5 py-1 text-xs font-medium text-slate-900 hover:bg-slate-100"
      >
        Sign in
      </button>
    </>
  );
}

function MissingPermission({ missing, what }: { missing: readonly string[]; what: string }) {
  return (
    <>
      <p className="text-sm font-semibold text-slate-900">
        You are signed in, and not allowed to see {what}.
      </p>
      {missing.length > 0 ? (
        <>
          <p className="mt-1 text-xs text-slate-700">
            The server named {missing.length === 1 ? "the permission" : "the permissions"} it
            asked for and you do not hold:
          </p>
          <ul className="mt-2 space-y-1" data-testid="refusal-missing-permissions">
            {missing.map((permission) => (
              <li key={permission} className="font-mono text-xs text-slate-900">
                {permission}
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p className="mt-1 text-xs text-slate-700" data-testid="refusal-unnamed">
          The server named no permission — its own logs name the route it refused.
        </p>
      )}
      <p className="mt-2 text-xs text-slate-700">
        A permission comes from a role your directory grants. Your roles are on the identity
        page, and changing them is your directory&apos;s business rather than this
        dashboard&apos;s.
      </p>
    </>
  );
}
