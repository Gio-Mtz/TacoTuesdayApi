# Deployment runbook

Read this before every `az containerapp update`. It is short on purpose.

The API is deployed by hand — there is no deploy workflow in this repository, and that is
deliberate: `ci.yml` builds, tests and proves the Dockerfile still builds, but it pushes
nothing and deploys nothing. A deployment is a decision, not a side effect of a push.

Which means the checks below are not automated either. Nothing will stop you.

---

## 1. The one setting that must never reach production

```
Testing:AllowMissingDatabase        →  env var  Testing__AllowMissingDatabase
```

If this is `true` in production, **the API boots, reports itself healthy, and loses every
signup**. Not "degrades". Loses. And nothing in the platform will tell you.

### What it actually does

Three lines of `src/TacoTuesday.Api/Program.cs` decide it:

| Line | What happens when the flag is `true` |
| --- | --- |
| `:80` | the flag is read into `allowMissingDatabase` |
| `:82` | `LeadsConnectionString.ThrowIfUnusable(...)` **returns early instead of throwing** — see `Configuration/LeadsConnectionString.cs`, `if (allowMissing) return;` |
| `:88` | `if (!allowMissingDatabase && ...)` is false, so **`AddLeadsPersistence` never runs** |

And `AddLeadsPersistence` is where all three of these live
(`src/Modules/TacoTuesday.Modules.Leads/LeadsModule.cs:32-39`):

- `AddDbContext<LeadsDbContext>` — the database connection
- `AddScoped<ILeadStore, EfCoreLeadStore>` — the only thing that writes a lead
- `AddDbContextCheck<LeadsDbContext>(name: "leads-db", tags: ["ready", "db"])` — the only
  readiness check this API has

Skip that one call and you lose the database, the store and the health check **together**.
There is no fallback store: `InMemoryLeadStore` exists, but it is never registered in the
container — `grep -rn "InMemoryLeadStore" src/` finds only its own definition. It is a unit
test fixture, constructed with `new` in `tests/`.

### Why both probes still answer Healthy

This is the part that makes the failure invisible, and it is worth understanding once:

- **`/health/live`** (`Program.cs:121`) is mapped with `Predicate = _ => false`. It runs **no
  checks at all, by design** — it answers "the process is up", nothing more. It is Healthy on
  a container that cannot do a single useful thing.
- **`/health/ready`** (`Program.cs:123`) is mapped with no predicate, so it runs every
  registered check. With the flag on, **the registered set is empty** — and an empty set is
  `Healthy`. Zero checks failed out of zero.

So `/health/ready` answers **200 Healthy** either way, and the default response body is the
word `Healthy` with no breakdown. **You cannot tell the two states apart from outside.** A
200 from `/health/ready` proves nothing about the database. (Lesson #52 in the handoff, and
lesson #48: even when `leads-db` *is* registered, `AddDbContextCheck` proves the
connection, not the schema.)

Container Apps reads those probes, marks the revision healthy, and sends it traffic.

### What the user gets

`POST /api/leads` resolves `CreateLeadHandler` from DI **per request** — it is a parameter of
the endpoint delegate (`Features/CreateLead/CreateLeadEndpoint.cs`) and registered Scoped by
`AddHandlersFrom`. Its constructor is `CreateLeadHandler(ILeadStore store, IClock clock)`.
With `ILeadStore` unregistered, the container cannot construct it, and the request dies in
`UseExceptionHandler` as a **500 ProblemDetails** — on every submission, from the first one.

[ADR 0005](adr/0005-leads-persistence.md) said so when the flag was introduced, in the same
words: *"Setting it in production does not get you a working API; it gets you a 500 on the
first POST."*

> This is strictly worse than the API refusing to start. A revision that fails to boot never
> takes traffic. This one takes all of it.

### Why the flag is not simply deleted

Because the integration tests need it, and that is its only legitimate use.
`tests/TacoTuesday.IntegrationTests/TacoTuesdayApiFactory.cs:24` sets it to `true` so the
test host can boot **without** a SQL Server connection string, and then line 27 registers its
own persistence over SQLite in-memory:

```csharp
builder.UseSetting("Testing:AllowMissingDatabase", "true");

builder.ConfigureTestServices(services =>
    services.AddLeadsPersistence(options => options.UseSqlite(_connection)));
```

Read in that order, the flag means **"skip the production persistence registration, something
else will provide it."** In the tests, something else does. In production, nothing does.

The setting is also the documented opt-out in
[ADR 0005](adr/0005-leads-persistence.md) — a setting rather than a check on the
environment name, on purpose (`docs/adr/0005-leads-persistence.md:121`).

**This is documented debt, tracked as TD-029, and the mitigation is this page.** Closing it in
code means making the flag impossible to turn on outside the test host — which is a C# change,
and the whole reason it is still open.

---

## 2. Before you deploy

```bash
RG=rg-tacotuesday
APP=tacotuesday-api
```

### Read this first: every check below prints a number or a list, never silence

That is deliberate, and it is the **second** version of this section. The first one asked you to
read an *empty line* as the answer, and on 12-Oct-2026 that cost a round trip: the flag check
printed nothing, and "nothing" is also what you see when the query is wrong, when the resource
group is wrong, or when the container has no environment variables at all. Worse, an empty line
meant **"good"** in the old check (a) and **"stop the deploy"** in the old check (b) — the same
observable, two opposite verdicts, in two commands that looked alike. A procedure you run in the
middle of a deployment cannot ask you to tell those apart by eye.

Two traps that produced it, both still worth knowing:

- **`containers[0]`, not `containers[]`.** The flattening form collapses two levels, so "no
  matches" and "no container" come out looking the same.
- **`az containerapp show` returns the template of the latest revision *defined*, not the one
  currently serving traffic.** Step (d) covers that.

**a. Inventory first — this is the auto-check.** It must print at least
`ConnectionStrings__Leads`, because the app cannot boot without it (§1). **If this prints nothing
at all, your command is wrong — not the deployment.** Fix it before you read any verdict below.

```bash
az containerapp show -n $APP -g $RG \
  --query "properties.template.containers[0].env[].name | sort(@)" -o tsv
```

Known-good baseline, measured in production on 12-Oct-2026 — exactly these four names:

```
ConnectionStrings__Leads
Cors__AllowedOrigins__0
Cors__AllowedOrigins__1
Cors__AllowedOrigins__2
```

A fifth name deserves a look before you ship. A *missing* `Cors__AllowedOrigins__*` is the TD-022
failure: the API starts, every probe stays green, and only the browser breaks.

**b. The flag must not be in the template. This prints `0`.**

```bash
az containerapp show -n $APP -g $RG \
  --query "length(properties.template.containers[0].env[?starts_with(name, 'Testing')])" -o tsv
```

`0` is the only acceptable answer. `1` or more is a stop-the-deploy. Matching the **prefix**
rather than the full name is what makes this catch both spellings — `Testing__AllowMissingDatabase`
and `Testing:AllowMissingDatabase` — plus anything else that arrives under a `Testing` section.
**An empty line is not `0`:** it means the command failed, so go back to (a).

**c. The connection string must be there. This prints `1`.**

```bash
az containerapp show -n $APP -g $RG \
  --query "length(properties.template.containers[0].env[?name == 'ConnectionStrings__Leads'])" -o tsv
```

`0` is a stop: with the flag correctly absent, a missing connection string makes the app refuse to
boot (`Program.cs:82`), which is the designed behaviour and still a failed deployment. See
[docs/database.md](database.md#telling-the-app-where-the-database-is). Only the name is ever
printed — the value does not reach your terminal.

**d. Confirm what is actually serving.** (a) to (c) read the latest revision *defined*. After a
rollback, or when a new revision failed to start, that is not the revision answering requests:

```bash
az containerapp revision list -n $APP -g $RG \
  --query "[].{rev:name, weight:properties.trafficWeight, active:properties.active}" -o table
```

If the row carrying the traffic weight is not the newest one, re-run (a) to (c) against it with
`az containerapp revision show -n $APP -g $RG --revision <rev>` — the template sits at the same
path, `properties.template.containers[0].env[]`.

**e. CI is green on the commit you are about to ship.** All three jobs, including
`Container image builds` — the job that exists because a Dockerfile that no longer builds kept a
three-week-old image in production while both probes answered Healthy.

**f. The image tag is the one you just built.** `--image ...:v2` twice in a row deploys the old
image and the revision looks perfectly fine.

---

## 3. After you deploy — the check that is actually worth 60 seconds

Skip `/health/ready`. Section 1 explains why it cannot answer this question.

**Post a real lead and read the status code.** `kind`, `name` and `email` are all required
(`Features/CreateLead/CreateLeadHandler.cs`, `Validate`); `kind` must be exactly `company` or
`candidate`, and `company` is required too when it is `company`.

```bash
curl -i -sS -X POST https://<host>/api/leads \
  -H 'Content-Type: application/json' \
  -d '{"kind":"candidate","name":"Runbook check","email":"runbook-check+'"$(date +%s)"'@tacotuesdayco.com"}'
```

- **201** — the row was written. This is the only output that proves the database is wired up.
- **200 with `alreadyRegistered: true`** — the address was already there. The endpoint is
  idempotent by lowercased email; the `+$(date +%s)` above is what gets you a 201 instead, and
  it is a legal local part, the address still delivers.
- **400** — the payload, not the deployment. The body lists the field. Note that **this still
  proves nothing about the database**: validation runs before the store is touched — but the
  handler had to be constructed to run it, so a 400 does rule out a missing `ILeadStore`.
- **500** — read section 1 first, then section 4. The flag is the first suspect.
- **404** — the endpoint is not in the running image. The deployed revision is older than you
  think; check the image tag, not the code. This has happened, for three weeks.
- **429** — rate limiter, 5 per minute per IP (`appsettings.json`, `RateLimiting:Leads`). Wait
  and retry; it is not a failure.

**That 201 is a real row in the production table**, not a dry run — there is no test mode. Use
an address you will recognise later; `docs/database-access.md` has the query to read it back,
which is also how you confirm the row exists, because a 201 is the API's word for it and the
API has been wrong before.

---

## 4. If the flag is already on in production

```bash
az containerapp update -n $APP -g $RG \
  --remove-env-vars Testing__AllowMissingDatabase
```

**Remove the name step 2a actually printed, not the one above.** The check in 2b matches the
`Testing` *prefix*, so it fires on `Testing__AllowMissingDatabase` and on
`Testing:AllowMissingDatabase` alike — but `--remove-env-vars` takes an exact name, and removing
a spelling that is not there succeeds while changing nothing. Read the name off 2a and pass that.

**Confirm the flag name before you lean on it** — `az containerapp update --help | grep env`
takes a second, and this line was written from the CLI's documented surface, not run. If
`--remove-env-vars` is not there, `--replace-env-vars` with the full set you *do* want works
instead. Do **not** reach for `--remove-all-env-vars`: it would take `ConnectionStrings__Leads`
with it and you would trade this failure for the one in section 2c.

`--remove-env-vars` is a change to the container **template**, so it creates a new revision
and the new replicas start without it. That matters: a change to a *secret* does **not** create
a revision, and this is exactly the trap documented in
[docs/database-access.md](database-access.md) — the setting looks changed and the running
replicas never see it.

Then re-run the check in section 3. And assume **every signup since that revision started is
gone** — they were never written anywhere. There is no queue and no retry; the 500 happened
before the store would have been touched. If you need to know how many, the window is the
revision's start time to now, and the Container App logs are the only record:

```bash
az containerapp logs show -n $APP -g $RG --tail 200
```

---

## What this runbook does not cover

- **Migrations.** The app does not migrate itself on startup, on purpose.
  [docs/database.md](database.md) is the authority.
- **Database permissions and the managed identity.**
  [docs/database-access.md](database-access.md).
- **CORS.** `Cors:AllowedOrigins` ships as `[]` in `appsettings.json` and nothing guards it —
  a browser-only failure, invisible to every `curl` in this file. That is TD-022, and it is
  the other half of the same shape of problem: a default that is published broken.
