// 2026-08-25-39ab: the ONE place a fetch response becomes a typed object.
//
// Every *Api module used to compose its own base URL and assert its own body
// into its own interface with a bare `as`. A payload the dashboard did not
// recognise therefore failed in thirteen different ways, and a shape change was
// thirteen edits and a hunt. The base URL, the status check and the JSON read
// live here now.
//
// This does NOT make the types true — nothing here validates a body against its
// interface. What it does is give that check somewhere to go once the REST
// contract is generated from the server: one function, one edit.

import { currentAccessToken } from "@/lib/auth/session";

/** The API origin every request is composed against. Empty means same-origin. */
export const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export function apiUrl(path: string): string {
  return `${API_BASE}${path}`;
}

/**
 * A response the client asked for and could not use — either the server refused
 * it, or the body was not something this client could read. The path is part of
 * the message because "HTTP 500" alone never said which surface went down.
 */
export class ApiResponseError extends Error {
  readonly path: string;
  readonly status: number;

  constructor(path: string, status: number, detail: string) {
    super(`${path}: ${detail}`);
    this.name = "ApiResponseError";
    this.path = path;
    this.status = status;
  }
}

/** One fetch, composed against the API origin. No status check — the caller
 *  that branches on 404/409 needs the response, not an exception. */
export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  return fetch(apiUrl(path), await authorized(init));
}

// 2026-08-25-2de1: this being the ONE outgoing point is what makes the bearer one
// line rather than fifteen. No token held returns the caller's init untouched, so
// an installation with no authority sends the request it has always sent, header
// for header.
async function authorized(init?: RequestInit): Promise<RequestInit | undefined> {
  const token = await currentAccessToken();
  if (!token) return init;
  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${token}`);
  return { ...init, headers };
}

/**
 * Turn a response into the object the caller expects, or fail with a message
 * that names the surface and what arrived instead. A proxy's HTML error page,
 * an empty body, a truncated stream — all land here, once.
 */
export async function readJson<T>(res: Response, path = "the API"): Promise<T> {
  if (!res.ok) throw new ApiResponseError(path, res.status, `HTTP ${res.status}`);
  try {
    return (await res.json()) as T;
  } catch (cause) {
    throw new ApiResponseError(path, res.status, unreadable(res, cause));
  }
}

/** GET a path and read its body — what most read clients do and nothing more. */
export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  return readJson<T>(await apiFetch(path, { signal }), path);
}

/** Send a body and read the answer — the write-side twin of getJson. */
export async function sendJson<T>(
  method: string,
  path: string,
  body: unknown,
  signal?: AbortSignal,
): Promise<T> {
  const res = await apiFetch(path, {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal,
  });
  return readJson<T>(res, path);
}

function unreadable(res: Response, cause: unknown): string {
  const type = res.headers?.get?.("content-type") ?? null;
  const reason = cause instanceof Error ? cause.message : String(cause);
  return `the server answered HTTP ${res.status} with a body this client cannot read as JSON`
    + `${type ? ` (content-type ${type})` : ""} — ${reason}`;
}
