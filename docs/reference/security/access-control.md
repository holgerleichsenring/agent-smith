# Access control: claims, roles, permissions

Every route this server maps declares the **permission** a caller needs — `runs.read`,
`config.write`, `secrets.read`. Permissions are bundled into **roles**. Roles are held by
callers because their **identity provider** says so.

That last sentence is the whole design, and it is worth stating plainly:

> The directory says WHO a caller is. Signing in happens there and nowhere else: a caller
> the directory refuses resolves to nobody before any mapping is consulted, so nothing
> here can let somebody in. What a caller may DO is this installation's decision — a role
> claim, a group membership it maps, or a role an administrator grants a person on the
> **Access** surface.

A grant decides a role and never decides access. There is still no user store, and the
list of people on the Access surface is not one: it is the callers this installation has
actually seen, kept so an administrator picks a person instead of copying an identifier
out of a directory console.

## The three built-in roles

| Role | Holds |
| --- | --- |
| `reader` | what the agent DID — runs, the live run drawer, the catalog, the connection snapshot, its own identity |
| `operator` | everything `reader` holds, plus run control, run deletion, project init and connection probes |
| `admin` | the whole catalog, configuration and secrets included |

`reader` deliberately holds no `config.read`: the configuration is where credentials,
trackers and repositories are named, and "may look at the run list" is not "may read the
installation".

## First login, before anything is mapped

A fresh installation has an authority and no mapping, so the first caller resolves to
**zero roles**. That is expected, and it is why the walkthrough starts with a way in.

**1. Give yourself a way back in.** `AGENTSMITH_ADMIN_GRANT` names callers who hold
`admin` whatever the directory says. It is read from the environment and reaches no
editable surface — it is changed where the deployment is.

```bash
AGENTSMITH_ADMIN_GRANT=sub:00000000-0000-0000-0000-000000000000
```

Every entry is prefixed with the claim it is matched against, `group:` or `sub:`, and is
matched against **only** that claim. A grant tried across claim types would turn any
claim that happened to contain the value — an email, say — into an administrator. An
entry without a prefix grants nothing and is reported as a finding.

**2. Ask the server what your token carried.** `GET /api/identity` answers any
authenticated caller, including one with no roles at all — that is the caller it exists
for.

```json
{
  "authenticated": true,
  "subject": "00000000-0000-0000-0000-000000000000",
  "issuer": "https://login.example.com/realms/agentsmith",
  "roleClaim": "roles",
  "groupClaim": "groups",
  "roleClaimValues": [],
  "groupClaimValues": ["11111111-1111-1111-1111-111111111111"],
  "roles": ["admin"],
  "permissions": ["catalog.read", "config.read", "..."],
  "findings": []
}
```

`roleClaim` and `groupClaim` say which claims were **looked in**; the two `…Values`
arrays say what was **found there**. Those values are what a mapping is written from.

If the page instead says **"This server did not accept your token"**, no mapping will
help: the token was refused before any claim in it was read. The page names which check
refused it — audience, issuer, signature, expiry — and shows **both sides**: the authority
and audience this server expects, beside the audience, issuer and version the token you
just presented carried. The fix is written from the difference between them.

### One issuer, one audience, matched exactly

The audience is compared as a single string, and the issuer is the one this server's
authority publishes in its discovery document. There is no second accepted form of either:
no list, no prefix rule, no per-token adjustment.

That is what lets any one OIDC authority work here — a realm that has no `api://` form and
no token versions is a first-class case rather than an exception — but it also means a
directory that mints more than one shape needs the matching one named.

**Microsoft Entra mints two, and the API decides which.** Not the endpoint the sign-in used:
the resource's own app registration carries `requestedAccessTokenVersion`, and while it is
unset (the default of a portal-created registration) that resource gets **version 1** access
tokens even when every client talks to the `/v2.0` endpoint. The two shapes are:

| | version 1 | version 2 |
|---|---|---|
| `aud` | `api://<api-client-id>` | `<api-client-id>` |
| `iss` | `https://sts.windows.net/<tenant>/` | `https://login.microsoftonline.com/<tenant>/v2.0` |
| authority to configure | `https://login.microsoftonline.com/<tenant>/` | `https://login.microsoftonline.com/<tenant>/v2.0` |
| name claim to configure | `upn` | `preferred_username` |

Either column works, as long as **all of it** comes from one column. Correcting only the
audience leaves the issuer refusing, because the issuer follows the authority rather than
being configured beside it.

The dashboard's own authority is a separate setting and may legitimately sit in the other
column: it governs the sign-in and the `id_token`, while the server's governs the access
token. The banner that warns about two halves of a sign-in does not fire on that difference
alone — it asks whether a token actually survived.

If you have another API in the same directory that accepts both shapes, it is most likely
built on `Microsoft.Identity.Web`, which picks the expected audience per request from the
token's `ver` claim. This server does not, and does not intend to: the value it validates is
the value an operator configured.

**3. Grant the role** in the Config Studio, under **Access**. Four panes over one
document:

* **People** — everyone this installation has seen, searchable and paged, each row
  showing the roles they hold and where each one came from. Grant a role from the row.
  Somebody who has not called yet is added by hand and reads *not signed in yet* rather
  than a timestamp.
* **Groups** — every group value that has arrived or been mapped, and the roles it grants.
* **Roles** — the three built-in roles, what each holds, and how many people and groups
  carry it, above the full permission matrix. Below it, the roles this installation
  composed for itself: pick permissions out of the catalog, and the role stands beside the
  built-in three. The built-in bundles themselves are fixed.
* **Claim names** — which claims roles and groups are read out of, and the claim callers
  are named by.

**4. There is no step four.** A saved grant applies to the next request. What a role MEANS
and who holds it are both application configuration: they change when a team does, so they
live in the config store like every other setting and reload live.

The Access surface needs `access.read` and `access.write`, which only `admin` holds.
`config.write` is deliberately not enough: a custom role may bundle `config.write`, and
granting a role is how such a caller would make themselves an administrator and collect
the secrets permissions the catalog keeps separable.

### A grant remembers the claim it was written against

A person grant stores `{claim, value}`, not a bare value, and resolves only while `claim`
is the configured `name_claim`. Written under `preferred_username` and later read under
`email`, one value can name a different person entirely — the same cross-claim collision
`AGENTSMITH_ADMIN_GRANT`'s mandatory prefix refuses. A grant whose claim is no longer in
force grants nothing and says so in `findings`.

Values compare **ordinally**: `Ada@example.com` and `ada@example.com` are two identifiers,
not one word.

### Point `name_claim` at `sub` if you can

`sub` is opaque and never reused. `email` and `preferred_username` are editable by their
holder in common directory configurations — a person who can change their own can claim a
grant written for somebody else, and a grant reassigned after somebody leaves goes to
whoever inherits the address. The surface warns whenever `name_claim` is not `sub`.

### Every write leaves a route to admin

A role mapping that reaches the store must leave at least one way to reach `admin`: a
person granted it, a group mapped onto it, a role claim this installation reads, or a
non-empty `AGENTSMITH_ADMIN_GRANT`. The check sits on the document store rather than on a
route, because a save, an import, a revert and the bootstrap migration all write a mapping
and three of them never pass a settings endpoint. A refusal names the four routes.

### Who has been seen, and for how long

A validated caller is NOTED — subject, the name-claim value with its claim, the role and
group values that arrived — coalesced in memory and written off the request path. There is
no sign-in event to hook: a bearer token is checked on every request and this server holds
no session, so a row per validated token would be one write per request per caller. The
record fails open: nobody is ever refused because their observation could not be stored.

Observations are kept for `observation_retention_days` (90 by default) and are **not**
configuration — they never travel in a config export. Removing a person removes their
grant and their record in one action.

The *authority*, the *audience* and the *enforce* switch are different — they are
bootstrap configuration, read from the file and the environment before the config store
exists, because the authority that decides who may read the store cannot be read out of
it. A change to those three costs one restart.

An installation upgrading with `role_claim`, `group_claim`, `group_roles` or `roles`
still in its `auth:` block has them **imported once**, on the first boot after the
upgrade. Nothing is lost, and nothing has to be edited by hand. After that import the
store is the single answer, and the file's copy is ignored.

**5. Turn enforcement on** once `GET /api/identity` shows the roles you expect. Set an
authority with `enforce: false` first: tokens are validated and nothing is refused, which
is how an issuer gets proven before anybody can be locked out.

## What resolves, and how it compares

* **Role names fold case.** One directory emits an app-role value as the operator
  capitalised it, another lowercases everything; `Admin` and `admin` are the same role.
* **Group values compare exactly.** A group value is an opaque identifier, and
  case-folding an opaque identifier is a smell. The one exception is a leading slash on a
  group *path* — the console shows `platform-admins`, the token carries
  `/platform-admins` — which is normalised away.
* **A caller in two mapped groups holds the union** of both bundles, and the admin grant
  unions with whatever the token already carried.
* **Custom roles are additive, and composed here.** A role you write stands beside the
  built-in three and never replaces one: a name that collides with a built-in is refused at
  the save, as is a permission name the catalog does not contain — compared exactly, so
  `Runs.Read` is refused the way an invented name is. Both used to be accepted and then
  quietly dropped from the resolved bundle, which is why they are now refused while the
  person who typed them is still looking at them.
* **A role already configured is left alone unless you touch it.** The validation is over
  what a save ADDS OR CHANGES: a legacy bundle naming a permission this version no longer
  has keeps working, keeps its `findings` entry, and does not refuse an unrelated save.
* **Removing a role is refused while a holder can be seen.** Losing a role means losing
  every permission with it, so the refusal names who still holds it — granted here, through
  a mapped group, or seen arriving with it in the directory's claim. That list is a floor:
  somebody who holds it only through the directory and has not signed in inside the
  observation window is not in it. Withdrawing a permission FROM a role is not refused —
  narrowing a role is how a role is corrected.
* **A person grant unions with the directory's roles.** Somebody can hold `reader` from
  their directory and `admin` from a grant at the same time.

### A directory that nests its roles

Some providers do not put role names in a flat claim. Realm roles under
`realm_access.roles` and client roles under `resource_access.<client>.roles` arrive as a
single claim whose value is JSON text, and **no flat `role_claim` name can address
either**. Such an installation maps **groups** instead — or has its operator add a
protocol mapper that flattens the roles into a claim of their own.

### A directory that hides its groups

Past roughly two hundred group memberships some providers omit the group claim entirely
and send `_claim_names` / `_claim_sources` instead; a token delivered through a URL
fragment carries `hasgroups`. All three are detected, and reported as themselves:

> The token carries '_claim_names', which means the directory left its group claim out
> because the caller is in too many groups. No group mapping can resolve this caller;
> grant the role through a role claim instead.

That is a different problem from a group nobody mapped, and it is worth not confusing the
two: no mapping you write will ever match, because the values never arrived.

## Reading a refusal

With enforcement on, a caller missing a permission gets `403` and a body naming exactly
what was missing — never an empty body:

```json
{
  "error": "The caller is missing one or more permissions this route requires.",
  "missingPermissions": ["secrets.read"]
}
```

A route may state several permissions, and they are required **together**. The four
routes that cross the config/secrets boundary — the change feed, revert, export and
import — state both, so a holder of `config.read` alone is refused for exactly one
reason, and the body says which.

A `401` with a `WWW-Authenticate: Bearer` header is a different answer: it means no valid
token was presented at all.

## Attribution

A config change is attributed to the **principal** — the claim `name_claim` names,
`sub` by default. A change made with no principal is attributed to `dashboard`, as it
always was. Nothing about the attribution comes from the request, so nothing about it can
be forged by a client.
