# Claude Code Skills — Khoi Pro

`.claude/skills/` holds project-specific skills so future Claude Code sessions follow this repo's
actual architecture and conventions instead of generic defaults. Generated 2026-08-28 alongside
`docs/architecture-audit.md`; each skill encodes facts verified during that audit, not assumptions.

Four pre-existing skills (`postgres-dapper`, `dotnet-clean-architecture`, `fullstack-feature-builder`,
`react-premium-ui`) were removed as part of this pass — the audit found they asserted the wrong
stack for this repo (Dapper, CQRS/MediatR, React 19, TypeScript — this repo uses none of those; see
the "Existing Skills Audit" findings folded into `docs/architecture-audit.md`'s history). Their
replacements below are correct for the actual stack. `deployment-debugging` was kept as-is — it's
diagnostic-oriented and complements the new `deployment-review` rather than duplicating it.

| Skill | Purpose | When to use |
|---|---|---|
| `react-feature-development` | Build/extend frontend UI for a feature the backend already supports | Adding a component, form, or modal to an existing feature area (Vault, Wiki, Library, Ideas, Finance, Reminders, Settings, Team, Projects, Tasks) |
| `ui-modernization` | Improve visual design, accessibility, responsiveness of existing screens | Spacing/consistency passes, modal accessibility fixes, responsive-table fixes — never for adding features |
| `dotnet-api-integration` | Wire frontend to backend, or change an endpoint the frontend calls | API client work, DTO shape changes, checking a frontend call against its real controller |
| `regression-safe-refactor` | Refactor with proof behavior didn't change | Simplifying, deduplicating, extracting, renaming, reorganizing working code |
| `test-existing-feature` | Add/improve tests for existing functionality | Backend service/validator/controller tests, frontend component/utility tests — knows the real coverage gaps |
| `code-review` | Repo-specific review checklist (layering, upload-path handling, permission mapping) | Alongside a general review — this one only knows this codebase's specific gotchas |
| `frontend-quality-gate` | Run and honestly report `npm run build`/`test:run` | Before calling frontend work done |
| `dotnet-quality-gate` | Run and honestly report `dotnet build`/`test` | Before calling backend work done |
| `deployment-review` | Pre-ship checklist for Vercel/CORS/production-config changes | Reviewing a deployment-related change before it ships (complements `deployment-debugging`, which diagnoses an already-broken one) |

## Invoking

```text
/react-feature-development
/ui-modernization
/dotnet-api-integration
/regression-safe-refactor
/test-existing-feature
/code-review
/frontend-quality-gate
/dotnet-quality-gate
/deployment-review
```

## Validation performed

Every `SKILL.md` under `.claude/skills/` was checked for: valid YAML frontmatter (opening/closing
`---`, no unescaped colons breaking the scalar), a `name:` matching its directory name, and a
`description:` specific enough to signal when it applies (not "use this for .NET work"). All 10
skills (9 new + `deployment-debugging`) passed.

## Keeping these current

These skills cite specific facts from the 2026-08-28 audit (test counts, known gaps, file:line
references) that will drift as the codebase changes. When a referenced fact stops being true (a
gap gets closed, a file gets moved), update the skill — don't let it keep citing stale numbers as
current state.
