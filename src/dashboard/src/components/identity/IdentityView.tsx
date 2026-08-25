"use client";

import { useCallerIdentity } from "@/hooks/useCallerIdentity";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { FailedSurface } from "@/components/shell/FailedSurface";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { PageHead } from "@/components/system/PageHead";
import { ClaimFacts } from "./ClaimFacts";

// 2026-08-25-4530: the surface p0503d built its endpoint for. The case it exists
// for is a caller with NO roles — the first login of an installation that has
// just configured an authority, where nothing is mapped yet and the operator
// cannot write a mapping until they can read what their directory actually sent.
//
// There is no screen here for granting anybody a role. The directory decides who
// holds one; this page shows what arrived and what the installation made of it.

export function IdentityView() {
  const { auth } = useRuntimeSettings();
  // With no authority configured nothing signs in, so the read is not attempted:
  // a refused request is a poor way to learn that there is nobody to describe.
  const { identity, refusal, failure, loading } = useCallerIdentity(Boolean(auth.authority));

  return (
    <div className="mock-shell mock-runs" data-testid="identity-view">
      <main className="main">
        <PageHead
          title="Your identity"
          sub="What your token carried, and what this installation made of it."
        />
        {!auth.authority ? (
          <Unconfigured />
        ) : refusal ? (
          <RefusalSurface refusal={refusal} surface="your identity" />
        ) : failure ? (
          <FailedSurface surface="identity" error={failure} />
        ) : identity ? (
          <ClaimFacts identity={identity} />
        ) : (
          <p className="text-sm text-[var(--color-ink-mid)]">
            {loading ? "Reading your identity…" : "Nothing has been read yet."}
          </p>
        )}
      </main>
    </div>
  );
}

// No authority configured is not a fault and not a refusal: this dashboard signs
// nobody in, so there is no token to describe. Whether the server accepts an
// anonymous caller is the server's half, and the banner above says so.
function Unconfigured() {
  return (
    <p data-testid="identity-unconfigured" className="text-sm text-[var(--color-ink-mid)]">
      This dashboard has no authority configured, so nothing signs in and every call it makes
      is anonymous.
    </p>
  );
}