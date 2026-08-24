# Access control: claims, roles, permissions

Every route this server maps declares the **permission** a caller needs — `runs.read`,
`config.write`, `secrets.read`. Permissions are bundled into **roles**. Roles are held by
callers because their **identity provider** says so.

That last sentence is the whole design, and it is worth stating plainly:

> The directory says which roles a caller holds. The application says what those roles
> may do. **Nobody assigns a person a role inside agent-smith.** There is no user store,
> no member list and no screen on which an administrator grants somebody a role — and
> there will not be one. Identity-to-roles happens at the identity provider, as an
> app-role assignment or a group membership.

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

**3. Write the mapping** into the `auth:` block of `agentsmith.yml` (see
`config/agentsmith.example.yml`):

```yaml
auth:
  authority: https://login.example.com/realms/agentsmith
  audience: agent-smith
  enforce: false
  group_roles:
    "11111111-1111-1111-1111-111111111111":
      - operator
  roles:
    config-viewer:
      - config.read
```

**4. Restart the server.** The auth block is *bootstrap* configuration: it is read from
the file and the environment before the config store exists, because the authority that
decides who may read the store cannot be read out of it. Unlike the Config Studio's
settings, it does not reload live — a change costs one restart.

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
* **Custom roles are additive.** A name that collides with a built-in role does not
  replace it, and a permission name outside the catalog is dropped from the bundle rather
  than granted. Both are reported in `findings`.

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
