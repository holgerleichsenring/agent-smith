import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "../apiResponse";
import { fetchSealedSecretsCert } from "../configApi";

// 2026-11-23-933dg: one route reads a Sealed Secrets certificate — fetched from a URL, or parsed from a pasted PEM.
// Its refusals (400, or 502 when the URL did not answer) are plain sentences, carried whole as the reason.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function plain(status: number, text: string): Response {
  return {
    ok: false, status, text: async () => text, json: async () => { throw new Error("not json"); },
    headers: { get: () => "text/plain" },
  } as unknown as Response;
}

function answer(body: unknown): Response {
  return {
    ok: true, status: 200, text: async () => JSON.stringify(body), json: async () => body,
    headers: { get: () => "application/json" },
  } as unknown as Response;
}

const CERT = {
  pem: "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----", fingerprint: "ab:cd",
  notBefore: "2026-11-01T00:00:00Z", notAfter: "2036-11-01T00:00:00Z",
};

afterEach(() => vi.unstubAllGlobals());

describe("fetchSealedSecretsCert", () => {
  it("fetchSealedSecretsCert_PostsTheUrlOrThePem_AndReadsTheCertificate", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(CERT));
    vi.stubGlobal("fetch", fetchMock);

    await expect(fetchSealedSecretsCert({ url: "https://certs.example.internal/v1/cert.pem" })).resolves.toEqual(CERT);
    await fetchSealedSecretsCert({ pem: CERT.pem });

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/config\/pocs\/sealed-secrets-cert\/fetch$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ url: "https://certs.example.internal/v1/cert.pem" });
    expect(JSON.parse(fetchMock.mock.calls[1][1].body as string)).toEqual({ pem: CERT.pem });
  });

  it("fetchSealedSecretsCert_400And502_CarryTheirSentences", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(plain(400, "The certificate is not PEM with an RSA key."))
      .mockResolvedValueOnce(plain(502, "https://certs.example.internal answered 503.")));

    const bad = await fetchSealedSecretsCert({ pem: "x" }).catch((err: unknown) => err) as ApiResponseError;
    const down = await fetchSealedSecretsCert({ url: "https://certs.example.internal" }).catch((err: unknown) => err) as ApiResponseError;

    expect(bad).toBeInstanceOf(ApiResponseError);
    expect([bad.status, bad.reason]).toEqual([400, "The certificate is not PEM with an RSA key."]);
    expect([down.status, down.reason]).toEqual([502, "https://certs.example.internal answered 503."]);
  });
});
