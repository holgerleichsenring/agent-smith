"use client";

import { useEffect, useState } from "react";
import {
  fetchInstallationIdentity,
  type InstallationIdentity,
  type SandboxAgentRelease,
} from "@/lib/installationApi";
import { BUILD_REVISION, RELEASE_VERSION } from "@/lib/buildIdentity";
import { PageHead } from "./PageHead";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";

// 2026-08-27-729e: "what am I running", asked calmly. It is NOT in the banner stack above
// every route — those name what is WRONG, and a permanent panel among them becomes
// furniture, which is the reasoning the rail identity already applied. Whether the halves
// disagree stays where it was: the build-mismatch banner and the pinned-agent finding.
//
// Server, sandbox agent and database come from the server's own read-out; the dashboard's
// release is this bundle's own constant, labelled as its own, because it cannot reach the
// server — the findings request carries the revision only.

const NOT_STATED = "not stated by this build";

export function InstallationIdentityView() {
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
        title="Installation"
        sub="Which build of agent-smith this installation runs — server, dashboard and sandbox agent — and the database behind them. Server, dashboard and agent are published together; a release they do not share is a redeploy waiting to happen."
      />

      {refusal ? (
        <RefusalSurface refusal={refusal} surface="the installation report" />
      ) : error ? (
        <div className="stateline err" data-testid="installation-error">
          Failed to read the installation report: {error.message}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="installation-loading">
          Reading the installation report…
        </div>
      ) : (
        <>
          <Components data={data} />
          <Database data={data} />
        </>
      )}
    </div>
  );
}

function Components({ data }: { data: InstallationIdentity }) {
  return (
    <section data-testid="installation-components">
      <div className="section-head">
        <h2>Components</h2>
        <span className="cnt">{2 + data.agents.length}</span>
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
        <Row
          testId="installation-server"
          name="Server"
          value={data.serverRelease ?? NOT_STATED}
          sub={data.serverRevision ? `revision ${short(data.serverRevision)}` : "no revision stamped"}
        />
        {/* Read locally: this bundle's own stamp, never something the server told us. */}
        <Row
          testId="installation-dashboard"
          name="Dashboard"
          value={RELEASE_VERSION || NOT_STATED}
          sub={BUILD_REVISION ? `revision ${short(BUILD_REVISION)}` : "no revision stamped"}
        />
        {data.agents.map((agent) => (
          <Row
            key={agent.project}
            testId={`installation-agent-${agent.project}`}
            name={`Sandbox agent · ${agent.project}`}
            value={agent.version ?? NOT_STATED}
            sub={agentSub(agent)}
          />
        ))}
        {data.agents.length === 0 && (
          <div className="stateline" data-testid="installation-no-projects">
            No project is configured, so no sandbox agent is spawned.
          </div>
        )}
      </div>
    </section>
  );
}

function Database({ data }: { data: InstallationIdentity }) {
  const { provider, reachable, pendingMigrations, error } = data.database;
  return (
    <section data-testid="installation-database">
      <div className="section-head">
        <h2>Database</h2>
      </div>
      <div style={{ height: 14 }} />
      <div className="list">
        <Row testId="installation-provider" name="Provider" value={provider} sub="from the configuration" />
        <Row
          testId="installation-migrations"
          name="Migrations"
          value={migrationValue(reachable, pendingMigrations)}
          sub={error ?? "the schema this server records runs into"}
        />
      </div>
    </section>
  );
}

function migrationValue(reachable: boolean, pending: number): string {
  if (!reachable) return "unknown — the database did not answer";
  if (pending === 0) return "up to date";
  return `${pending} pending — run 'agentsmith database migrate'`;
}

function agentSub(agent: SandboxAgentRelease): string {
  if (agent.source === "pinned") return "pinned — this project names its own tag";
  if (agent.source === "derived") return "derived — the tag follows this server's release";
  return "underivable — this build carries no release to derive a tag from";
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
