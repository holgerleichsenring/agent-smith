"use client";

import { useEffect, useState } from "react";
import {
  fetchInstallationIdentity,
  type CatalogBinding,
  type InstallationIdentity,
  type SandboxAgentRelease,
} from "@/lib/installationApi";
import { BUILD_REVISION, RELEASE_VERSION } from "@/lib/buildIdentity";
import { PageHead } from "./PageHead";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// 2026-08-27-729e: "what am I running", asked calmly. It is NOT in the banner stack above
// every route — those name what is WRONG, and a permanent panel among them becomes
// furniture, which is the reasoning the rail identity already applied. Whether the halves
// disagree stays where it was: the build-mismatch banner and the pinned-agent finding.
//
// Server, sandbox agent and database come from the server's own read-out; the dashboard's
// release is this bundle's own constant, labelled as its own, because it cannot reach the
// server — the findings request carries the revision only.

export function InstallationIdentityView() {
  const t = useT();
  const [data, setData] = useState<InstallationIdentity | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchInstallationIdentity(controller.signal)
      .then(setData)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e);
      });
    return () => controller.abort();
  }, []);

  const refusal = refusalIn(error);

  return (
    <div data-testid="installation-view">
      <PageHead
        title={t("system.installation.title")}
        sub={t("system.installation.sub")}
      />

      {refusal ? (
        <RefusalSurface refusal={refusal} surface={t("system.installation.surface")} />
      ) : error ? (
        <div className="stateline err" data-testid="installation-error">
          {t("system.installation.failed", { message: error.message })}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="installation-loading">
          {t("system.installation.loading")}
        </div>
      ) : (
        <>
          <Components data={data} />
          <Catalog data={data} />
          <Database data={data} />
        </>
      )}
    </div>
  );
}

function Components({ data }: { data: InstallationIdentity }) {
  const t = useT();
  const notStated = t("system.installation.notStated");
  const noRevision = t("system.installation.noRevision");
  return (
    <section data-testid="installation-components">
      <div className="section-head">
        <h2>{t("system.installation.components")}</h2>
        <span className="cnt">{2 + data.agents.length}</span>
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
        <Row
          testId="installation-server"
          name={t("system.installation.server")}
          value={data.serverRelease ?? notStated}
          sub={data.serverRevision ? t("system.installation.revision", { revision: short(data.serverRevision) }) : noRevision}
        />
        {/* Read locally: this bundle's own stamp, never something the server told us. */}
        <Row
          testId="installation-dashboard"
          name={t("system.installation.dashboard")}
          value={RELEASE_VERSION || notStated}
          sub={BUILD_REVISION ? t("system.installation.revision", { revision: short(BUILD_REVISION) }) : noRevision}
        />
        {data.agents.map((agent) => (
          <Row
            key={agent.project}
            testId={`installation-agent-${agent.project}`}
            name={t("system.installation.agent", { project: agent.project })}
            value={agent.version ?? notStated}
            sub={agentSub(agent, t)}
          />
        ))}
        {data.agents.length === 0 && (
          <div className="stateline" data-testid="installation-no-projects">
            {t("system.installation.noProjects")}
          </div>
        )}
      </div>
    </section>
  );
}

// 2026-09-20-4981: two facts, never one. The binding is what this server RESOLVED; the
// floor is what its binary was built against, and on three of the four source modes they
// differ by design — so the floor is labelled as the floor and never stands in for the
// answer. A catalog that would not resolve still says what was configured, because that
// is the moment this page is being read.
function Catalog({ data }: { data: InstallationIdentity }) {
  const t = useT();
  const { catalog } = data;
  return (
    <section data-testid="installation-catalog">
      <div className="section-head">
        <h2>{t("system.installation.catalog")}</h2>
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
        <Row
          testId="installation-catalog-binding"
          name={t("system.installation.boundTo")}
          value={bindingValue(catalog, t)}
          sub={catalog.resolved ? t("system.installation.resolved") : t("system.installation.unresolved")}
        />
        <Row
          testId="installation-catalog-floor"
          name={t("system.installation.floor")}
          value={data.embeddedCatalogVersion ?? t("system.installation.notStated")}
          sub={t("system.installation.floorSub")}
        />
      </div>
    </section>
  );
}

function bindingValue(catalog: CatalogBinding, t: T): string {
  const base = `${catalog.source} ${catalog.version ?? t("system.installation.noVersion")}`;
  return catalog.overlay ? t("system.installation.overlay", { base, overlay: catalog.overlay }) : base;
}

function Database({ data }: { data: InstallationIdentity }) {
  const t = useT();
  const { provider, reachable, pendingMigrations, error } = data.database;
  return (
    <section data-testid="installation-database">
      <div className="section-head">
        <h2>{t("system.installation.database")}</h2>
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
        <Row
          testId="installation-provider"
          name={t("system.installation.provider")}
          value={provider}
          sub={t("system.installation.fromConfig")}
        />
        <Row
          testId="installation-migrations"
          name={t("system.installation.migrations")}
          value={migrationValue(reachable, pendingMigrations, t)}
          sub={error ?? t("system.installation.schemaSub")}
        />
      </div>
    </section>
  );
}

function migrationValue(reachable: boolean, pending: number, t: T): string {
  if (!reachable) return t("system.installation.dbUnknown");
  if (pending === 0) return t("system.installation.upToDate");
  return t("system.installation.pending", { count: pending });
}

function agentSub(agent: SandboxAgentRelease, t: T): string {
  if (agent.source === "pinned") return t("system.installation.pinned");
  if (agent.source === "derived") return t("system.installation.derived");
  return t("system.installation.underivable");
}

function short(revision: string): string {
  return revision.length > 12 ? revision.slice(0, 12) : revision;
}

function Row({
  testId,
  name,
  value,
  sub,
}: {
  testId: string;
  name: string;
  value: string;
  sub: string;
}) {
  return (
    <div className="ecard" data-testid={testId}>
      <div className="ec-top">
        <div style={{ minWidth: 0 }}>
          <div className="ec-name sans">{name}</div>
          <div className="ec-sub">{sub}</div>
        </div>
        <div className="ec-right">
          <span className="mono num">{value}</span>
        </div>
      </div>
    </div>
  );
}
