"use client";

import type { CallerIdentity } from "@/lib/identityApi";
import { useT } from "@/hooks/useT";

// 2026-08-25-4530: the values, in the order an operator reads them — who the
// token says you are, which claim the server looked in, what arrived there, and
// what it resolved to. The claim NAMES are shown beside their values because a
// claim that arrived empty and a claim nobody looked in produce the same blank,
// and only one of the two is fixed by changing the directory.

export function ClaimFacts({ identity }: { identity: CallerIdentity }) {
  const t = useT();
  const projects = Object.entries(identity.projectPermissions ?? {}).filter(([, granted]) => granted.length > 0);
  return (
    <div className="space-y-6" data-testid="identity-facts">
      <section>
        <Heading>{t("identity.signedInAs")}</Heading>
        <Fact label={t("identity.subject")} value={identity.subject ?? t("identity.noSubject")} />
        <Fact label={t("identity.issuer")} value={identity.issuer ?? t("identity.noIssuer")} />
      </section>

      <section>
        <Heading>{t("identity.whatArrived")}</Heading>
        <Values
          testId="identity-role-claim"
          label={t("identity.roleClaim", { claim: identity.roleClaim })}
          values={identity.roleClaimValues}
        />
        <Values
          testId="identity-group-claim"
          label={t("identity.groupClaim", { claim: identity.groupClaim })}
          values={identity.groupClaimValues}
        />
      </section>

      <section>
        <Heading>{t("identity.resolvedTo")}</Heading>
        <Values testId="identity-roles" label={t("identity.roles")} values={identity.roles} />
        <Values testId="identity-permissions" label={t("identity.permissions")} values={identity.permissions} />
        {projects.length > 0 && <PerProject projects={projects} />}
      </section>

      {identity.roles.length === 0 && projects.length === 0 && <NoRolesYet />}

      {identity.findings.length > 0 && (
        <section data-testid="identity-findings">
          <Heading>{t("identity.noticed")}</Heading>
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

// 2026-10-15-4a4de: what a project binding gave, per project — held there only, beside the global list above.
function PerProject({ projects }: { projects: [string, string[]][] }) {
  const t = useT();
  return (
    <div className="mt-2 text-sm" data-testid="identity-project-permissions">
      <span className="text-[var(--color-ink-mid)]">{t("identity.perProject")}</span>
      <ul className="mt-1 space-y-0.5">
        {projects.map(([project, granted]) => (
          <li key={project} className="text-xs" data-testid={`identity-project-${project}`}>
            <span className="font-semibold">{project}</span>{" "}
            <span className="font-mono">{granted.join(", ")}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

// The case the endpoint was built for, said out loud. Without this the page is a
// list of empty lists and reads as a fault rather than as an unwritten mapping.
function NoRolesYet() {
  const t = useT();
  return (
    <p data-testid="identity-no-roles" className="text-sm text-[var(--color-ink-mid)]">
      {t("identity.noRoles")}
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
  const t = useT();
  return (
    <div className="mt-2 text-sm" data-testid={testId}>
      <span className="text-[var(--color-ink-mid)]">{label}</span>
      {values.length === 0 ? (
        <span className="ml-2 italic text-[var(--color-ink-mid)]">{t("identity.nothingArrived")}</span>
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
