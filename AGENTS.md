## Agent skills

### Issue tracker

Issues live as GitHub issues in `ZLiLiang/JiuMi`, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical roles: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Git commits

- Use Conventional Commits: `type(scope): subject`, with `type` from `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `build`, `ci`, `perf`, `style`.
- Write the subject in the imperative mood, lowercase, no trailing period, at most 72 characters.
- Wrap body lines at 72 characters; use the body to explain *why*, not *what*.
- Commit message content is **English**, even when the repo's docs are Chinese.
