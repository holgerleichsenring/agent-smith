// 2026-10-01-283de: the browser sandbox's egress guard. Chromium is pointed at this proxy for
// EVERY request — page, subresource, websocket, service-worker fetch — with loopback no longer
// bypassed. The proxy resolves a host ONCE, refuses it when any address is not public, and
// connects to the address it checked, so a second lookup cannot rebind the name to an internal
// one. The one loopback target it lets through is the local origin serving the uploaded set.
// The ranges match PublicAddressRule on the server.
import dns from 'node:dns/promises';
import http from 'node:http';
import net from 'node:net';

const ranges = [
  ['0.0.0.0', 8, 'ipv4', 'unspecified'], ['10.0.0.0', 8, 'ipv4', 'private'],
  ['100.64.0.0', 10, 'ipv4', 'carrier-grade NAT'], ['127.0.0.0', 8, 'ipv4', 'loopback'],
  ['169.254.0.0', 16, 'ipv4', 'link-local'], ['172.16.0.0', 12, 'ipv4', 'private'],
  ['192.0.0.0', 24, 'ipv4', 'IETF protocol assignment'], ['192.168.0.0', 16, 'ipv4', 'private'],
  ['198.18.0.0', 15, 'ipv4', 'benchmarking'], ['224.0.0.0', 4, 'ipv4', 'multicast'],
  ['240.0.0.0', 4, 'ipv4', 'reserved or broadcast'],
  ['::', 96, 'ipv6', 'unspecified, loopback or IPv4-compatible'], ['64:ff9b::', 96, 'ipv6', 'NAT64'],
  ['64:ff9b:1::', 48, 'ipv6', 'NAT64'], ['fc00::', 7, 'ipv6', 'unique-local'],
  ['fe80::', 10, 'ipv6', 'link-local'], ['ff00::', 8, 'ipv6', 'multicast'],
];
const blocks = ranges.map(([base, prefix, family, name]) => {
  const list = new net.BlockList();
  list.addSubnet(base, prefix, family);
  return { list, family, label: `${name} (${base}/${prefix})` };
});

// An IPv4-mapped IPv6 address is judged as the IPv4 address it carries.
export function unwrapMapped(address) {
  const dotted = /^::ffff:(\d+\.\d+\.\d+\.\d+)$/i.exec(address);
  if (dotted) return dotted[1];
  const hex = /^::ffff:([0-9a-f]{1,4}):([0-9a-f]{1,4})$/i.exec(address);
  if (!hex) return address;
  const high = parseInt(hex[1], 16), low = parseInt(hex[2], 16);
  return [high >> 8, high & 255, low >> 8, low & 255].join('.');
}

export function refusalFor(rawAddress) {
  const address = unwrapMapped(rawAddress.replace(/^\[|\]$/g, '').split('%')[0]);
  const family = net.isIPv4(address) ? 'ipv4' : net.isIPv6(address) ? 'ipv6' : null;
  if (!family) return `${rawAddress} is not an IP address`;
  const block = blocks.find(b => b.family === family && b.list.check(address, family));
  return block ? `${address} is ${block.label}` : null;
}

export class EgressGuard {
  constructor() {
    this.refused = [];
    this.allowedLocal = new Set();
  }

  // The local origin serving the uploaded set — the only loopback target let through.
  // 2026-10-01-283di: a comparison serves its two sides from two origins, so it is a set.
  allowLocal(port) {
    this.allowedLocal.add(`127.0.0.1:${port}`);
  }

  async resolve(host, port) {
    const bare = host.replace(/^\[|\]$/g, '');
    if (this.allowedLocal.has(`${bare}:${port}`)) return { address: '127.0.0.1' };
    let addresses;
    if (net.isIP(bare)) addresses = [bare];
    else {
      try { addresses = (await dns.lookup(bare, { all: true, verbatim: true })).map(a => a.address); }
      catch { return { refusal: `${bare} does not resolve` }; }
    }
    if (addresses.length === 0) return { refusal: `${bare} resolves to no address` };
    for (const address of addresses) {
      const refusal = refusalFor(address);
      if (refusal) return { refusal: `${bare} → ${refusal}` };
    }
    return { address: addresses[0] };
  }

  note(target, reason) {
    this.refused.push({ url: target, reason });
  }

  async listen() {
    const server = http.createServer((req, res) => this.forward(req, res));
    server.on('connect', (req, client, head) => this.tunnel(req, client, head));
    server.on('upgrade', (req, client, head) => this.upgrade(req, client, head));
    await new Promise(done => server.listen(0, '127.0.0.1', done));
    this.server = server;
    return server.address().port;
  }

  async tunnel(req, client, head) {
    client.on('error', () => {});
    const { hostname, port } = new URL(`http://${req.url}`);
    const target = await this.resolve(hostname, Number(port) || 443);
    if (target.refusal) {
      this.note(`${req.url} (tunnel)`, target.refusal);
      client.end('HTTP/1.1 403 Forbidden\r\n\r\n');
      return;
    }
    const upstream = net.connect(Number(port) || 443, target.address, () => {
      client.write('HTTP/1.1 200 Connection Established\r\n\r\n');
      if (head?.length) upstream.write(head);
      upstream.pipe(client);
      client.pipe(upstream);
    });
    upstream.on('error', () => client.destroy());
  }

  async forward(req, res) {
    let url;
    try { url = new URL(req.url); } catch { res.writeHead(400).end(); return; }
    if (url.protocol !== 'http:') { res.writeHead(400).end(); return; }
    const port = Number(url.port) || 80;
    const target = await this.resolve(url.hostname, port);
    if (target.refusal) {
      this.note(url.href, target.refusal);
      res.writeHead(403, { 'content-type': 'text/plain' }).end('refused by the egress guard');
      return;
    }
    const headers = { ...req.headers };
    delete headers['proxy-connection'];
    delete headers['proxy-authorization'];
    const upstream = http.request({ host: target.address, port, method: req.method,
      path: url.pathname + url.search, headers, setHost: false }, response => {
      res.writeHead(response.statusCode ?? 502, response.headers);
      response.pipe(res);
    });
    upstream.on('error', () => { if (!res.headersSent) res.writeHead(502); res.end(); });
    req.pipe(upstream);
  }

  async upgrade(req, client, head) {
    client.on('error', () => {});
    let url;
    try { url = new URL(req.url); } catch { client.destroy(); return; }
    const port = Number(url.port) || 80;
    const target = await this.resolve(url.hostname, port);
    if (target.refusal) {
      this.note(url.href, target.refusal);
      client.end('HTTP/1.1 403 Forbidden\r\n\r\n');
      return;
    }
    const upstream = net.connect(port, target.address, () => {
      const lines = [];
      for (let i = 0; i < req.rawHeaders.length; i += 2) lines.push(`${req.rawHeaders[i]}: ${req.rawHeaders[i + 1]}`);
      upstream.write(`${req.method} ${url.pathname}${url.search} HTTP/1.1\r\n${lines.join('\r\n')}\r\n\r\n`);
      if (head?.length) upstream.write(head);
      upstream.pipe(client);
      client.pipe(upstream);
    });
    upstream.on('error', () => client.destroy());
  }
}
