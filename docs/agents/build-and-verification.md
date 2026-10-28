# Build and verification

This file is the single source of truth for how JiuMi is built and gated. Do not add alternate build commands to other docs or agent instructions.

## Commands

- Full gate: `pwsh ./verify.ps1`
- Build only: `dotnet build JiuMi.sln -c Release` or `dotnet build JiuMi.slnx -c Release`, whichever exists
- Tests only: `dotnet test JiuMi.sln -c Release --no-build` or `dotnet test JiuMi.slnx -c Release --no-build` (only meaningful when a `*.Tests.csproj` project exists)
- Format check: `dotnet format JiuMi.sln --verify-no-changes --severity warn --no-restore` or the same with `JiuMi.slnx`

## What the gate enforces

- `Directory.Build.props`: Nullable reference types, `latest-recommended` analyzers, IDE style rules during build, and warnings as errors.
- `.editorconfig`: formatting, naming, and CA/IDE rule severities.
- `.gitattributes`: keeps CRLF in the working tree, matching `.editorconfig` and the Windows CI checkout.
- `verify.ps1`: Release build, then tests when test projects exist, then a formatting check.
- CI, when present, must call `pwsh ./verify.ps1`; do not duplicate the commands in a workflow or another script.

## Publishing

- `pwsh ./publish.ps1` publishes `src/JiuMi.Desktop` as a self-contained `win-x64` build and writes `artifacts/JiuMi-<version>-win-x64.zip`; the ZIP contains `JiuMi.Desktop.exe` at its root.
- `publish.ps1` is packaging, not a gate. `verify.ps1` still decides whether a change is done; publishing must not re-implement the build or test commands.
- The semantic version comes from `<Version>` in `Directory.Build.props`. Bump it there when cutting a release. The SDK appends the source revision to the informational version (`<version>+<commit>`), while the application's version helper strips that metadata before Settings → About displays it.
- `.github/workflows/ci.yml` runs on pull requests only, skipping changes that touch just `docs/**` or Markdown, plus manual dispatch: the `verify` job calls `pwsh ./verify.ps1`, then the `publish` job runs `pwsh ./publish.ps1` and uploads the ZIP as an Actions artifact. Pushes do not start a run, so manual dispatch is the way to cut an artifact from `dev`.
- Signing, auto-update, rollback, non-Windows artifacts and store distribution are out of scope.

## Rules

- A code change is complete only after `pwsh ./verify.ps1` passes.
- If the build method changes, update `verify.ps1`, `Directory.Build.props`, this document, `AGENTS.md`, and any CI workflow in the same PR. Do not introduce a second gate command.
- Warnings are errors. Suppress narrowly with `#pragma` or `SuppressMessage` and a reason; do not add global `<NoWarn>`.
- When existing code is first brought under the gate, fix violations or suppress them narrowly. Do not disable `TreatWarningsAsErrors` to make the gate pass.
- `verify.ps1` accepts `JiuMi.sln` or `JiuMi.slnx` (the .NET 10 default). If neither exists, it exits with a clear message and success; the gate activates automatically once one is added. It prefers `JiuMi.sln` when both exist; do not keep two divergent solution files.
- `global.json` pins the SDK to `10.0.402` with `latestFeature` roll-forward. Change it only as part of a deliberate build-method change.
- Test projects follow the `*.Tests.csproj` naming convention so the gate can find them.
