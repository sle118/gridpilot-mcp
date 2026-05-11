param(
    [string]$ChromePath = "",
    [string]$PreviewHtml = "docs/preview/readme-presentation-preview.html",
    [string]$OutputDir = "docs/preview/out",
    [int[]]$ViewportWidths = @(1440, 1200, 980),
    [int]$ViewportHeight = 2600
)

$ErrorActionPreference = "Stop"

function Resolve-ChromePath {
    param([string]$PreferredPath)

    if ($PreferredPath -and (Test-Path $PreferredPath)) {
        return (Resolve-Path $PreferredPath).Path
    }

    $candidates = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "$env:ProgramFiles(x86)\Google\Chrome\Application\chrome.exe",
        "$env:LocalAppData\Google\Chrome\Application\chrome.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Google Chrome was not found. Pass -ChromePath with a valid executable path."
}

function Convert-ToFileUrl {
    param([string]$Path)

    $resolvedPath = (Resolve-Path $Path).Path
    return ([System.Uri]$resolvedPath).AbsoluteUri
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedChrome = Resolve-ChromePath -PreferredPath $ChromePath
$resolvedPreview = Resolve-Path (Join-Path $repoRoot $PreviewHtml)
$previewUrl = Convert-ToFileUrl -Path $resolvedPreview
$resolvedOutputDir = Join-Path $repoRoot $OutputDir

if (-not (Test-Path $resolvedOutputDir)) {
    New-Item -ItemType Directory -Path $resolvedOutputDir | Out-Null
}

foreach ($width in $ViewportWidths) {
    $outputPath = Join-Path $resolvedOutputDir ("readme-preview-{0}.png" -f $width)
    & $resolvedChrome `
        --headless=new `
        --disable-gpu `
        --hide-scrollbars `
        --window-size="$width,$ViewportHeight" `
        --screenshot="$outputPath" `
        $previewUrl | Out-Null

    if (-not (Test-Path $outputPath)) {
        throw "Expected screenshot was not created: $outputPath"
    }

    Write-Output "Rendered $outputPath"
}
