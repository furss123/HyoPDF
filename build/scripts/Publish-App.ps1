param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime,

    [Parameter(Mandatory = $true)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$profileName = if ($Runtime -eq 'win-x64') { 'win-x64' } else { 'win-arm64' }
$arch = if ($Runtime -eq 'win-x64') { 'x64' } else { 'arm64' }
$artifactsDir = Join-Path $repoRoot 'artifacts'
$publishDir = Join-Path $artifactsDir "publish\$Runtime"
$artifactName = "HyoPDF-$Version-$arch.exe"
$artifactPath = Join-Path $artifactsDir $artifactName

if (Test-Path $artifactsDir) {
    Get-ChildItem $artifactsDir -Filter "HyoPDF-$Version-$arch.*" | Remove-Item -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Push-Location $repoRoot
try {
    dotnet restore (Join-Path $repoRoot 'src\HyoPDF.App\HyoPDF.App.csproj') -r $Runtime

    dotnet publish (Join-Path $repoRoot 'src\HyoPDF.App\HyoPDF.App.csproj') `
        -c Release `
        -r $Runtime `
        -p:PublishProfile=$profileName `
        -p:HyoPDFVersion=$Version `
        --no-restore

    $publishedExe = Join-Path $publishDir 'HyoPDF.exe'
    if (-not (Test-Path $publishedExe)) {
        throw "Published executable not found at $publishedExe"
    }

    New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
    Copy-Item $publishedExe $artifactPath -Force
    Write-Host "Published $artifactPath"
}
finally {
    Pop-Location
}
