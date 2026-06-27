param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('x64', 'arm64')]
    [string] $Arch,

    [Parameter(Mandatory = $true)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$artifactsDir = Join-Path $repoRoot 'artifacts'
$runtime = if ($Arch -eq 'x64') { 'win-x64' } else { 'win-arm64' }
$publishDir = Join-Path $artifactsDir "publish\$runtime"
$exePath = Join-Path $artifactsDir "HyoPDF-$Version-$Arch.exe"
$installerPlatform = $Arch

if (-not (Test-Path $exePath)) {
    throw "Expected published executable at $exePath. Run Publish-App.ps1 first."
}

if ($Version -match '^(\d+\.\d+\.\d+)') {
    $msiVersion = "$($Matches[1]).0"
}
else {
    $msiVersion = '1.0.0.0'
}

Push-Location $repoRoot
try {
    dotnet build (Join-Path $repoRoot 'installer\HyoPDF.Installer\HyoPDF.Installer.wixproj') `
        -c Release `
        -p:PublishDir=$publishDir `
        -p:ProductVersion=$msiVersion `
        -p:InstallerVersion=$Version `
        -p:InstallerPlatform=$installerPlatform `
        -p:OutputPath=$artifactsDir

    $builtMsi = Get-ChildItem $artifactsDir -Filter "HyoPDF-$Version-$Arch.msi" | Select-Object -First 1
    if (-not $builtMsi) {
        throw "MSI was not produced in $artifactsDir"
    }

    Write-Host "Built $($builtMsi.FullName)"
}
finally {
    Pop-Location
}
