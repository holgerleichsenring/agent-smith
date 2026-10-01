// 2026-10-01-283de: the local origin an uploaded set is served from. Not file:// — ES modules,
// fetch and root-relative paths all fail there, and a site that renders differently is not the
// site uploaded. The origin's root is the directory of the set's shallowest index.html (the
// folder an upload's paths usually share), or the set's own root when it has none.
import fs from 'node:fs/promises';
import http from 'node:http';
import path from 'node:path';

const types = {
  '.html': 'text/html; charset=utf-8', '.htm': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg', '.gif': 'image/gif', '.webp': 'image/webp', '.avif': 'image/avif',
  '.ico': 'image/x-icon', '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf',
  '.otf': 'font/otf', '.txt': 'text/plain; charset=utf-8', '.md': 'text/plain; charset=utf-8',
};

async function htmlFiles(root, dir = root, depth = 0, found = []) {
  if (depth > 8) return found;
  for (const entry of await fs.readdir(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) await htmlFiles(root, full, depth + 1, found);
    else if (/\.html?$/i.test(entry.name)) found.push(path.relative(root, full));
  }
  return found;
}

const shallowest = files => files
  .sort((a, b) => a.split(path.sep).length - b.split(path.sep).length || a.localeCompare(b))[0];

// The origin's root and the path to navigate to; `page` is relative to the site root or the set root.
export async function locate(setRoot, page) {
  const pages = await htmlFiles(setRoot);
  const index = shallowest(pages.filter(p => /^index\.html?$/i.test(path.basename(p))));
  const siteRoot = index ? path.join(setRoot, path.dirname(index)) : setRoot;
  const entry = page
    ? [path.join(siteRoot, page), path.join(setRoot, page)].find(p => pages.includes(path.relative(setRoot, p)))
      ?? path.join(siteRoot, page)
    : path.join(setRoot, index ?? shallowest(pages) ?? 'index.html');
  const root = entry.startsWith(siteRoot + path.sep) ? siteRoot : setRoot;
  return { root, path: '/' + path.relative(root, entry).split(path.sep).map(encodeURIComponent).join('/') };
}

export async function serve(root) {
  const server = http.createServer(async (req, res) => {
    let file;
    try {
      file = path.resolve(root, '.' + decodeURIComponent(new URL(req.url, 'http://local').pathname));
    } catch { res.writeHead(400).end(); return; }
    if (file !== root && !file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
    try {
      if ((await fs.stat(file)).isDirectory()) file = path.join(file, 'index.html');
      const body = await fs.readFile(file);
      res.writeHead(200, { 'content-type': types[path.extname(file).toLowerCase()] ?? 'application/octet-stream' });
      res.end(body);
    } catch { res.writeHead(404).end(); }
  });
  await new Promise(done => server.listen(0, '127.0.0.1', done));
  return server.address().port;
}
