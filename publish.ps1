$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src/JiuMi.Desktop/JiuMi.Desktop.csproj'
$artifacts = Join-Path $PSScriptRoot 'artifacts'
$publishDir = Join-Path $artifacts 'publish/win-x64'

if (-not (Test-Path $project)) {
    throw "No desktop project found at '$project'."
}

$version = (dotnet msbuild $project -nologo -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($version)) {
    throw 'Could not read <Version>; set it in Directory.Build.props.'
}

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Write-Host "Publishing JiuMi $version (win-x64, Release)..."
dotnet publish $project -c Release -r win-x64 --self-contained true --nologo -o $publishDir
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Join-Path $publishDir 'JiuMi.Desktop.exe'
if (-not (Test-Path $exe)) {
    throw "Published output is missing '$exe'."
}

$zip = Join-Path $artifacts "JiuMi-$version-win-x64.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip
Write-Host "Artifact: $zip"
