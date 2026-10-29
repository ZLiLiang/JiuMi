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
dotnet publish $project -c Release -r win-x64 --self-contained true --nologo -o $publishDir -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Join-Path $publishDir 'JiuMi.Desktop.exe'
if (-not (Test-Path $exe)) {
    throw "Published output is missing '$exe'."
}

$pdbFiles = @(Get-ChildItem -Path $publishDir -Filter '*.pdb' -File -Recurse)
if ($pdbFiles.Count -gt 0) {
    throw "Published output still contains debug symbols: $($pdbFiles.FullName -join ', ')."
}

$zip = Join-Path $artifacts "JiuMi-$version-win-x64.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $pdbEntries = @($archive.Entries | Where-Object { $_.FullName -like '*.pdb' })
}
finally {
    $archive.Dispose()
}

if ($pdbEntries.Count -gt 0) {
    throw "ZIP '$zip' contains debug symbols: $($pdbEntries.FullName -join ', ')."
}

Write-Host ("Artifact: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
