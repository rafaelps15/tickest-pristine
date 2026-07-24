---
name: code-cleanup
description: Run a comprehensive Clean Architecture audit across the entire codebase (not just the working-tree diff) and fix what it finds — safe, mechanical issues applied directly; breaking or design-level changes explained and confirmed with the user before touching anything. Use for periodic cleanup passes, pre-release hardening, or when the user asks to "clean up the codebase," "fix inconsistencies," or "make the review findings work."
argument-hint: [optional: a layer, feature, or area to focus on; defaults to the whole codebase]
---

# Comprehensive Codebase Cleanup

Unlike `ca-review` (which reviews the current diff and reports without fixing), this skill audits the **whole codebase** against the template's conventions and **applies fixes**. Treat it as: audit like `ca-review`, then act on the findings.

## Workflow

1. **Map the codebase.** List every project and feature slice (`Glob`/`Explore`) so you know the real scope before auditing — don't assume it matches the last diff.
2. **Audit in parallel, split by layer.** For a full-codebase pass, spawn parallel `Explore` (read-only) agents, one per area: Domain+Infrastructure, Application (grouped by related features), Web.Api endpoints, and test coverage. Give each agent the checklist below plus explicit file paths to check — vague prompts produce vague findings. Run them in the background and wait for all to report before synthesizing.
3. **Verify every finding yourself before acting on it.** Sub-agent reports are a starting point, not ground truth — they searched fast and can misjudge severity. Before applying or flagging a finding, read the actual file(s) involved. In this template's history, an agent once flagged a missing per-owner permission split as a "Blocker: any user can delete any ticket" — reading the endpoint and the seed data showed the permission was already gated to Admin-only, so it was a design inconsistency worth asking about, not a security hole worth an urgent unilateral fix. Downgrade or reclassify findings that don't hold up.
4. **Split findings into two buckets:**
   - **Apply directly** — anything mechanical and unambiguously correct: pre-existing build/analyzer errors (`dotnet build` failing is itself a finding, fix it first), missing validators, missing response fields that exist on a sibling slice, entities not inheriting `Entity`, endpoints missing `.RequireAuthorization()`/`.AllowAnonymous()`/`:guid` route constraints, dead-simple test gaps, doc-comment clarifications.
   - **Ask first** — anything that changes behavior, authorization semantics, or deletes/adds a feature surface: splitting a flat permission into Own/Manage, adding domain events that touch a seeder or other non-HTTP caller, removing a fully-modeled but unused subsystem, wiring up vs. deleting a dead slice, changing default role permissions. Use `AskUserQuestion` with concrete options (not just "yes/no") — e.g. "remove the dead code" vs. "build the real feature" — so the user's answer determines real scope, not just permission to proceed.
5. **Apply the "direct" bucket now.** Build after each logical group of changes, not just at the end — this template treats warnings as errors (`TreatWarningsAsErrors`, `AnalysisMode=All`), so `dotnet build` catching a stray explicit-type-instead-of-`var` immediately is cheaper than debugging a pile of them later.
6. **Implement the "ask first" bucket** once the user has answered, following the existing slice/handler/endpoint conventions exactly (see `ca-review`'s checklist). When a fix has a non-obvious landmine — e.g. a domain event handler that reads `IUserContext` will crash if the entity method is also called from a seeder outside an HTTP request — design around it (event carries the actor id explicitly) rather than either skipping the fix or breaking the seeder.
7. **Treat test failures surfaced along the way as real bugs, not inconvenient tests.** A test that fails only because a test double (e.g. an in-memory `DbContext` double) doesn't mirror production configuration (query filters, etc.) is a test-infrastructure bug — fix the double, don't loosen the assertion. A test that fails because the HTTP response code is wrong is very likely a real bug in error-type-to-status-code mapping, not a bad test — trace it to the root cause (in this template's history: every handler-level "Unauthorized" `Result` mapped to HTTP 500 instead of 403, because `ErrorType` had no `Forbidden` case) and fix the root cause, updating every call site that hits it, not just the one the test happened to exercise.
8. **Fill test coverage gaps** found during the audit: missing handler tests (every `Result.Failure` path + happy path with domain-event assertions), missing validator `TestValidate` coverage, missing integration tests per feature area, and architecture-test gaps (e.g. `SharedKernel` purity not asserted).
9. **Run the full suite** (`dotnet build` then `dotnet test TickestPristine.slnx`, Docker required for integration tests) and don't report done until it's green.

## Checklist

Reuse `ca-review`'s checklist in full (layer boundaries, slice structure, error handling, validation & security, state changes & caching, tests) — this skill's job is to act on it, not redefine it.

## Output format

While auditing: report findings grouped as **Blockers**, **Convention violations**, and **Test gaps**, each with `file:line` and a one-line fix — same as `ca-review`. Once fixes start landing, switch to a running account of what changed and why, and call out explicitly anything still pending a user decision. Close with the final `dotnet build`/`dotnet test` result and a short list of what was fixed directly vs. what's still open.
