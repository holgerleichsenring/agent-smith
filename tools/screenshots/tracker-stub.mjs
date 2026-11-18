// A stand-in for the demo's GitHub tracker (a GitHub Enterprise-shaped REST API on localhost), so the
// ticket page can read issue bodies and comments without any external access. Read-only: every write
// answers 201 and is dropped. Names and texts are invented.
//   node tracker-stub.mjs [port]   (default 5918)
import { createServer } from "node:http";

const port = Number(process.argv[2] ?? 5918);
const base = `http://127.0.0.1:${port}`;
const hour = 3600_000;
const anchor = Date.now() - 5 * hour;
const at = (minutes) => new Date(anchor + minutes * 60_000).toISOString();
const sam = { login: "sam-sample", id: 1001, type: "User", html_url: `${base}/sam-sample` };
const alex = { login: "alex-example", id: 1002, type: "User", html_url: `${base}/alex-example` };

const issues = {
  39: { title: "Search: ignore accents in product names", body: "Searching for 'creme' should find 'Crème brûlée'.", state: "closed", created: -240 },
  41: {
    title: "Checkout: apply volume discounts to the cart total",
    body: "Orders of 10 or more units of one product get the tier discount from the price list. The cart shows the discount as its own line.\n\n**Acceptance**\n1. A cart line with 10+ units is priced at the tier rate.\n2. The cart total shows the discount as a separate line.\n3. Existing coupon codes still apply after the tier discount.",
    state: "open", created: -60,
    comments: [
      { user: sam, minutes: -45, body: "Tier rates are already in the price list export (column `tier_10`). Coupons must still work on top." },
      { user: alex, minutes: 55, body: "Checked on staging with a 12-unit cart and a coupon: the discount line shows, the coupon applies after it. Thanks!" },
    ],
  },
  44: { title: "Saved for later: keep cart items across sessions", body: "Saved items survive a new session.", state: "open", created: 105 },
  45: { title: "Reorder: one click from the order history", body: "Reorder a past order in one click.", state: "open", created: 105 },
  46: { title: "Saved for later: reminder email after 7 days", body: "Remind the customer of items saved for a week.", state: "open", created: 105 },
};

const issueJson = (n) => {
  const i = issues[n];
  return {
    number: Number(n), id: 5000 + Number(n), title: i.title, body: i.body, state: i.state,
    html_url: `https://github.com/acme/shop/issues/${n}`, user: sam, labels: [{ name: "shop" }],
    created_at: at(i.created), updated_at: at(i.created + 30), comments: (i.comments ?? []).length,
  };
};

createServer((req, res) => {
  const send = (status, body) => {
    res.writeHead(status, { "content-type": "application/json" });
    res.end(JSON.stringify(body));
  };
  if (req.method !== "GET") return send(201, {});
  const path = new URL(req.url, base).pathname.replace(/^\/api\/v3/, "");
  let m;
  if ((m = path.match(/^\/repos\/acme\/shop\/issues\/(\d+)\/comments$/)) && issues[m[1]])
    return send(200, (issues[m[1]].comments ?? []).map((c, k) => ({
      id: 9000 + Number(m[1]) * 10 + k, body: c.body, user: c.user,
      created_at: at(c.minutes), updated_at: at(c.minutes), html_url: `https://github.com/acme/shop/issues/${m[1]}#c${k}`,
    })));
  if ((m = path.match(/^\/repos\/acme\/shop\/issues\/(\d+)$/)) && issues[m[1]]) return send(200, issueJson(m[1]));
  if (path === "/repos/acme/shop/issues") return send(200, Object.keys(issues).map(issueJson));
  if (path === "/repos/acme/shop") return send(200, { id: 1, name: "shop", full_name: "acme/shop", owner: { login: "acme" }, html_url: "https://github.com/acme/shop" });
  return send(404, { message: "Not Found" });
}).listen(port, "127.0.0.1", () => console.log(`tracker stub on ${base}`));
