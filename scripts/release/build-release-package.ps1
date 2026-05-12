[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($env:OS -ne "Windows_NT") {
    throw "GridPilot release packaging requires Windows."
}

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Get-ReleaseVersion {
    param([string]$ExplicitVersion, [string]$RepoRoot)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion)) {
        return $ExplicitVersion.Trim()
    }

    $describe = & git -C $RepoRoot describe --tags --match "v[0-9]*" --always --dirty
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($describe)) {
        return $describe.Trim()
    }

    throw "Unable to determine a release version. Pass -Version explicitly or run from a tagged commit."
}

function Copy-ReleaseFile {
    param(
        [string]$RepoRoot,
        [string]$SourceRelativePath,
        [string]$DestinationRoot,
        [string]$DestinationRelativePath
    )

    $sourcePath = Join-Path $RepoRoot $SourceRelativePath
    if (-not (Test-Path $sourcePath)) {
        throw "Missing release file: $SourceRelativePath"
    }

    $destinationPath = Join-Path $DestinationRoot $DestinationRelativePath
    $destinationDirectory = Split-Path $destinationPath -Parent
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    Copy-Item -Path $sourcePath -Destination $destinationPath -Force
}

$repoRoot = Get-RepoRoot
$releaseVersion = Get-ReleaseVersion -ExplicitVersion $Version -RepoRoot $repoRoot

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot ".tmp\release-pack"
}

$packageRootName = "gridpilot-mcp-$releaseVersion-windows-x64"
$stagingRoot = Join-Path $OutputRoot $packageRootName
$zipPath = Join-Path $OutputRoot "$packageRootName.zip"

Remove-Item -Path $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null

$selfContained = "true"
$publishTargets = @(
    @{
        Name = "host"
        Project = "src/ExcelMcp.ToolHost/ExcelMcp.ToolHost.csproj"
        DestinationRelativePath = "host"
    },
    @{
        Name = "proxy"
        Project = "src/ExcelMcp.ToolProxy/ExcelMcp.ToolProxy.csproj"
        DestinationRelativePath = "proxy"
    },
    @{
        Name = "tray"
        Project = "src/GridPilot.Tray/GridPilot.Tray.csproj"
        DestinationRelativePath = ""
    }
)

foreach ($target in $publishTargets) {
    $targetOutput = if ([string]::IsNullOrWhiteSpace($target.DestinationRelativePath)) {
        $stagingRoot
    }
    else {
        Join-Path $stagingRoot $target.DestinationRelativePath
    }
    New-Item -ItemType Directory -Force -Path $targetOutput | Out-Null

    & dotnet publish (Join-Path $repoRoot $target.Project) `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained $selfContained `
        -p:PublishSingleFile=false `
        -o $targetOutput

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $($target.Project)."
    }

    Get-ChildItem -Path $targetOutput -Recurse -File |
        Where-Object { $_.Name -match '^.*\.pdb$|^createdump\.exe$|^mscordaccore.*\.dll$|^mscordbi\.dll$' } |
        Remove-Item -Force
}

Copy-ReleaseFile -RepoRoot $repoRoot -SourceRelativePath "README.md" -DestinationRoot $stagingRoot -DestinationRelativePath "README.md"
Copy-ReleaseFile -RepoRoot $repoRoot -SourceRelativePath ".env.example" -DestinationRoot $stagingRoot -DestinationRelativePath ".env.example"
Copy-ReleaseFile -RepoRoot $repoRoot -SourceRelativePath "docs/topics/mcp-setup-and-troubleshooting.md" -DestinationRoot $stagingRoot -DestinationRelativePath "docs/topics/mcp-setup-and-troubleshooting.md"
Copy-ReleaseFile -RepoRoot $repoRoot -SourceRelativePath "docs/topics/public-distribution-and-release-workflow.md" -DestinationRoot $stagingRoot -DestinationRelativePath "docs/topics/public-distribution-and-release-workflow.md"

    $manifest = [ordered]@{
    version = $releaseVersion
    createdUtc = (Get-Date).ToUniversalTime().ToString("o")
    commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    configuration = $Configuration
    runtimeIdentifier = $RuntimeIdentifier
    packageRoot = $packageRootName
    files = @(
        "README.md"
        ".env.example"
        "docs/topics/mcp-setup-and-troubleshooting.md"
        "docs/topics/public-distribution-and-release-workflow.md"
        "GridPilot.Tray.exe"
        "host/"
        "proxy/"
    )
}

$manifestPath = Join-Path $stagingRoot "release-manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $stagingRoot,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

[pscustomobject]@{
    Version = $releaseVersion
    PackageRoot = $stagingRoot
    ZipPath = $zipPath
}
