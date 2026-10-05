# Contributing

Read this before your first pull request. It is short on purpose.

Setup lives in [`docs/onboarding.md`](docs/onboarding.md). This file is about **how work moves**.

---

## Who does what

| Role | Owns |
| --- | --- |
| **Stakeholder** | Scope. What is worth building and in what order. Final word |
| **Product Owner** | The backlog. Acceptance criteria. Says no to things that grow the sprint |
| **Scrum Master** | The board, the working agreement, removing blockers |
| **Developer** | The code, its tests, and saying out loud what was not verified |

Today Gio is stakeholder + DevOps and Claude is PO + SM + Dev. The roles are written down so
that when a real person takes one over, the handover is a sentence and not an archaeology
project.

## Branches

`main` is protected. Nothing is pushed to it directly, including by the person who owns the
repository.

```
feat/us-012-waitlist-analytics
fix/us-004-duplicate-email-returns-409
chore/bump-ef-core
docs/onboarding
```

`<type>/<us-id>-<short-description>`. The story id is there so that six months from now the
branch name still says why.

## Pull requests

One story, one pull request. If it takes more than about 400 changed lines, it is probably two
stories — say so instead of pushing it through.

The pull request template is a checklist, not decoration. **The line that matters most is the
one about what you did not verify.** Nobody is ever upset about a known gap that was written
down; everybody is upset about one that was not.

## Definition of Done

A story is done when **all** of these are true:

1. The acceptance criteria are met, each one checked individually.
2. There are tests for the behaviour, and they fail if the behaviour is removed.
3. **CI is green.** Not "should be" — green.
4. Someone ran it and saw it work. Automated tests are not the same as looking at it.
5. A non-obvious decision has an ADR in `docs/adr/`.
6. What was **not** verified is written in the pull request.

Point 3 is the one that costs the most and is worth the most. CI is the only thing here that
cannot talk itself into being satisfied.

## A constraint you need to know about

The Claude sessions that write most of the code **have no .NET SDK and cannot reach the
Microsoft package hosts**. That is a hard limit of the environment, not a preference.

Consequences, and they are not negotiable:

- Frontend changes are built and tested before they are proposed.
- **Backend changes are not compiled by their author.** CI is the first thing that compiles
  them, and it is the gate.
- Pull requests carrying unverified backend code say so in the description.
- Backend stories are kept small for exactly this reason. A wide refactor written blind is a
  bad trade.

If you are a human with a working SDK: you have an advantage here. Use it, and keep saying
plainly which parts you actually ran.

## Tests

| Kind | Where | What it protects |
| --- | --- | --- |
| Unit | `tests/TacoTuesday.UnitTests` | Domain rules |
| Integration | `tests/TacoTuesday.IntegrationTests` | Endpoints end to end, SQLite |
| Architecture | `tests/TacoTuesday.ArchitectureTests` | Module boundaries |

The architecture tests are load-bearing. A module may reference another module's `Contracts`
namespace and nothing else. When one fails, the rule was broken — do not relax the test.

## Commits

```
us-012: add analytics events to the waitlist form

The form now emits company_signup and candidate_signup...
```

Prefix with the story id. Imperative mood. The body explains **why**, not what — the diff
already says what.
