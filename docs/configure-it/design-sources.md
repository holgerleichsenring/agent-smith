# Design sources

A ticket that says "build it like the Figma frame" used to leave the agent guessing, because a Figma link opened as a web page returns the application shell and none of the design. A design source fixes that. You register a Figma token once, give a project access to it, and from then on the agent reads a linked frame through Figma's REST API: sizes, spacing, colours, fonts, text, and a rendered PNG of the frame.

Figma is the only vendor today.

## What you need

- A Figma personal access token whose account can open the files your designs live in. Agent Smith sends it as `X-Figma-Token` to `api.figma.com` and nowhere else.
- That token in an environment variable of the server process, say `FIGMA_TOKEN`: in the compose `.env`, or in the Kubernetes Secret the deployment reads its environment from. The process reads its environment when it starts, so restart it after adding the variable.

Figma's variables (design tokens) come from its Variables API, which needs a Figma Enterprise plan and a token with the `file_variables:read` scope. Without those, a read still returns every value set on the nodes and says the variables were unavailable. It never invents a token name it didn't read.

## Setting it up in the studio

1. **Secrets** → New secret. A secret you create in the studio reads the environment variable of exactly its name, so call it `FIGMA_TOKEN`. The studio stores the name; the value stays in the environment.
2. **Design sources** → New design source. Give it an id (`figma-acme`, say), pick the vendor `figma`, pick the secret from step 1 as its auth secret, and optionally a display name. The drawer won't save without a vendor and a secret.
3. **Projects** → edit the project → **identity** tab. A **design sources** picker appears there once the catalog holds an entry. Pick the source. A project can list several, for instance one per Figma organisation.

While a design source names a secret, that secret can't be deleted. A project that names a design source the catalog doesn't hold is refused on save, and at startup it's a blocking finding that takes the project out rather than letting it start without the access it declared.

The same thing in `agentsmith.yml`:

```yaml
secrets:
  figma_token: ${FIGMA_TOKEN}

design_sources:
  figma-acme:
    vendor: figma
    auth: figma_token          # a name from secrets:, never the token
    display_name: Acme design system

projects:
  todo-list:
    agent: claude-default
    tracker: todo-issues
    repos: [todo-list-web]
    design_sources: [figma-acme]
```

A design source whose `auth` names no entry of `secrets:` is a blocking finding and isn't built.

## What the agent can do with it

With a design source on the project, the coding master of a run and the design partner in [Work it out](../how-it-works/work-it-out.md) both get a tool called `design_read`. Scan pipelines don't get it, and neither do the sub-agents of a design conversation. A project without a design source never sees the tool.

`design_read` takes a Figma link that carries `node-id`, the link you get from "Copy link to selection" on a frame. It answers with:

- the file's name, version and last modification;
- one line per node below the frame (three levels deep by default, up to six): type, name, size, auto-layout, colours as hex, corner radius, text with its font, the component an instance is of, applied style and variable names;
- the file's variables per mode, when Figma answered for them;
- a PNG export of the frame, at most 1568 pixels on its longest side, which a vision model sees right after the answer.

A large frame is cut to a character budget, and the answer says how many nodes it left out and that reading a child node's link goes further. A failed read names its kind (`not_found`, `forbidden`, `rate_limited` with the wait, `unreachable`), and the agent passes that on as it came. A link without `node-id` is answered with a request for the frame's link.

The token stays on the server. The model names a link, and the server's Figma client adds the token to the request. The token is looked up at the moment of each read and is masked in everything the run records, including a secret registered after the server started.

### Links in tickets and conversations

Any Figma link in a ticket's description, its comments, a design conversation or the ticket a conversation is about is listed to the model under "Design references", rebuilt from its file key and node id, up to ten of them. Nothing is fetched up front; the agent reads the frames the work depends on. A project without a design source gets the same list with a note that the links can't be read here.

### Versions

When the design partner writes a ticket or spec from a frame, it cites the link with its `node-id` and the version `design_read` reported, for example `https://www.figma.com/design/<key>?node-id=1-2 (version 123456)`, and writes the acceptance criteria from the concrete values it read. The run that builds it passes that version back to `design_read`, which then says whether the design moved since (`design moved: <cited> -> <current>`) and can read the cited version instead of the current one. The coding master reports a moved design in its result rather than quietly building either version.

Every read in a run is recorded as a `DesignRead` event with the source, file key, node and version, so the run's event log shows which design the work was built against. A design conversation has no run, so its reads are recorded nowhere; the version travels in the ticket text it writes.
