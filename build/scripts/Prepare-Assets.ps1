param(
    [string]$AppIconPng = "assets\icons\app-icon-512.png",
    [string]$BannerPng = "assets\installer\banner-source.png",
    [string]$SideImgPng = "assets\installer\side-image-source.png"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$python = Get-Command python -ErrorAction SilentlyContinue

if (-not $python) {
    throw 'Python with Pillow is required. Install Python and run: pip install pillow'
}

& $python.Source (Join-Path $PSScriptRoot 'Prepare-Assets.py') `
    --app-icon (Join-Path $repoRoot $AppIconPng) `
    --banner (Join-Path $repoRoot $BannerPng) `
    --side (Join-Path $repoRoot $SideImgPng)
