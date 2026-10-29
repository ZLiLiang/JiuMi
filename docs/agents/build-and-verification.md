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

- `pwsh ./publish.ps1` publishes `src/JiuMi.Desktop` as a self-contained `win-x64` build and writes `artifacts/JiuMi-<version>-win-x64.zip`; the ZIP contains `JiuMi.Desktop.exe` at its root. The build copies Magick.NET's `Notice.txt` next to it as `THIRD-PARTY-NOTICES.txt` (Apache-2.0 and ImageMagick License texts); do not remove it from the package.
- Publishing passes `-p:DebugType=None -p:DebugSymbols=false`, which also removes the native `libSkiaSharp.pdb` and `libHarfBuzzSharp.pdb` that SkiaSharp and HarfBuzzSharp ship. `publish.ps1` fails when the publish directory or the ZIP still contains a `*.pdb`.
- `publish.ps1` is packaging, not a gate. `verify.ps1` still decides whether a change is done; publishing must not re-implement the build or test commands.
- The semantic version comes from `<Version>` in `Directory.Build.props`. Bump it there when cutting a release. The SDK appends the source revision to the informational version (`<version>+<commit>`), while the application's version helper strips that metadata before Settings → About displays it.
- `.github/workflows/ci.yml` runs on pull requests only, skipping changes that touch just `docs/**` or Markdown, plus manual dispatch: the `verify` job calls `pwsh ./verify.ps1`, then the `publish` job runs `pwsh ./publish.ps1` and uploads the ZIP as an Actions artifact. Pushes do not start a run, so manual dispatch is the way to cut an artifact from `dev`.
- Signing, auto-update, rollback, non-Windows artifacts and store distribution are out of scope.

### Package size

Baseline measured on 2026-10-29 (version 1.0.0, win-x64, Magick.NET included):

| Artifact | Files | Size |
| --- | ---: | ---: |
| Publish directory | 249 | 235.8 MB |
| `*.pdb` inside it | 6 | 100.3 MB |
| Publish directory without `*.pdb` | 243 | 135.6 MB |
| ZIP without `*.pdb` | 243 | 59.7 MB |

`libSkiaSharp.pdb` (80.1 MB) and `libHarfBuzzSharp.pdb` (19.9 MB) make up almost all of the symbol weight; the four application `.pdb` files are under 0.3 MB in total. After removing symbols, the largest entries are `Magick.Native-Q16-x64.dll` (23.6 MB), `System.Private.CoreLib.dll` (15.3 MB), `libSkiaSharp.dll` (11.1 MB), `System.Private.Xml.dll` (7.4 MB), `av_libglesv2.dll` (5.1 MB), `coreclr.dll` (4.4 MB), `FluentAvalonia.dll` (3.5 MB) and `System.Linq.Expressions.dll` (3.5 MB).

The only assets unused on win-x64 are the macOS and Linux Avalonia backends (`Avalonia.Native`, `Avalonia.Metal`, `Avalonia.X11`, `Avalonia.FreeDesktop*`, about 1.5 MB), debugger-only runtime files such as `Microsoft.DiaSymReader.Native.amd64.dll` (2.3 MB), and runtime-pack defaults that the app does not touch (`System.Data.Common`, `System.Private.DataContractSerialization`, `Microsoft.VisualBasic*`, about 5.8 MB). They stay in the package: removing them does not reach the disk budget and risks the runtime or platform detection.

### Trimming

`PublishTrimmed` is not enabled. The switch would live in `src/JiuMi.Desktop/JiuMi.Desktop.csproj` as `<PublishTrimmed>true</PublishTrimmed>` with `<TrimMode>partial</TrimMode>`. A trial with those properties fails the publish build because `Directory.Build.props` turns trim-analyzer warnings into errors: IL2026/IL2072 fire on five reflection-based `JsonSerializer` calls (settings, world-clock cache, holiday cache, localization), the reflection `Binding` in `LocalizeExtension`, a reflection binding compiled from `Styles/ShellStyles.axaml`, and `ViewLocator`'s `Assembly.GetType` plus `Activator.CreateInstance`. `CommunityToolkit.Mvvm` produced no trim warnings; ILLink also reports `Avalonia.Controls.DataGrid` and `Avalonia.DesignerSupport` as assemblies with their own trim warnings. Suppressing the analysis builds, but the result cannot start: trimming disables reflection-based JSON by default, and localization loading throws `InvalidOperationException: Reflection-based serialization has been disabled for this application`.

A forced trial (`-p:SuppressTrimAnalysisWarnings=true -p:JsonSerializerIsReflectionEnabledByDefault=true`) did run, and the shell, world-clock page and image-conversion page rendered; it shrank the symbol-free publish directory to 77.7 MB and the ZIP to 33.6 MB. It is still not enabled, because it hides the trim warnings that predict exactly these failures. Re-evaluate only after the JSON paths move to a `JsonSerializerContext`, `LocalizeExtension` and the shell styles stop creating reflection `Binding`s, the view types that `ViewLocator` resolves are preserved explicitly, and the third-party trim warnings are addressed or rooted.

## Rules

- A code change is complete only after `pwsh ./verify.ps1` passes.
- If the build method changes, update `verify.ps1`, `Directory.Build.props`, this document, `AGENTS.md`, and any CI workflow in the same PR. Do not introduce a second gate command.
- Warnings are errors. Suppress narrowly with `#pragma` or `SuppressMessage` and a reason; do not add global `<NoWarn>`.
- When existing code is first brought under the gate, fix violations or suppress them narrowly. Do not disable `TreatWarningsAsErrors` to make the gate pass.
- `verify.ps1` accepts `JiuMi.sln` or `JiuMi.slnx` (the .NET 10 default). If neither exists, it exits with a clear message and success; the gate activates automatically once one is added. It prefers `JiuMi.sln` when both exist; do not keep two divergent solution files.
- `global.json` pins the SDK to `10.0.402` with `latestFeature` roll-forward. Change it only as part of a deliberate build-method change.
- Test projects follow the `*.Tests.csproj` naming convention so the gate can find them.
