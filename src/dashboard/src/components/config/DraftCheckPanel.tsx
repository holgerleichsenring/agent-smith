"use client";

import { useEffect, useRef, useState } from "react";
import {
  checkConnectionDraft,
  checkPocRegistryDraft,
  checkTrackerDraft,
  type DraftCheckReport,
  type PocRegistrySetting,
  type StudioConnection,
  type StudioTracker,
} from "@/lib/configApi";

// 2026-10-02-5f89b: the Test action on the connection and tracker forms. It checks the UNSAVED
// draft against its host and renders the server's steps as they came — each with its mark, the
// last one being the first failure. The panel holds no rules: the order, the sentences and the
// stopping point are the server's. Needs config.write and diagnostics.probe; a refusal shows as
// the request's error.

// 2026-11-23-933de: a PoC target's registry block too, with its own label and test id prefix.
type Props =
  | { kind: "connections"; draft: StudioConnection }
  | { kind: "trackers"; draft: StudioTracker }
  | { kind: "poc-registry"; draft: PocRegistrySetting; label: string; help: string; testId: string };

export function DraftCheckPanel(props: Props) {
  const [report, setReport] = useState<DraftCheckReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [running, setRunning] = useState(false);
  const inFlight = useRef<AbortController | null>(null);

  // A draft that changed since the last Test is a different draft; its old answer would lie.
  const key = JSON.stringify(props.draft);
  useEffect(() => {
    setReport(null);
    setError(null);
  }, [key]);
  useEffect(() => () => inFlight.current?.abort(), []);

  const run = () => {
    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;
    setRunning(true);
    setReport(null);
    setError(null);
    const ask =
      props.kind === "connections"
        ? checkConnectionDraft(props.draft, controller.signal)
        : props.kind === "trackers"
          ? checkTrackerDraft(props.draft, controller.signal)
          : checkPocRegistryDraft(props.draft, controller.signal);
    ask
      .then(setReport)
      .catch((err: Error) => {
        if (err.name !== "AbortError") setError(err.message);
      })
      .finally(() => setRunning(false));
  };

  const poc = props.kind === "poc-registry" ? props : null;
  const id = poc?.testId ?? "draft-check";
  return (
    <div className="field" data-testid={id}>
      <label>
        {poc ? poc.label : "test"}{" "}
        <span className="help">{poc ? poc.help : "checks this unsaved draft against its host; nothing is saved"}</span>
      </label>
      <div className="picks">
        <button type="button" className="pick" disabled={running} onClick={run} data-testid={`${id}-run`}>
          {running ? "Testing…" : poc ? poc.label : "Test"}
        </button>
      </div>
      {error && (
        <div className="dsh-body" style={{ color: "var(--bad)" }} data-testid={`${id}-error`}>
          {error}
        </div>
      )}
      {report && (
        <ol className="flex flex-col gap-1" data-testid={`${id}-steps`}>
          {report.steps.map((step) => (
            <li
              key={step.key}
              className="dsh-body"
              data-testid={`${id}-step-${step.key}`}
              data-ok={step.ok}
              style={{ color: step.ok ? undefined : "var(--bad)" }}
            >
              <span aria-label={step.ok ? "passed" : "failed"}>{step.ok ? "✓" : "✗"}</span>{" "}
              <strong>{step.label}</strong> — {step.detail}
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
