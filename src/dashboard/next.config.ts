import type { NextConfig } from "next";

// p0169f-followup: browser hits localhost:3000; SignalR hub lives on the
// backend (localhost:8081 in dev, server:8081 in compose). The rewrite
// proxies /hub/* through the Next.js server so the browser stays
// same-origin (no CORS, no separate WS endpoint to remember). Target is
// configurable via AGENTSMITH_BACKEND_URL; defaults match local dev.
// Next.js rewrites forward WebSocket upgrades transparently since v12.
const backendUrl = process.env.AGENTSMITH_BACKEND_URL ?? "http://localhost:8081";

const nextConfig: NextConfig = {
  output: "standalone",
  reactStrictMode: true,
  async rewrites() {
    return [
      { source: "/hub/:path*", destination: `${backendUrl}/hub/:path*` },
      { source: "/api/:path*", destination: `${backendUrl}/api/:path*` },
    ];
  },
};

export default nextConfig;
