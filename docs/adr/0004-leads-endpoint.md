# ADR-0004 — Posting the same address twice is a success, and the fence is a rate limit

- **Status:** accepted
- **Date:** 2026-09-29

## Context

`POST /api/leads` is the only call the landing page makes. Its contract was written on the
UI side first, on purpose, in `src/app/api/Leads/ILead.ts` of TacoTuesdayUI: the shape of the
request is a product decision about what we are willing to ask a stranger for, not an
implementation detail of a C# module.

Four facts constrain everything below.

1. **The response already carries `alreadyRegistered`.** ADR-0003 put it there so that
   idempotency would be *visible* rather than merely correct, and the form is already written
   to read it: it says "ya estabas en la lista" instead of congratulating somebody twice.
2. **The form's honeypot is a filter, not a fence.** It is a hidden field on a page. It stops
   a bot that renders the page and fills every input; it stops nothing at all that POSTs
   straight at the URL. ADR-0003 wrote down that the server has to carry its own limit, and
   this is where that debt comes due.
3. **The browser's validation is a courtesy.** Same reason. Whatever the form checks, the
   endpoint has to check again, and with the same numbers — if the two drift apart, a visitor
   fills in something the page accepts and the server rejects with no field highlighted.
4. **Nothing can be compiled or run here.** Claude has no .NET SDK and the Microsoft hosts are
   blocked (see EQUIPO - Roles y reglas). This story ships as `[requiere verificación de Gio]`
   and is not Done until `dotnet build` and `dotnet test` pass on his machine. That limit is
   the reason the .NET surface of this story is deliberately small.

## Options considered

### Duplicate email — Option A: 409 Conflict

- Pros: literally correct by the letter of HTTP. The resource exists; the request conflicts.
- Cons: **it is a lie about what happened to the person.** They typed their address into a
  waiting list twice. They are on the waiting list. That is the outcome they wanted, reached
  twice. Answering with an error means the form must special-case one error code into a
  success message, and every other client that ever calls this has to learn the same trick.

### Duplicate email — Option B: 200 with `alreadyRegistered: true`

- Pros: the endpoint has one success shape and one failure shape. Retries are safe: a flaky
  network, a double tap, a user who comes back next week all converge on the same row. The UI
  already consumes exactly this.
- Cons: a client that ignores the body cannot tell "added" from "was already there" — so the
  first insert answers **201** and a repeat answers **200**, which puts the distinction in the
  status line too for logs and caches.

### Abuse — Option A: keep leaning on the honeypot

- Pros: nothing to build.
- Cons: it is client-side. `curl` in a loop fills the table. Not a real option; listed because
  "the form already has protection" is the sentence that would have let it happen.

### Abuse — Option B: a per-IP rate limit on the endpoint

- Pros: it is in the framework (`Microsoft.AspNetCore.RateLimiting`), it is configuration, and
  it is the only layer that sees every caller regardless of how they got here.
- Cons: it needs the real client IP, and behind Container Apps ingress every request arrives
  from the ingress proxy. Without `UseForwardedHeaders` the whole internet shares one bucket
  and the first busy minute locks out every visitor at once.

### Storage — Option A: ship the SQL table in this story

- Pros: the endpoint would be real end to end on day one.
- Cons: it drags in EF Core, a mapping, a migration and the Azure SQL connection string, none
  of which can be run or reviewed here, and all of it would land in one pull request written
  blind. That is exactly the shape EQUIPO calls a bomb.

### Storage — Option B: an in-memory store behind an interface, table in US-005

- Pros: the contract, the idempotency and the validation become real and testable now, in a
  change small enough to review in minutes. US-005 swaps one line of registration.
- Cons: **the list does not survive a restart, and each replica keeps its own.** Container
  Apps scales to zero. Today an address can be accepted and then vanish.

## Decision

- **A repeated address is a success**: 201 the first time, **200 with
  `alreadyRegistered: true`** after that. Never 409. Uniqueness is decided on the **lowercased**
  address, and the address is also stored as the person typed it.
- **The endpoint is rate limited per client IP**, fixed window, limits from configuration
  (`RateLimiting:Leads`), defaulting to **5 per 60 seconds**. Rejections answer **429** — not
  the framework's default 503 — and carry `Retry-After`.
- **`UseForwardedHeaders` runs first in the pipeline**, with `KnownProxies` and `KnownNetworks`
  cleared, so the limiter partitions on the real caller.
- **`UseRateLimiter` runs after `UseCors`.** Reversed, a rate-limited visitor gets an opaque
  CORS failure and the UI reports "no pudimos conectar" instead of "demasiados intentos".
- **Server validation mirrors the form field for field**, including the exact regex Angular's
  `Validators.email` uses, copied rather than improved on.
- **Storage is in-memory behind `ILeadStore`.** US-005 replaces the registration and nothing
  else in the module moves.
- Ids are **UUID v7**, because US-005 turns them into a clustered primary key on Azure SQL and
  a random v4 fragments that index from the first thousand rows.

## Consequences

- **What gets easier:** the UI can be wired to a real endpoint (US-006) without waiting for a
  database. Every validation rule now has a test that fails if the two halves drift.
- **What gets harder:** there are now two copies of the same validation rules, in two
  languages, in two repositories. The unit tests are the only thing holding them together —
  a rule changed in `waitlist.ts` and not here is a silent 400 with no field highlighted.
- **What we are now committed to:** `alreadyRegistered` is public contract. Any store that
  replaces the in-memory one must be able to answer "was this address already here?"
  atomically — in SQL that is a unique index on the normalized column plus catching the
  duplicate-key violation, not a `SELECT` followed by an `INSERT`.
- **Clearing `KnownProxies` means trusting `X-Forwarded-For`.** That is only safe while
  ingress is the single way into the container. The day the container is reachable directly,
  this has to be revisited.
- **How we reverse this:** the 200/201 split is one `if` in `CreateLeadEndpoint`. The rate
  limit is a policy in `Program.cs` and two numbers in `appsettings.json` — it can be tuned
  without a redeploy, or removed by deleting one `.RequireRateLimiting(...)`.

## Failure modes

- **The container restarts and the list is gone.** Guaranteed, not hypothetical: Container
  Apps scales to zero. **This is why the landing must not be announced to anybody until US-005
  lands.** It is written here so that "it's live, send it around" is a decision and not an
  accident.
- **Two replicas, two lists.** Same cause. The same address can be accepted as new twice.
- **A shared NAT or a corporate proxy trips the limit.** Five per minute per IP means an
  office behind one address shares one budget. The numbers are configuration precisely so the
  first complaint costs an environment variable, not a deploy.
- **`X-Forwarded-For` is spoofed** and the limit partitions per attacker-chosen key, i.e. not
  at all. Only reachable if the container is exposed outside ingress.
- **The regexes drift.** The copy in `CreateLeadHandler` and the one in `waitlist.ts` are the
  same string today. Nothing enforces that but the comment on each and the tests on this side.
