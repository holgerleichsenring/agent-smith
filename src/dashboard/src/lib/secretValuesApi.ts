import { getJson, sendJson } from "@/lib/apiResponse";

// 2026-11-17-f3a8d: the admin area's secret values. Write-only: a value goes to the server in the request body and
// nothing the server answers carries one back — the reads say where a value comes from, who set it and when.

export interface StoredSecretState {
  name: string;
  setBy: string;
  setAt: string;
  keyId: string;
  keyCurrent: boolean;
  readable: boolean;
}

export interface SecretValueState {
  name: string;
  /** 2026-11-18-9ae8: the environment variable the secret reads (its reference), not always its name. */
  variable: string;
  fromEnvironment: boolean;
  stored: StoredSecretState | null;
}

export interface SecretValuesView {
  masterKeyPresent: boolean;
  masterKeyProblem: string | null;
  secrets: SecretValueState[];
  orphans: StoredSecretState[];
  /** 2026-11-17-f3a8e: where the master key came from — the deployment decides it, so it is shown, never edited. */
  masterKeySource: string | null;
}

const PATH = "/api/config/secret-values";

export function fetchSecretValues(signal?: AbortSignal): Promise<SecretValuesView> {
  return getJson<SecretValuesView>(PATH, signal);
}

export function setSecretValue(name: string, value: string): Promise<{ line: string }> {
  return sendJson<{ line: string }>("PUT", `${PATH}/${encodeURIComponent(name)}`, { value });
}

export function deleteSecretValue(name: string): Promise<{ line: string }> {
  return sendJson<{ line: string }>("DELETE", `${PATH}/${encodeURIComponent(name)}`, {});
}

export function rewrapSecretValues(): Promise<{ line: string }> {
  return sendJson<{ line: string }>("POST", `${PATH}/rewrap`, {});
}
