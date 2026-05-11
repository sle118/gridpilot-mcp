[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$AssetPath,

    [Parameter(Mandatory = $true)]
    [string]$RepositoryUrl,

    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [string]$MirrorBranchName = "main"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path $AssetPath)) {
    throw "Release asset not found: $AssetPath"
}

foreach ($entry in @(
    @{ Name = "RepositoryUrl"; Value = $RepositoryUrl },
    @{ Name = "GitHubToken"; Value = $GitHubToken }
)) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.Value)) {
        throw "$($entry.Name) is required but was empty. Check the GitLab CI/CD variable name, scope, and whether the variable is protected on this tag."
    }
}

function Normalize-ReleaseTag {
    param([string]$Tag)

    if ([string]::IsNullOrWhiteSpace($Tag)) {
        throw "Release version is required."
    }

    $normalized = $Tag.Trim()
    $normalized = ($normalized -split '/')[ -1 ]

    if ([string]::IsNullOrWhiteSpace($normalized)) {
        throw "Release version '$Tag' could not be normalized."
    }

    return $normalized
}

function Get-GitHubRepositorySlug {
    param([string]$Url)

    $patterns = @(
        '^(?:https?://)?(?:[^@/]+@)?github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?/?$',
        '^git@github\.com:(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$'
    )

    foreach ($pattern in $patterns) {
        if ($Url -match $pattern) {
            return "$($Matches.owner)/$($Matches.repo)"
        }
    }

    throw "RepositoryUrl must point to a GitHub repository. Received: $Url"
}

function Invoke-Git {
    param(
        [string]$RepoRoot,
        [string[]]$Arguments
    )

    & git -C $RepoRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed."
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$Version = Normalize-ReleaseTag -Tag $Version
$slug = Get-GitHubRepositorySlug -Url $RepositoryUrl

$basicAuthBytes = [System.Text.Encoding]::ASCII.GetBytes("x-access-token:$GitHubToken")
$basicAuthValue = [Convert]::ToBase64String($basicAuthBytes)
$gitExtraHeader = "AUTHORIZATION: Basic $basicAuthValue"

Invoke-Git -RepoRoot $repoRoot -Arguments @(
    "-c",
    "http.extraHeader=$gitExtraHeader",
    "-c",
    "credential.helper=",
    "-c",
    "core.askPass=",
    "-c",
    "credential.useHttpPath=false",
    "push",
    "--follow-tags",
    "https://github.com/$slug.git",
    "HEAD:refs/heads/$MirrorBranchName"
)

$headers = @{
    Authorization = "Bearer $GitHubToken"
    Accept = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$releaseApiBase = "https://api.github.com/repos/$slug/releases"
$release = $null

try {
    $release = Invoke-RestMethod -Method Get -Uri "$releaseApiBase/tags/$Version" -Headers $headers
}
catch {
    if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) {
        $release = $null
    }
    else {
        throw
    }
}

$releaseBody = @"
GridPilot MCP $Version

Portable Windows ZIP release with the host, proxy, tray shell, README, setup guide, and release manifest.
"@

$payload = @{
    tag_name = $Version
    target_commitish = $MirrorBranchName
    name = "GridPilot MCP $Version"
    body = $releaseBody
    draft = $false
    prerelease = $false
}

if ($null -eq $release) {
    $release = Invoke-RestMethod -Method Post -Uri $releaseApiBase -Headers $headers -ContentType "application/json" -Body ($payload | ConvertTo-Json -Depth 6)
}
else {
    $release = Invoke-RestMethod -Method Patch -Uri "$releaseApiBase/$($release.id)" -Headers $headers -ContentType "application/json" -Body ($payload | ConvertTo-Json -Depth 6)
}

$assetName = Split-Path $AssetPath -Leaf
foreach ($asset in @($release.assets)) {
    if ($asset.name -eq $assetName) {
        Invoke-RestMethod -Method Delete -Uri "$releaseApiBase/assets/$($asset.id)" -Headers $headers | Out-Null
    }
}

$uploadUrl = ($release.upload_url -replace '\{\?name,label\}$', '')
$encodedAssetName = [System.Uri]::EscapeDataString($assetName)

Invoke-WebRequest `
    -Method Post `
    -Uri "${uploadUrl}?name=$encodedAssetName" `
    -Headers $headers `
    -ContentType "application/zip" `
    -InFile $AssetPath | Out-Null

[pscustomobject]@{
    Repository = $slug
    Version = $Version
    ReleaseUrl = $release.html_url
    AssetName = $assetName
}
