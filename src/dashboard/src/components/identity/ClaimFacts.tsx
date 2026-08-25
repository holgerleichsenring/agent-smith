"use client";

import type { CallerIdentity } from "@/lib/identityApi";

// 2026-08-25-4530: the values, in the order an operator reads them — who the
// token says you are, which claim the server looked in, what arrived there, and
// what it resolved to. The claim NAMES are shown beside their values because a
// claim that arrived empty and a claim nobody looked in produce the same blank,
// and only one of the two is fixed by changing the directory.

export function ClaimFacts({ identity }: { identity: CallerIdentity }) {
  return (
    <div className="space-y-6" data-testid="identity-facts">
      <section>
        <Heading>Signed in as</Heading>
        <Fact label="Subject" value={identity.subject ?? "— the token named no subject"} />
        <Fact label="Issuer" value={identity.issuer ?? "— the token named no issuer"} />
      </section>

      <section>
        <Heading>What arrived</Heading>
        <Values
          testId="identity-role-claim"
          label={`Role claim · ${identity.roleClaim}`}
          values={identity.roleClaimValues}
        />
        <Values
          testId="identity-group-claim"
          label={`Group claim · ${identity.groupClaim}`}
          values={identity.groupClaimValues}
        />
      </section>

      <section>
        <Heading>What it resolved to</Heading>
        <Values testId="identity-roles" label="Roles" values={identity.roles} />
        <Values testId="identity-permissions" label="Permissions" values={identity.permissions} />
      </section>

      {identity.roles.length === 0 && <NoRolesYet />}

      {identity.findings.length > 0 && (
        <section data-testid="identity-findings">
          <Heading>What the server noticed</Heading>
          <ul className="mt-1 space-y-1 text-sm text-amber-900">
            {identity.findings.map((finding) => (
              <li key={finding}>{finding}</li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

// The case the endpoint was built for, said out loud. Without this the page is a
// list of empty lists and reads as a fault rather than as an unwritten mapping.
function NoRolesYet() {
  return (
    <p data-testid="identity-no-roles" className="text-sm text-[var(--color-ink-mid)]">
      Your token carried no role this installation maps. The values above are what a mapping is
      written from — it is server configuration, and your directory decides which of them you
      carry.
    </p>
  );
}

function Heading({ children }: { children: React.ReactNode }) {
  return <h2 className="text-xs font-semibold uppercase tracking-wide">{children}</h2>;
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <p className="mt-1 text-sm">
      <span className="text-[var(--color-ink-mid)]">{label}</span>{" "}
      <span className="font-mono">{value}</span>
    </p>
  );
}

function Values({
  label,
  values,
  testId,
}: {
  label: string;
  values: string[];
  testId: string;
}) {
  return (
    <div className="mt-2 text-sm" data-testid={testId}>
      <span className="text-[var(--color-ink-mid)]">{label}</span>
      {values.length === 0 ? (
        <span className="ml-2 italic text-[var(--color-ink-mid)]">nothing arrived</span>
      ) : (
        <ul className="mt-1 space-y-0.5">
          {values.map((value) => (
            <li key={value} className="font-mono text-xs">
              {value}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
