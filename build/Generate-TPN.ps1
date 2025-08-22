param(
  [string]$ProjectPath,
  [string]$OutputPath,
  [switch]$VerboseLogging
)

$ErrorActionPreference = 'Stop'
if ($VerboseLogging) { $VerbosePreference = 'Continue' }

function Write-Info($msg) { Write-Host "[TPN] $msg" }

# Default to Elara.Host if not supplied
if (-not $ProjectPath) {
  $solutionRoot = Split-Path $PSScriptRoot -Parent
  $ProjectPath = Join-Path $solutionRoot 'Elara.Host\Elara.Host.csproj'
  Write-Info "No ProjectPath supplied. Defaulting to $ProjectPath"
}
if (-not $OutputPath) {
  $projDirDefault = Split-Path -Parent $ProjectPath
  $OutputPath = Join-Path $projDirDefault 'THIRD-PARTY-NOTICES.md'
  Write-Info "No OutputPath supplied. Will update only $OutputPath"
}

function Get-PackagesFromProjectJson {
  param([object]$json)
  $pkgs = @()
  foreach ($proj in $json.projects) {
    Write-Verbose ("Processing project entry with {0} frameworks" -f ($proj.frameworks | Measure-Object | Select-Object -ExpandProperty Count))
    foreach ($fw in $proj.frameworks) {
      if ($fw.topLevelPackages) { $pkgs += $fw.topLevelPackages }
      if ($fw.transitivePackages) { $pkgs += $fw.transitivePackages }
      if ($fw.dependencies) { $pkgs += $fw.dependencies } # older formats
    }
  }
  # Normalize to Name/Version
  $normalized = @()
  foreach ($p in $pkgs) {
    $nm = $null
    if ($p.id) { $nm = $p.id }
    elseif ($p.name) { $nm = $p.name }
    elseif ($p.packageName) { $nm = $p.packageName }
    $ver = $null
    if ($p.resolvedVersion) { $ver = $p.resolvedVersion }
    elseif ($p.version) { $ver = $p.version }
    elseif ($p.requestedVersion) { $ver = $p.requestedVersion }
    if ($nm -and $ver) {
      $normalized += [pscustomobject]@{ Name = $nm; Version = $ver }
    }
  }
  Write-Verbose ("Normalized {0} package entries" -f $normalized.Count)
  return $normalized
}

function Get-NuGetLicenseInfo {
  param([string]$Id, [string]$Version)
  $lower = $Id.ToLower()
  $regIndex = "https://api.nuget.org/v3/registration5-gz-semver2/$lower/index.json"
  try {
    $index = Invoke-RestMethod -UseBasicParsing -Method Get -Uri $regIndex
    $entry = $null
    foreach ($page in $index.items) {
      # Load items if paged
      $items = if ($page.items) { $page.items } else { (Invoke-RestMethod -UseBasicParsing -Uri $page.'@id').items }
      Write-Verbose ("Loaded {0} items from registration page" -f ($items | Measure-Object | Select-Object -ExpandProperty Count))
      $match = $items | Where-Object { $_.catalogEntry.version -eq $Version }
      if ($match) { $entry = $match[0].catalogEntry; break }
    }
    if (-not $entry) {
      return [pscustomobject]@{ LicenseExpression=$null; LicenseUrl=$null; ProjectUrl=$null; Repository=$null }
    }
    $repo = $null
    if ($entry.repository) { $repo = $entry.repository.url }
    return [pscustomobject]@{
      LicenseExpression = $entry.licenseExpression
      LicenseUrl        = $entry.licenseUrl
      ProjectUrl        = $entry.projectUrl
      Repository        = $repo
    }
  } catch {
    Write-Verbose ("NuGet metadata fetch failed for {0} {1}: {2}" -f $Id, $Version, $_.Exception.Message)
    return [pscustomobject]@{ LicenseExpression=$null; LicenseUrl=$null; ProjectUrl=$null; Repository=$null }
  }
}

function New-ThirdPartyNotices {
  param([array]$Packages)
  Write-Verbose ("Building TPN for {0} packages" -f ($Packages | Measure-Object | Select-Object -ExpandProperty Count))
  $lines = @()
  $lines += "# Third-Party Notices"
  $lines += ""
  $lines += "This file lists direct and transitive NuGet dependencies for this project, with their licenses and links."
  $lines += ""
  $count = ($Packages | Measure-Object | Select-Object -ExpandProperty Count)
  $lines += "Packages discovered: $count"
  $lines += ""
  $lines += "First few packages: " + (($Packages | Sort-Object Name, Version | Select-Object -First 10 | ForEach-Object { "{0} {1}" -f $_.Name, $_.Version }) -join ', ')
  $lines += ""
  foreach ($p in $Packages | Sort-Object Name, Version) {
    try {
      Write-Verbose ("Generating entry for {0} {1}" -f $p.Name, $p.Version)
      $info = Get-NuGetLicenseInfo -Id $p.Name -Version $p.Version
      $licenseText = if ($info.LicenseExpression) { $info.LicenseExpression } elseif ($info.LicenseUrl) { $info.LicenseUrl } else { "(license metadata unavailable)" }
      $lines += "## $($p.Name) $($p.Version)"
      $lines += "- License: $licenseText"
      if ($info.ProjectUrl) { $lines += "- Project: $($info.ProjectUrl)" }
      if ($info.Repository) { $lines += "- Source: $($info.Repository)" }
      $lines += ""
    } catch {
      Write-Verbose ("Failed to build entry for {0} {1}: {2}" -f $p.Name, $p.Version, $_.Exception.Message)
    }
  }
  return $lines -join "`r`n"
}

function Get-PackagesFromAssetsJson {
  param([string]$AssetsPath)
  $results = @()
  if (-not (Test-Path $AssetsPath)) { Write-Verbose "Assets file not found: $AssetsPath"; return $results }
  Write-Verbose "Parsing assets file: $AssetsPath"
  try {
    $assets = Get-Content -Raw -Path $AssetsPath | ConvertFrom-Json
    foreach ($key in $assets.libraries.PSObject.Properties.Name) {
      $lib = $assets.libraries.$key
      if ($lib.type -eq 'package') {
        $parts = $key -split '/'
        if ($parts.Length -ge 2) {
          $results += [pscustomobject]@{ Name = $parts[0]; Version = $parts[1] }
        }
      }
    }
  } catch {
    Write-Verbose ("Failed reading assets file: {0}" -f $_.Exception.Message)
  }
  return ($results | Sort-Object Name, Version -Unique)
}

function Fail {
  param([string]$Message, [string]$Details)
  Write-Error "[TPN] $Message"
  if ($Details) { Write-Host "[TPN] Details:" -ForegroundColor Yellow; Write-Host $Details }
  exit 1
}

try {
  # 0) Validate dotnet
  Write-Verbose "Checking dotnet availability"
  $dotnetVersion = (& dotnet --version) 2>$null
  if (-not $dotnetVersion) { Fail "dotnet CLI not found in PATH." "Install .NET SDK or ensure 'dotnet' is available." }
  Write-Verbose "dotnet version: $dotnetVersion"

  # 1) Read packages for project
  Write-Info "Collecting packages for $ProjectPath"
  $raw = dotnet list "$ProjectPath" package --include-transitive --format json | Out-String
  $exit = $LASTEXITCODE
  if ($exit -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
    Fail "'dotnet list package' failed (exit $exit) or returned empty output." $raw
  }
  $json = $null
  try {
    $json = $raw | ConvertFrom-Json
  } catch {
    $temp = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $temp -Value $raw -Encoding UTF8
    Fail "Failed to parse JSON from 'dotnet list package'." "Raw output saved to: $temp"
  }
  if (-not $json.projects) {
    Write-Verbose "No projects found in JSON; continuing with empty package list."
  }
  $packagesList = @()
  if ($json) { $packagesList = Get-PackagesFromProjectJson -json $json | Where-Object { $_.Name -and $_.Version } | Sort-Object Name, Version -Unique }
  Write-Info ("dotnet list returned {0} packages" -f $packagesList.Count)
  if ($packagesList.Count -gt 0) {
    Write-Verbose ("Sample from dotnet list: {0}" -f (($packagesList | Select-Object -First 5 | ForEach-Object { "{0} {1}" -f $_.Name, $_.Version }) -join ', '))
  } else {
    Write-Verbose "dotnet list returned no packages or schema not recognized."
  }

  $projDir = Split-Path -Parent $ProjectPath
  $assetsPath = Join-Path $projDir 'obj\project.assets.json'
  $packagesAssets = Get-PackagesFromAssetsJson -AssetsPath $assetsPath
  Write-Info ("assets.json returned {0} packages" -f $packagesAssets.Count)
  if ($packagesAssets.Count -gt 0) {
    Write-Verbose ("Sample from assets.json: {0}" -f (($packagesAssets | Select-Object -First 5 | ForEach-Object { "{0} {1}" -f $_.Name, $_.Version }) -join ', '))
  }
  # If no assets and dotnet list is empty, try a targeted restore then re-read assets
  if ($packagesList.Count -eq 0 -and $packagesAssets.Count -eq 0) {
    Write-Info "No packages from dotnet list or assets.json. Performing 'dotnet restore' and retrying assets parse."
    & dotnet restore "$ProjectPath" | Out-Null
    $packagesAssets = Get-PackagesFromAssetsJson -AssetsPath $assetsPath
    Write-Info ("After restore, assets.json returned {0} packages" -f $packagesAssets.Count)
  }

  # Merge (union) of both sources
  $packages = @()
  $keySet = @{}
  foreach ($pkg in ($packagesList + $packagesAssets)) {
    $key = "{0}|{1}" -f $pkg.Name, $pkg.Version
    if (-not $keySet.ContainsKey($key)) { $keySet[$key] = $true; $packages += $pkg }
  }
  $packages = $packages | Sort-Object Name, Version
  Write-Info ("Merged package count: {0}" -f $packages.Count)
  if ($packages.Count -gt 0) {
    Write-Verbose ("Merged sample: {0}" -f (($packages | Select-Object -First 10 | ForEach-Object { "{0} {1}" -f $_.Name, $_.Version }) -join ', '))
  }

  # Write debug sidecar file with details
  $debugLines = @()
  $debugLines += "dotnet list count: $($packagesList.Count)"
  $debugLines += "assets.json path: $assetsPath"
  $debugLines += "assets.json count: $($packagesAssets.Count)"
  $debugLines += "merged count: $($packages.Count)"
  $debugLines += "dotnet list sample: " + (($packagesList | Select-Object -First 10 | ForEach-Object { "{0} {1}" -f $_.Name, $_.Version }) -join ', ')
  $debugLines += "assets sample: " + (($packagesAssets | Select-Object -First 10 | ForEach-Object { "{0} {1}" -f $_.Name, $_.Version }) -join ', ')
  $debugLines += "merged sample: " + (($packages | Select-Object -First 20 | ForEach-Object { "{0} {1}" -f $_.Name, $_.Version }) -join ', ')

  # 2) Generate markdown
  Write-Info "Generating THIRD-PARTY-NOTICES.md with $($packages.Count) packages"
  $content = New-ThirdPartyNotices -Packages $packages

  # 3) Write to project root and optional output path
  $projDir = Split-Path -Parent $ProjectPath
  $rootOut = Join-Path $projDir 'THIRD-PARTY-NOTICES.md'
  Set-Content -Encoding UTF8 -Path $rootOut -Value $content

  if ($OutputPath -and $OutputPath -ne $rootOut) {
    $outDir = Split-Path -Parent $OutputPath
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
    Set-Content -Encoding UTF8 -Path $OutputPath -Value $content
    Write-Info "Wrote: $rootOut and $OutputPath"
  } else {
    Write-Info "Wrote: $rootOut"
  }

  # Write debug sidecar next to rootOut
  $debugPath = [System.IO.Path]::ChangeExtension($rootOut, '.debug.txt')
  Set-Content -Encoding UTF8 -Path $debugPath -Value ($debugLines -join "`r`n")
  Write-Verbose ("Wrote debug info to {0}" -f $debugPath)
} catch {
  Fail "Unhandled error: $($_.Exception.Message)" $($_.ScriptStackTrace)
}
