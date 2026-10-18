## Agent skills

### Issue tracker

Issues live as GitHub issues in `ZLiLiang/JiuMi`, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical roles: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

`docs/architecture.md` is a reference manual, not required reading for every ticket. Use `docs/agents/architecture-map.md` and load only the relevant section.

### Code standards

C# code follows `docs/coding-guidelines/csharp.md`. Mechanical rules live in `.editorconfig` and `Directory.Build.props`. Treat the standard as the review rubric for code changes: cite its section or the relevant CA/IDE rule ID in review comments.

### Build and verification

Run `pwsh ./verify.ps1` before considering a code change done. It is the only supported gate: Release build with warnings as errors, tests when test projects exist, and `dotnet format --verify-no-changes`. CI, when present, must run the same script. See `docs/agents/build-and-verification.md`; do not introduce a second build command.

## Git commits

- Use Conventional Commits: `type(scope): subject`, with `type` from `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `build`, `ci`, `perf`, `style`.
- Write the subject in the imperative mood, lowercase, no trailing period, at most 72 characters.
- Wrap body lines at 72 characters; use the body to explain *why*, not *what*.
- Commit message content is **English**, even when the repo's docs are Chinese.
