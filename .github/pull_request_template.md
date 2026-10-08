<!--
  Keep this short. The CI is the judge; this template is the human context around it.
  Delete the comment lines, not the headings. An empty section is an unanswered question.
-->

## What this changes

<!-- One or two sentences. What a reviewer would see if they called the API. -->

**Card:** <!-- US-0xx / TD-0xx / S0-x / OPS-xx -->

## Why it matters

<!-- The business value in one sentence: why a customer or Gio cares. -->

## Acceptance criteria

<!-- Copy them from the card and tick them one by one. An unticked box means not done. -->

- [ ]
- [ ]

## How it was verified

> Paste the **real output**, not a summary. "It should pass" is not evidence.

- [ ] `dotnet build` — exit 0
- [ ] `dotnet test` — exit 0, all three projects
      (`TacoTuesday.UnitTests`, `TacoTuesday.IntegrationTests`, `TacoTuesday.ArchitectureTests`)
- [ ] `GET /health/ready` → **200**, if this PR touches configuration, connection strings
      or startup
      <!-- A clean start proves nothing: the app boots happily with a connection string that
           cannot reach the database. 200 on /health/ready is the only evidence it can. -->

```text
paste here: the `dotnet test` summary line, and the /health/ready response with its status code
```

## 🔴 What was NOT verified

> **Required, and read this first when the PR was authored by Claude.**
>
> **There is no .NET SDK on either side of the bridge, and there is no way to get one** — the
> container's proxy blocks both `mcr.microsoft.com` and `docker.io`, so it cannot be installed
> and it cannot be run in a container either. That limit is settled; it is not worth re-testing.
>
> **So any C# in a PR Claude wrote was never compiled and never tested.** The commit message
> carries a `NOT VERIFIED:` section naming exactly what was not built. **The first real
> compilation happens in CI, on this branch, after Gio pushes.** Until that run is green, treat
> the C# here as a proposal, not as working code.
>
> This is also why backend cards are kept small and why wide refactors are not written blind.

<!-- List anything else that was not verified. If everything in scope was verified, write
     `nothing` on purpose — a blank section reads as a lapse. -->

## Data and migrations

<!-- Only if this PR touches entities, EF configuration or the schema. -->

- [ ] No schema change in this PR
- [ ] Schema changes, and the migration is in the diff, is reversible, and was named here

## Before merge

- [ ] **CI is green on this branch** — not "should be". Green. For this repo it is not a
      formality: it is the **first time the code was compiled at all**.
- [ ] The diff contains **only** the files this card needs (`git status --porcelain`)
- [ ] If there was an architecture decision, there is an ADR in `docs/adr/`
- [ ] Gio ran it locally against a real database and it behaves as a product
