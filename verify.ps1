[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot 'JiuMi.sln'

if (-not (Test-Path $solution)) {
    $solution = Join-Path $PSScriptRoot 'JiuMi.slnx'
}

if (-not (Test-Path $solution)) {
    Write-Host "No solution found at 'JiuMi.sln' or 'JiuMi.slnx'; nothing to verify yet."
    exit 0
}

Write-Host "Building $solution ($Configuration)..."
dotnet build $solution -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$testProjects = @(Get-ChildItem -Path $PSScriptRoot -Recurse -Filter '*.Tests.csproj' -File)
if ($testProjects.Count -gt 0) {
    Write-Host "Running tests..."
    dotnet test $solution -c $Configuration --no-build --nologo
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host 'Checking formatting...'
dotnet format $solution --verify-no-changes --severity warn --no-restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host 'Verification passed.'
