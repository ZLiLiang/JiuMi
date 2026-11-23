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

Run `pwsh ./verify.ps1` before considering a code change done. It is the only supported gate: Release build with warnings as errors, tests when test projects exist, and `dotnet format --verify-no-changes`. CI, when present, must run the same script. See `docs/agents/build-and-verification.md`; do not introduce a second build command. The Windows publish and release flow is documented there too; publishing is not a gate.

### Release Gitee mirror sync

When cutting or completing a release after `.github/workflows/release.yml` publishes `v<version>` to GitHub, **the agent** runs `pwsh ./tools/publish-release.ps1 -SyncGiteeFromGitHub -Tag v<version>` locally (using the owner's `GITEE_TOKEN` user environment variable) to mirror the exact SHA-256-verified ZIP and `app-update/update.json` to Gitee, rather than asking the user to run it manually. See `docs/agents/build-and-verification.md`.

## Git commits

- Use Conventional Commits: `type(scope): subject`, with `type` from `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `build`, `ci`, `perf`, `style`.
- Write the subject in the imperative mood, lowercase, no trailing period, at most 72 characters.
- Wrap body lines at 72 characters; use the body to explain *why*, not *what*.
- Commit message content is **English**, even when the repo's docs are Chinese.

## Pull requests

- Open pull requests with the `pr` skill.
- Write the pull request body in **Chinese**; keep the title in Conventional Commits form.
- Link the ticket with `Closes #<n>`.
- Update the ticket once the pull request is open: tick the `## Acceptance criteria` items that are verified, post the resolution comment, and close the ticket. If the owner rejects the pull request, they will say so; reopen the ticket then.
- Never merge a pull request. Merging is the repository owner's decision: stop once the pull request is open.
