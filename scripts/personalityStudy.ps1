<#
.SYNOPSIS
    Personality Study script — runs the SimRunner for each island actor and
    archives all generated artifacts into a single timestamped zip.

.DESCRIPTION
    Iterates every actor defined in Archetypes.cs, runs the SimRunner with
    decision tracing and artifact saving enabled, then zips all outputs into
    artifacts/personality-study-seed<seed>-<timestamp>.zip.

.PARAMETER Seed
    RNG seed passed to each simulation run. Default: 42.

.PARAMETER Duration
    Simulation duration in seconds. Default: 864000 (10 in-game days).

.EXAMPLE
    .\scripts\personalityStudy.ps1
    .\scripts\personalityStudy.ps1 -Seed 99 -Duration 86400
#>
param(
    [int]$Seed     = 42,
    [int]$Duration = 864000
)

$ErrorActionPreference = 'Stop'

# Resolve the repo root from the script's own location
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Dynamically read actor names from Archetypes.cs so this list stays in sync.
# Matches C# dictionary entries of the form: ["Name"] = new()
$archetypesFile = Join-Path 'src' 'JohnnyLike.SimRunner' 'Archetypes.cs'
$actors = [regex]::Matches(
    (Get-Content $archetypesFile -Raw),
    '\["([^"]+)"\]\s*=\s*new\s*\('
) | ForEach-Object { $_.Groups[1].Value }

if ($actors.Count -eq 0) {
    Write-Error "Could not read actor names from $archetypesFile"
    exit 1
}

$timestamp   = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$studySubDir = "personality-study-seed${Seed}-${timestamp}"

Write-Host "=== Personality Study ==="
Write-Host "Seed:     $Seed"
Write-Host "Duration: ${Duration}s"
Write-Host "Actors:   $($actors -join ' ')"
Write-Host "Subdir:   $(Join-Path 'artifacts' $studySubDir)"
Write-Host ""

foreach ($actor in $actors) {
    Write-Host "--- Running simulation for actor: $actor ---"
    dotnet run --project src/JohnnyLike.SimRunner -- `
        --domain island `
        --seed $Seed `
        --trace `
        --actor $actor `
        --duration $Duration `
        --decision-verbose `
        --save-artifacts $studySubDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "SimRunner failed for actor '$actor' (exit code $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
    Write-Host ""
}

Write-Host "=== All simulations complete. Archiving artifacts... ==="

$studyDir = Join-Path 'artifacts' $studySubDir
$zipName  = Join-Path 'artifacts' "personality-study-seed${Seed}-${timestamp}.zip"

$artifactFiles = @()
if (Test-Path $studyDir) {
    $artifactFiles = Get-ChildItem -Path $studyDir -File -Recurse
}

if ($artifactFiles.Count -eq 0) {
    Write-Error "No artifact files found to archive."
    exit 1
}

Compress-Archive -Path (Join-Path $studyDir '*') -DestinationPath $zipName

Write-Host ""
Write-Host "Archived $($artifactFiles.Count) file(s) to: $zipName"
