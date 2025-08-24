param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug',
    [switch]$Restore,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

function Pause-And-Exit([int]$code) {
    Write-Host ''
    Read-Host 'Press Enter to exit'
    exit $code
}

try {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projPath = Join-Path $scriptDir 'Elara.Updater.Dev.csproj'

    if (-not (Test-Path $projPath)) {
        Write-Error "Project file not found: $projPath"
        Pause-And-Exit 1
    }

    Write-Host "Project: $projPath"
    Write-Host "Configuration: $Configuration"

    if ($Clean) {
        Write-Host 'Cleaning project...'
        & dotnet clean --nologo -c $Configuration "$projPath"
    }

    if ($Restore) {
        Write-Host 'Restoring packages...'
        & dotnet restore --nologo "$projPath"
    }

    Write-Host 'Building project...'
    & dotnet build --nologo -c $Configuration "$projPath"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        Pause-And-Exit $LASTEXITCODE
    }

    Write-Host 'Build succeeded.' -ForegroundColor Green
    Pause-And-Exit 0
}
catch {
    Write-Error $_
    Pause-And-Exit 1
}
