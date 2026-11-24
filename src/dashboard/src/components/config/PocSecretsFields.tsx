"use client";

import { useEffect, useState } from "react";
import { ApiResponseError } from "@/lib/apiResponse";
import { fetchSealedSecretsCert, type PocSecretsSetting, type SealedSecretsCert } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { DrawerSection, SelectField, TextField } from "./formFields";

// 2026-11-23-933dg: how a PoC target seals its PoCs' secret variables and pull secret — the provider (Sealed Secrets
// only) and the controller's public certificate. The certificate is public, so it is a plain field: pasted from
// `kubeseal --fetch-cert`, or fetched from a URL. Whatever the field holds is read by the server (no network) and its
// fingerprint, expiry and age are shown; nothing is saved until the form is.

export const POC_SECRETS_PROVIDER = "sealed_secrets";

/** A target read without a secrets block is given these. */
export function newPocSecrets(): PocSecretsSetting {
  return { provider: POC_SECRETS_PROVIDER, sealedSecretsCert: null, sealedSecretsCertUrl: null };
}

export const CERT_URL_PLACEHOLDER = "https://sealed-secrets.example.internal/v1/cert.pem";

const INSPECT_DELAY_MS = 400;
const DAY_MS = 24 * 60 * 60 * 1000;

type Read = { status: "none" } | { status: "read"; cert: SealedSecretsCert } | { status: "failed"; message: string };

export function PocSecretsFields({ secrets, testId, onChange }: {
  secrets: PocSecretsSetting | null | undefined; testId: string; onChange: (secrets: PocSecretsSetting) => void;
}) {
  const t = useT();
  const value = secrets ?? newPocSecrets();
  const set = (patch: Partial<PocSecretsSetting>) => onChange({ ...value, ...patch });
  const pem = value.sealedSecretsCert ?? "";
  const [read, setRead] = useState<Read>({ status: "none" });
  const [fetching, setFetching] = useState(false);

  // Whatever the field holds is parsed by the server, a moment after the last keystroke.
  useEffect(() => {
    if (!pem.trim()) {
      setRead({ status: "none" });
      return;
    }
    const abort = new AbortController();
    const timer = setTimeout(() => {
      fetchSealedSecretsCert({ pem }, abort.signal)
        .then((cert) => setRead({ status: "read", cert }))
        .catch((err: unknown) => {
          if (!abort.signal.aborted) setRead({ status: "failed", message: refusalText(t, err) });
        });
    }, INSPECT_DELAY_MS);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [pem, t]);

  const fetchFromUrl = () => {
    const url = (value.sealedSecretsCertUrl ?? "").trim();
    if (!url) return;
    setFetching(true);
    fetchSealedSecretsCert({ url })
      .then((cert) => {
        set({ sealedSecretsCert: cert.pem, sealedSecretsCertUrl: url });
        setRead({ status: "read", cert });
      })
      .catch((err: unknown) => setRead({ status: "failed", message: refusalText(t, err) }))
      .finally(() => setFetching(false));
  };

  return (
    <DrawerSection title={t("config.pocs.secrets.title")} testId={testId}>
      <span className="help">{t("config.pocs.secrets.help")}</span>
      <SelectField label={t("config.pocs.secrets.provider")} value={value.provider || POC_SECRETS_PROVIDER}
        options={[{ value: POC_SECRETS_PROVIDER, label: "Sealed Secrets" }]} testId={`${testId}-provider`}
        onChange={() => set({ provider: POC_SECRETS_PROVIDER })} />
      <div className="field">
        <label>
          {t("config.pocs.secrets.cert")}
          <span className="help">{t("config.pocs.secrets.cert.help")}</span>
        </label>
        <textarea className="mono" rows={6} spellCheck={false} data-testid={`${testId}-cert`} value={pem}
          placeholder="-----BEGIN CERTIFICATE-----" onChange={(e) => set({ sealedSecretsCert: e.target.value || null })} />
      </div>
      <TextField label={t("config.pocs.secrets.certUrl")} value={value.sealedSecretsCertUrl ?? ""} mono testId={`${testId}-cert-url`}
        placeholder={CERT_URL_PLACEHOLDER} help={t("config.pocs.secrets.certUrl.help")}
        onChange={(v) => set({ sealedSecretsCertUrl: v.trim() || null })} />
      <div className="field">
        <button type="button" className="btn" data-testid={`${testId}-fetch`}
          disabled={fetching || !(value.sealedSecretsCertUrl ?? "").trim()} onClick={fetchFromUrl}>
          {fetching ? t("config.pocs.secrets.fetching") : t("config.pocs.secrets.fetch")}
        </button>
      </div>
      {read.status === "failed" && <span className="help" role="alert" data-testid={`${testId}-fetch-error`}>{read.message}</span>}
      {read.status === "read" && <CertFacts cert={read.cert} testId={testId} />}
    </DrawerSection>
  );
}

function CertFacts({ cert, testId }: { cert: SealedSecretsCert; testId: string }) {
  const t = useT();
  const now = Date.now();
  const notAfter = new Date(cert.notAfter);
  const days = Math.max(0, Math.floor((now - new Date(cert.notBefore).getTime()) / DAY_MS));
  const expired = notAfter.getTime() < now;
  return (
    <div className="field">
      <span className="help mono" data-testid={`${testId}-fingerprint`}>{t("config.pocs.secrets.fingerprint", { fingerprint: cert.fingerprint })}</span>
      <span className="help" data-testid={`${testId}-not-after`} data-expired={expired ? "true" : "false"}>
        {expired
          ? t("config.pocs.secrets.expired", { date: notAfter.toISOString().slice(0, 10) })
          : t("config.pocs.secrets.notAfter", { date: notAfter.toISOString().slice(0, 10) })}
      </span>
      <span className="help" data-testid={`${testId}-age`}>{t("config.pocs.secrets.age", { count: days })}</span>
    </div>
  );
}

function refusalText(t: T, err: unknown): string {
  if (err instanceof ApiResponseError && err.reason) return err.reason;
  return t("config.pocs.secrets.failed", { message: err instanceof Error ? err.message : String(err) });
}
