# Publishes a tagged JiuMi release ZIP and updates the app-update/update.json manifests on GitHub and Gitee.
#
# Idempotent: rerunning for the same tag replaces existing assets in place without creating duplicates.
# Does not create git tags. In CI, triggered by pushing a v* tag; locally, used for reruns/recovery.
#
#   pwsh ./tools/publish-release.ps1
#   pwsh ./tools/publish-release.ps1 -SkipGitee
#   pwsh ./tools/publish-release.ps1 -SkipVerify

[CmdletBinding()]
param(
    [string]$Tag = '',
    [switch]$SkipVerify,
    [switch]$SkipGitee,
    [string]$GitHubRepository = 'ZLiLiang/JiuMi',
    [string]$GiteeRepository = 'ZLiLiang/JiuMi',
    [string]$UpdateTag = 'app-update',
    [string]$Gh = 'gh',
    [string]$Git = 'git'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$desktopProject = Join-Path $repoRoot 'src/JiuMi.Desktop/JiuMi.Desktop.csproj'
$artifactsDir = Join-Path $repoRoot 'artifacts'

$giteeQuotaBytes = 1073741824L # 1 GiB repository attachment quota
$giteeWarnThresholdBytes = [long][math]::Floor($giteeQuotaBytes * 0.8)

function Mask-Secret {
    param([string]$Text, [string]$Secret)
    if ([string]::IsNullOrEmpty($Text) -or [string]::IsNullOrEmpty($Secret)) {
        return $Text
    }
    return $Text.Replace($Secret, '***')
}

function Emit-WarningMessage {
    param([string]$Message)
    Write-Warning $Message
    if ($env:GITHUB_ACTIONS -eq 'true') {
        Write-Host "::warning::$Message"
    }
}

function Invoke-GitChecked {
    param([string[]]$Arguments, [string]$SecretToMask = '')
    $output = & $Git -C $repoRoot @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).Trim()
    if ($exitCode -ne 0) {
        $safeText = Mask-Secret -Text $text -Secret $SecretToMask
        throw "git failed with exit code ${exitCode}: $safeText"
    }
    return $text
}

function Get-ManifestChannelEntry {
    param(
        [string]$Version,
        [bool]$IsPrerelease,
        [string]$PublishedAt,
        [string]$PageUrl,
        [string]$AssetUrl,
        [string]$AssetSha256,
        [long]$AssetSize
    )
    return [ordered]@{
        version     = $Version
        prerelease  = $IsPrerelease
        publishedAt = $PublishedAt
        pageUrl     = $PageUrl
        assetUrl    = $AssetUrl
        assetSha256 = $AssetSha256
        assetSize   = $AssetSize
    }
}

function ConvertFrom-ReleaseHistoryEntry {
    param(
        [object]$Release,
        [string]$HostKind,
        [string]$Repository
    )
    if ($null -eq $Release) {
        return $null
    }

    $tagName = [string]$Release.tag_name
    if ([string]::IsNullOrWhiteSpace($tagName) -or $tagName -eq $UpdateTag) {
        return $null
    }

    $ver = if ($tagName.StartsWith('v', [StringComparison]::Ordinal)) { $tagName.Substring(1) } else { $tagName }
    if ($ver -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
        return $null
    }

    $zipAsset = $null
    if ($null -ne $Release.assets) {
        foreach ($asset in $Release.assets) {
            if ([string]$asset.name -like 'JiuMi-*-win-x64.zip') {
                $zipAsset = $asset
                break
            }
        }
    }

    $publishedAt = if ($Release.PSObject.Properties['published_at'] -and -not [string]::IsNullOrWhiteSpace([string]$Release.published_at)) {
        [string]$Release.published_at
    }
    elseif ($Release.PSObject.Properties['created_at'] -and -not [string]::IsNullOrWhiteSpace([string]$Release.created_at)) {
        [string]$Release.created_at
    }
    else {
        [DateTimeOffset]::UtcNow.ToString('O')
    }

    $pageUrl = if ($HostKind -eq 'Gitee') {
        "https://gitee.com/$Repository/releases/tag/$tagName"
    }
    else {
        "https://github.com/$Repository/releases/tag/$tagName"
    }

    $assetUrl = if ($null -ne $zipAsset -and $zipAsset.PSObject.Properties['browser_download_url']) {
        [string]$zipAsset.browser_download_url
    }
    else {
        $null
    }

    $assetSize = if ($null -ne $zipAsset -and $zipAsset.PSObject.Properties['size']) {
        [long]$zipAsset.size
    }
    else {
        0L
    }

    return Get-ManifestChannelEntry `
        -Version $ver `
        -IsPrerelease ([bool]$Release.prerelease -or ($ver -match '-')) `
        -PublishedAt $publishedAt `
        -PageUrl $pageUrl `
        -AssetUrl $assetUrl `
        -AssetSha256 $null `
        -AssetSize $assetSize
}

function Write-ManifestFile {
    param(
        [string]$Path,
        [object]$StableEntry,
        [object]$PreviewEntry
    )
    $manifest = [ordered]@{
        generatedAt = [DateTimeOffset]::UtcNow.ToString('O')
        stable      = $StableEntry
        preview     = $PreviewEntry
    }
    $json = $manifest | ConvertTo-Json -Depth 6
    [System.IO.File]::WriteAllText($Path, $json + "`n", [System.Text.UTF8Encoding]::new($false))
}

# 1. Run verification gate unless explicitly skipped (CI runs verify.ps1 in an earlier job).
if (-not $SkipVerify) {
    Write-Host 'Running verification gate (verify.ps1)...'
    & pwsh (Join-Path $repoRoot 'verify.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "verify.ps1 failed with exit code $LASTEXITCODE."
    }
}

# 2. Read <Version> from Directory.Build.props and assert git / artifact consistency before any remote call.
$version = (dotnet msbuild $desktopProject -nologo -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($version)) {
    throw 'Could not read <Version>; set it in Directory.Build.props.'
}

$expectedTag = "v$version"
$effectiveTag = if ([string]::IsNullOrWhiteSpace($Tag)) { $expectedTag } else { $Tag.Trim() }
$normalizedTag = if ($effectiveTag.StartsWith('v', [StringComparison]::Ordinal)) { $effectiveTag.Substring(1) } else { $effectiveTag }
if ($normalizedTag -ne $version) {
    throw "Tag '$effectiveTag' does not match <Version> '$version' in Directory.Build.props."
}

$statusOutput = Invoke-GitChecked -Arguments @('status', '--porcelain')
if (-not [string]::IsNullOrWhiteSpace($statusOutput)) {
    throw "Working tree is dirty. Commit or stash changes before publishing a release.`n$statusOutput"
}

$headCommit = Invoke-GitChecked -Arguments @('rev-parse', 'HEAD')
$headTags = @(Invoke-GitChecked -Arguments @('tag', '--points-at', 'HEAD') -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($headTags -notcontains $effectiveTag) {
    throw "HEAD ($headCommit) is not tagged with '$effectiveTag'. Found tags on HEAD: $($headTags -join ', ')."
}

$zipName = "JiuMi-$version-win-x64.zip"
$zipPath = Join-Path $artifactsDir $zipName
if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Missing release artifact '$zipPath'. Run 'pwsh ./publish.ps1' first."
}

$assetSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$assetSize = (Get-Item -LiteralPath $zipPath).Length
$isPrerelease = $version -match '-'
$nowIso = [DateTimeOffset]::UtcNow.ToString('O')

Write-Host "Publishing JiuMi $version (tag: $effectiveTag, prerelease: $isPrerelease, size: $assetSize bytes, sha256: $assetSha256)..."

$githubError = $null
$giteeError = $null
$githubOtherChannelEntry = $null
$tempDir = Join-Path ([IO.Path]::GetTempPath()) ('jiumi-release-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    # 3. GitHub release + update.json (uploaded last).
    try {
        Write-Host "Publishing to GitHub ($GitHubRepository)..."
        & $Gh release view $effectiveTag --repo $GitHubRepository 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $createArgs = @('release', 'create', $effectiveTag, '--repo', $GitHubRepository, '--title', "JiuMi $version", '--generate-notes')
            if ($isPrerelease) {
                $createArgs += '--prerelease'
            }
            & $Gh @createArgs
            if ($LASTEXITCODE -ne 0) {
                throw "gh release create $effectiveTag failed with exit code $LASTEXITCODE."
            }
        }
        else {
            Write-Host "GitHub release $effectiveTag already exists; uploading asset with --clobber."
        }

        & $Gh release upload $effectiveTag $zipPath --repo $GitHubRepository --clobber
        if ($LASTEXITCODE -ne 0) {
            throw "gh release upload $zipName failed with exit code $LASTEXITCODE."
        }

        $ghReleaseInfo = (& $Gh release view $effectiveTag --repo $GitHubRepository --json publishedAt,url | ConvertFrom-Json)
        $ghPublishedAt = if (-not [string]::IsNullOrWhiteSpace([string]$ghReleaseInfo.publishedAt)) { [string]$ghReleaseInfo.publishedAt } else { $nowIso }
        $ghPageUrl = if (-not [string]::IsNullOrWhiteSpace([string]$ghReleaseInfo.url)) { [string]$ghReleaseInfo.url } else { "https://github.com/$GitHubRepository/releases/tag/$effectiveTag" }
        $ghAssetUrl = "https://github.com/$GitHubRepository/releases/download/$effectiveTag/$zipName"

        & $Gh release view $UpdateTag --repo $GitHubRepository 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            & $Gh release create $UpdateTag --repo $GitHubRepository --title 'Application update manifest' --notes 'Machine-readable update.json manifest published by tools/publish-release.ps1.'
            if ($LASTEXITCODE -ne 0) {
                throw "gh release create $UpdateTag failed with exit code $LASTEXITCODE."
            }
        }

        $ghExistingManifest = $null
        $ghPrevDir = Join-Path $tempDir 'gh-prev'
        New-Item -ItemType Directory -Path $ghPrevDir | Out-Null
        & $Gh release download $UpdateTag --repo $GitHubRepository --pattern 'update.json' --dir $ghPrevDir --clobber 2>$null
        $ghPrevManifestPath = Join-Path $ghPrevDir 'update.json'
        if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $ghPrevManifestPath)) {
            $ghExistingManifest = Get-Content -LiteralPath $ghPrevManifestPath -Raw | ConvertFrom-Json
        }

        $otherChannelName = if ($isPrerelease) { 'stable' } else { 'preview' }
        $ghOther = if ($null -ne $ghExistingManifest -and $ghExistingManifest.PSObject.Properties[$otherChannelName]) {
            $ghExistingManifest.$otherChannelName
        }
        else {
            $null
        }

        if ($null -eq $ghOther) {
            $ghReleases = & $Gh api "repos/$GitHubRepository/releases?per_page=30" | ConvertFrom-Json
            foreach ($rel in @($ghReleases)) {
                if ([bool]$rel.draft -or [string]$rel.tag_name -eq $UpdateTag) {
                    continue
                }
                $relIsPre = [bool]$rel.prerelease -or ([string]$rel.tag_name -match '-')
                if (($otherChannelName -eq 'preview' -and $relIsPre) -or ($otherChannelName -eq 'stable' -and -not $relIsPre)) {
                    $candidate = ConvertFrom-ReleaseHistoryEntry -Release $rel -HostKind 'GitHub' -Repository $GitHubRepository
                    if ($null -ne $candidate) {
                        $ghOther = $candidate
                        break
                    }
                }
            }
        }

        $githubOtherChannelEntry = $ghOther
        $ghCurrentEntry = Get-ManifestChannelEntry `
            -Version $version `
            -IsPrerelease $isPrerelease `
            -PublishedAt $ghPublishedAt `
            -PageUrl $ghPageUrl `
            -AssetUrl $ghAssetUrl `
            -AssetSha256 $assetSha256 `
            -AssetSize $assetSize

        $ghStable = if ($isPrerelease) { $ghOther } else { $ghCurrentEntry }
        $ghPreview = if ($isPrerelease) { $ghCurrentEntry } else { $ghOther }

        $ghManifestPath = Join-Path $tempDir 'github-update.json'
        Write-ManifestFile -Path $ghManifestPath -StableEntry $ghStable -PreviewEntry $ghPreview

        $ghUploadManifestPath = Join-Path $tempDir 'update.json'
        Copy-Item -LiteralPath $ghManifestPath -Destination $ghUploadManifestPath -Force
        & $Gh release upload $UpdateTag $ghUploadManifestPath --repo $GitHubRepository --clobber
        if ($LASTEXITCODE -ne 0) {
            throw "Uploading GitHub update.json failed with exit code $LASTEXITCODE."
        }

        Write-Host "GitHub release $effectiveTag and $UpdateTag/update.json updated."
    }
    catch {
        $githubError = $_.Exception.Message
        Write-Error "GitHub publishing failed: $githubError" -ErrorAction Continue
    }

    # 4. Gitee release + quota check + update.json (uploaded last).
    if (-not $SkipGitee) {
        $giteeToken = $env:GITEE_TOKEN
        try {
            if ([string]::IsNullOrWhiteSpace($giteeToken)) {
                throw 'GITEE_TOKEN environment variable is not set. Set GITEE_TOKEN or pass -SkipGitee.'
            }

            Write-Host "Publishing to Gitee ($GiteeRepository)..."
            $giteePushUrl = "https://oauth2:$giteeToken@gitee.com/$GiteeRepository.git"
            [void](Invoke-GitChecked -Arguments @('push', $giteePushUrl, "refs/tags/${effectiveTag}:refs/tags/${effectiveTag}") -SecretToMask $giteeToken)

            $giteeApiBase = "https://gitee.com/api/v5/repos/$GiteeRepository"
            $giteeReleases = @(Invoke-RestMethod -Method Get -Uri "$giteeApiBase/releases?access_token=$giteeToken&per_page=100")

            # Quota check: warn above 80% of 1 GiB without deleting anything.
            $totalAttachmentBytes = 0L
            foreach ($rel in $giteeReleases) {
                if ($null -ne $rel.assets) {
                    foreach ($asset in $rel.assets) {
                        if ($asset.PSObject.Properties['size'] -and $null -ne $asset.size) {
                            $totalAttachmentBytes += [long]$asset.size
                        }
                    }
                }
            }

            if ($totalAttachmentBytes -ge $giteeWarnThresholdBytes) {
                $usedMb = [math]::Round($totalAttachmentBytes / 1MB, 1)
                $pct = [math]::Round(($totalAttachmentBytes / [double]$giteeQuotaBytes) * 100, 1)
                Emit-WarningMessage "Gitee release attachment usage is ${usedMb} MB (${pct}% of 1 GiB quota). Consider manually pruning old release attachments."
            }

            $giteeRelease = $giteeReleases | Where-Object { [string]$_.tag_name -eq $effectiveTag } | Select-Object -First 1
            if ($null -eq $giteeRelease) {
                $createBody = @{
                    access_token     = $giteeToken
                    tag_name         = $effectiveTag
                    target_commitish = $headCommit
                    name             = "JiuMi $version"
                    body             = "JiuMi $version"
                    prerelease       = $isPrerelease
                } | ConvertTo-Json
                $giteeRelease = Invoke-RestMethod -Method Post -Uri "$giteeApiBase/releases" -ContentType 'application/json' -Body $createBody
            }
            else {
                Write-Host "Gitee release $effectiveTag already exists (id: $($giteeRelease.id)); replacing ZIP if present."
            }

            $giteeReleaseId = $giteeRelease.id
            $existingAttachFiles = @(Invoke-RestMethod -Method Get -Uri "$giteeApiBase/releases/$giteeReleaseId/attach_files?access_token=$giteeToken")
            foreach ($file in $existingAttachFiles) {
                if ([string]$file.name -eq $zipName) {
                    Invoke-RestMethod -Method Delete -Uri "$giteeApiBase/releases/$giteeReleaseId/attach_files/$($file.id)?access_token=$giteeToken" | Out-Null
                }
            }

            Invoke-RestMethod -Method Post -Uri "$giteeApiBase/releases/$giteeReleaseId/attach_files" -Form @{
                access_token = $giteeToken
                file         = Get-Item -LiteralPath $zipPath
            } | Out-Null

            # Ensure app-update release exists on Gitee.
            $giteeUpdateRelease = $giteeReleases | Where-Object { [string]$_.tag_name -eq $UpdateTag } | Select-Object -First 1
            if ($null -eq $giteeUpdateRelease) {
                $createUpdateBody = @{
                    access_token     = $giteeToken
                    tag_name         = $UpdateTag
                    target_commitish = $headCommit
                    name             = 'Application update manifest'
                    body             = 'Machine-readable update.json manifest published by tools/publish-release.ps1.'
                    prerelease       = $false
                } | ConvertTo-Json
                $giteeUpdateRelease = Invoke-RestMethod -Method Post -Uri "$giteeApiBase/releases" -ContentType 'application/json' -Body $createUpdateBody
            }

            $giteeExistingManifest = $null
            try {
                $giteeExistingManifest = Invoke-RestMethod -Method Get -Uri "https://gitee.com/$GiteeRepository/releases/download/$UpdateTag/update.json"
            }
            catch {
                $giteeExistingManifest = $null
            }

            $otherChannelName = if ($isPrerelease) { 'stable' } else { 'preview' }
            $giteeOther = if ($null -ne $giteeExistingManifest -and $giteeExistingManifest.PSObject.Properties[$otherChannelName]) {
                $giteeExistingManifest.$otherChannelName
            }
            else {
                $null
            }

            if ($null -eq $giteeOther) {
                foreach ($rel in $giteeReleases) {
                    if ([string]$rel.tag_name -eq $UpdateTag) {
                        continue
                    }
                    $relIsPre = [bool]$rel.prerelease -or ([string]$rel.tag_name -match '-')
                    if (($otherChannelName -eq 'preview' -and $relIsPre) -or ($otherChannelName -eq 'stable' -and -not $relIsPre)) {
                        $candidate = ConvertFrom-ReleaseHistoryEntry -Release $rel -HostKind 'Gitee' -Repository $GiteeRepository
                        if ($null -ne $candidate) {
                            $giteeOther = $candidate
                            break
                        }
                    }
                }
            }

            # First Gitee upload falls back to GitHub history for the other channel.
            if ($null -eq $giteeOther -and $null -ne $githubOtherChannelEntry) {
                $giteeOther = $githubOtherChannelEntry
            }

            $giteePublishedAt = if ($giteeRelease.PSObject.Properties['created_at'] -and -not [string]::IsNullOrWhiteSpace([string]$giteeRelease.created_at)) {
                [string]$giteeRelease.created_at
            }
            else {
                $nowIso
            }

            $giteeCurrentEntry = Get-ManifestChannelEntry `
                -Version $version `
                -IsPrerelease $isPrerelease `
                -PublishedAt $giteePublishedAt `
                -PageUrl "https://gitee.com/$GiteeRepository/releases/tag/$effectiveTag" `
                -AssetUrl "https://gitee.com/$GiteeRepository/releases/download/$effectiveTag/$zipName" `
                -AssetSha256 $assetSha256 `
                -AssetSize $assetSize

            $giteeStable = if ($isPrerelease) { $giteeOther } else { $giteeCurrentEntry }
            $giteePreview = if ($isPrerelease) { $giteeCurrentEntry } else { $giteeOther }

            $giteeManifestPath = Join-Path $tempDir 'update.json'
            Write-ManifestFile -Path $giteeManifestPath -StableEntry $giteeStable -PreviewEntry $giteePreview

            $giteeUpdateReleaseId = $giteeUpdateRelease.id
            $existingUpdateFiles = @(Invoke-RestMethod -Method Get -Uri "$giteeApiBase/releases/$giteeUpdateReleaseId/attach_files?access_token=$giteeToken")
            foreach ($file in $existingUpdateFiles) {
                if ([string]$file.name -eq 'update.json') {
                    Invoke-RestMethod -Method Delete -Uri "$giteeApiBase/releases/$giteeUpdateReleaseId/attach_files/$($file.id)?access_token=$giteeToken" | Out-Null
                }
            }

            Invoke-RestMethod -Method Post -Uri "$giteeApiBase/releases/$giteeUpdateReleaseId/attach_files" -Form @{
                access_token = $giteeToken
                file         = Get-Item -LiteralPath $giteeManifestPath
            } | Out-Null

            Write-Host "Gitee release $effectiveTag and $UpdateTag/update.json updated."
        }
        catch {
            $giteeError = Mask-Secret -Text $_.Exception.Message -Secret $giteeToken
            Write-Error "Gitee publishing failed: $giteeError" -ErrorAction Continue
        }
    }
    else {
        Write-Host 'Skipping Gitee publishing (-SkipGitee).'
    }
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

if ($null -ne $githubError -or $null -ne $giteeError) {
    throw 'Release publishing failed on one or more hosts. Review the errors above and rerun to recover.'
}

Write-Host "Release $effectiveTag published successfully."
