# ADR-0001 — API and UI live in two separate repositories

- **Status:** accepted
- **Date:** 2026-09-10

## Context

Taco Tuesday has two deployable units: a .NET modular monolith and an Angular SPA. They are
built by the same person today, but they have different build tools, different test runners,
different release cadences and different hosting.

## Options considered

### Option A — one repository containing both

- Pros: a change that spans API and UI is one commit and one PR, so the contract cannot drift.
  One CI pipeline. One place to look. Objectively simpler for a single developer.
- Cons: every CI run touches both toolchains unless paths are filtered. The Angular
  `node_modules` and the .NET `obj/` share a working tree. Cloning the API drags the UI along.

### Option B — two repositories

- Pros: each side owns its build, its versioning and its deploy. Matches how the work will be
  divided if anyone else joins. Either repo can be shown, cloned or open-sourced on its own.
- Cons: **the API contract can drift silently.** The API renames a field, the UI keeps sending
  the old one, and nothing fails until a user hits it in production. This is the real cost, and
  it is not hypothetical.

## Decision

We chose **Option B, two repositories**, and we pay the contract-drift cost deliberately rather
than by accident.

The mitigation is not discipline, it is tooling: the UI's HTTP client is **generated from the
API's OpenAPI document** and committed. A field rename on the API side then produces a failing
TypeScript compile in the UI, which is a build error instead of a production incident.

Until there are real endpoints to generate from, the UI hand-writes one service against
`/api/candidates/ping`. Generation lands with the first real feature, in its own ADR.

## Consequences

- What gets easier: independent deploys; each repo readable on its own; separate CI budgets.
- What gets harder: any change touching both sides is two PRs, and they must be ordered —
  API first (additive), UI second. Removing a field is a two-release dance, never one.
- What we are committed to: keeping the OpenAPI document accurate. It stops being documentation
  and becomes the contract the UI compiles against.
- **How we reverse this:** merging two repos into one is mechanical (`git subtree add` preserves
  both histories). The reverse direction is the cheap one, which is part of why B is acceptable.

## Failure modes

- The OpenAPI document drifts from reality (endpoint added without annotations) → the generated
  client silently lacks it. Guard: the integration tests assert the document contains every
  mapped endpoint.
- API and UI deploy out of order → the UI calls a field that does not exist yet. Guard: API
  changes are additive first; removals happen one release after the UI stops using them.
