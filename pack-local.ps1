# Packs Grape.Graphics into ./artifacts/nuget for local consumption by the
# file-based samples in ./samples. The version is suffixed with -local so it
# never collides with a real published version, and so NuGet's global package
# cache always picks up your latest local edits instead of a stale copy.
#
# Usage:
#     ./pack-local.ps1
#     dotnet run samples/TriangleSwarm.cs

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$VersionSuffix = "local-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$proj     = Join-Path $repoRoot 'src/Grape.Graphics/Grape.Graphics.csproj'
$outDir   = Join-Path $repoRoot 'artifacts/nuget'

# Read the project's <Version> and append the suffix.
[xml]$xml = Get-Content $proj
$baseVersion = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $baseVersion) { throw "Could not read <Version> from $proj." }
$packVersion = "$baseVersion-$VersionSuffix"

Write-Host "Packing Grape.Graphics $packVersion -> $outDir" -ForegroundColor Cyan

dotnet pack $proj `
    -c $Configuration `
    -o $outDir `
    /p:Version=$packVersion

if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed." }

Write-Host ""
Write-Host "Done. To run a sample against this build:" -ForegroundColor Green
Write-Host "    dotnet run samples/TriangleSwarm.cs"
Write-Host ""
Write-Host "If samples/TriangleSwarm.cs has '#:package Grape.Graphics@*' it will" -ForegroundColor DarkGray
Write-Host "automatically pick up $packVersion from the local feed." -ForegroundColor DarkGray
