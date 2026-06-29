param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('x64', 'arm64')]
    [string] $Arch
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$artifactsDir = Join-Path $repoRoot 'artifacts'
$files = @(
    "HyoPDF-$Version-$Arch.exe",
    "HyoPDF-$Version-$Arch.msi"
)

if ($Arch -eq 'x64') {
    $files += "HyoPDF-$Version-Setup.exe"
}

foreach ($file in $files) {
    $path = Join-Path $artifactsDir $file
    if (-not (Test-Path $path)) {
        Write-Warning "Skipping missing artifact: $path"
        continue
    }

    $hash = (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = "$path.sha256"
    Set-Content -Path $checksumPath -Value "$hash  $file" -NoNewline -Encoding ascii
    Write-Host "Wrote $checksumPath"
}
