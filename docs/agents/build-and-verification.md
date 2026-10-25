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

## Rules

- A code change is complete only after `pwsh ./verify.ps1` passes.
- If the build method changes, update `verify.ps1`, `Directory.Build.props`, this document, `AGENTS.md`, and any CI workflow in the same PR. Do not introduce a second gate command.
- Warnings are errors. Suppress narrowly with `#pragma` or `SuppressMessage` and a reason; do not add global `<NoWarn>`.
- When existing code is first brought under the gate, fix violations or suppress them narrowly. Do not disable `TreatWarningsAsErrors` to make the gate pass.
- `verify.ps1` accepts `JiuMi.sln` or `JiuMi.slnx` (the .NET 10 default). If neither exists, it exits with a clear message and success; the gate activates automatically once one is added. It prefers `JiuMi.sln` when both exist; do not keep two divergent solution files.
- `global.json` pins the SDK to `10.0.402` with `latestFeature` roll-forward. Change it only as part of a deliberate build-method change.
- Test projects follow the `*.Tests.csproj` naming convention so the gate can find them.
