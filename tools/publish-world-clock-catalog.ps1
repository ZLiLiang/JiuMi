# Publishes the generated world clock catalog to the stable release tag the application reads.
#
# Development only; it is not part of the build and not run by CI. Run
# tools/generate-world-clock-data.ps1 first, commit the generated files, then publish.
#
# The JSON files go up before the manifest, so a client never reads a manifest whose files
# are not published yet. Assets are replaced in place: the manifest URL stays stable and each
# data version stays immutable through its hashes.
#
#   pwsh ./tools/publish-world-clock-catalog.ps1
#   pwsh ./tools/publish-world-clock-catalog.ps1 -WhatIf

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\src\JiuMi.Application\Assets\world-clock'),
    [string]$Repository = 'ZLiLiang/JiuMi',
    [string]$ReleaseTag = 'world-clock-catalog',
    [string]$Gh = 'gh'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$dataFiles = @('countries.json', 'cities.json', 'office-hours.json')
$manifestPath = Join-Path $SourceDirectory 'manifest.json'

foreach ($name in $dataFiles + 'manifest.json') {
    $path = Join-Path $SourceDirectory $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing $path; run tools/generate-world-clock-data.ps1 first."
    }
}

& $Gh release view $ReleaseTag --repo $Repository 2>$null | Out-Null
$releaseExists = $LASTEXITCODE -eq 0
if (-not $releaseExists) {
    if ($PSCmdlet.ShouldProcess($ReleaseTag, "create release $Repository")) {
        & $Gh release create $ReleaseTag --repo $Repository --title 'World clock catalog' --notes 'World clock country, city and office-hours data. Published by tools/publish-world-clock-catalog.ps1.'
        if ($LASTEXITCODE -ne 0) {
            throw "gh release create failed with exit code $LASTEXITCODE."
        }
    }
}
else {
    Write-Host "Release $ReleaseTag already exists; replacing its assets."
}

# Versions must only move forward, or clients keep reporting "up to date" on old data.
$previousVersion = 0
if ($releaseExists) {
    $downloadDirectory = Join-Path ([IO.Path]::GetTempPath()) ('jiumi-catalog-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $downloadDirectory | Out-Null
    try {
        & $Gh release download $ReleaseTag --repo $Repository --pattern 'manifest.json' --dir $downloadDirectory --clobber 2>$null
        $publishedManifest = Join-Path $downloadDirectory 'manifest.json'
        if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $publishedManifest)) {
            $previousVersion = [int](Get-Content -LiteralPath $publishedManifest -Raw | ConvertFrom-Json).version
        }
    }
    finally {
        Remove-Item -LiteralPath $downloadDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$version = [int]$manifest.version
if ($version -le $previousVersion) {
    throw "Manifest version $version must be higher than the published version $previousVersion."
}

$dataPaths = $dataFiles | ForEach-Object { Join-Path $SourceDirectory $_ }
if ($PSCmdlet.ShouldProcess($ReleaseTag, 'upload the catalog documents')) {
    & $Gh release upload $ReleaseTag @dataPaths --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0) {
        throw "Uploading the catalog documents failed with exit code $LASTEXITCODE."
    }
}

if ($PSCmdlet.ShouldProcess($ReleaseTag, 'upload the catalog manifest')) {
    & $Gh release upload $ReleaseTag $manifestPath --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0) {
        throw "Uploading the catalog manifest failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Published catalog version $version to $Repository release $ReleaseTag."
