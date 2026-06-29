param(
    [Parameter(Mandatory = $true)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$publishDir = Join-Path $repoRoot 'artifacts\publish\win-x64'
$issPath = Join-Path $repoRoot 'installer\HyoPDF.iss'
$setupPath = Join-Path $repoRoot "artifacts\HyoPDF-$Version-Setup.exe"

if (-not (Test-Path $publishDir)) {
    throw "Expected publish output at $publishDir. Run Publish-App.ps1 first."
}

& (Join-Path $PSScriptRoot 'Prepare-Assets.ps1')

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 was not found. Install it with: choco install innosetup --version=6.2.2 -y'
}

Push-Location $repoRoot
try {
    & $iscc $issPath "/DAppVersion=$Version"

    if (-not (Test-Path $setupPath)) {
        throw "Inno Setup installer was not produced at $setupPath"
    }

    Write-Host "Built $setupPath"
}
finally {
    Pop-Location
}
