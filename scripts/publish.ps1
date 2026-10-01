#Requires -Version 5.1
<#
.SYNOPSIS
  Self-contained publish, archives and checksums for ControlDeFinanzasIA (F-021 S9, R-21, R-22, DD-12).

.DESCRIPTION
  For each RID: `dotnet publish src/ControlDeFinanzasIA.UI -c Release -r <rid>` into artifacts/publish/<rid>/, then
    win-x64            -> artifacts/ControlDeFinanzasIA-<version>-win-x64.zip
    osx-arm64, osx-x64 -> artifacts/ControlDeFinanzasIA-<version>-<rid>.tar.gz
                          artifacts/ControlDeFinanzasIA-<version>-<rid>.app.zip (unsigned .app bundle)
    linux-x64          -> artifacts/ControlDeFinanzasIA-<version>-linux-x64.tar.gz
  and artifacts/SHA256SUMS.txt (`sha256sum` format) over every archive in artifacts/.
  The version is read from Directory.Build.props, never typed. artifacts/ is git-ignored.

  Archives are written by scripts/lib/Archive.psm1, which sets Unix modes itself (0755 for ControlDeFinanzasIA.UI and
  createdump), so the script runs on Windows PowerShell 5.1 and PowerShell 7 on any host OS. Only the win-x64 path has
  been executed by the author (see docs/packaging-unverified.md).

.PARAMETER Rid
  win-x64, osx-arm64, osx-x64, linux-x64 or all.

.PARAMETER SkipPublish
  Re-archive an existing artifacts/publish/<rid> folder without running dotnet publish.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'osx-arm64', 'osx-x64', 'linux-x64', 'all')]
    [string]$Rid = 'all',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'lib/Archive.psm1') -Force

$repoRoot = Get-RepoRoot
$version = Get-AppVersion -RepoRoot $repoRoot
$artifacts = Join-Path $repoRoot 'artifacts'
$publishRoot = Join-Path $artifacts 'publish'
$project = Join-Path $repoRoot 'src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj'
$allRids = @('win-x64', 'osx-arm64', 'osx-x64', 'linux-x64')
$rids = if ($Rid -eq 'all') { $allRids } else { @($Rid) }

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

foreach ($current in $rids) {
    $out = Join-Path $publishRoot $current
    Write-Host "=== $current ==="

    if (-not $SkipPublish) {
        if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
        & dotnet publish $project -c Release -r $current -o $out --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $current (exit $LASTEXITCODE)." }
    }
    if (-not (Test-Path -LiteralPath $out -PathType Container)) {
        throw "Publish output not found: $out"
    }

    $baseName = "ControlDeFinanzasIA-$version-$current"
    $unixExecutables = @('ControlDeFinanzasIA.UI', 'createdump')

    if ($current -eq 'win-x64') {
        $zip = Join-Path $artifacts "$baseName.zip"
        New-ZipArchive -SourceDir $out -DestinationPath $zip -EntryPrefix $baseName
        Write-Host "  $zip"
    }
    else {
        $tar = Join-Path $artifacts "$baseName.tar.gz"
        New-TarGzArchive -SourceDir $out -DestinationPath $tar -EntryPrefix $baseName -ExecutableEntries $unixExecutables
        Write-Host "  $tar"
    }

    if ($current -like 'osx-*') {
        $bundleDir = Join-Path $publishRoot "$current-app"
        if (Test-Path -LiteralPath $bundleDir) { Remove-Item -LiteralPath $bundleDir -Recurse -Force }
        & (Join-Path $PSScriptRoot 'package-macos-app.ps1') -PublishDir $out -OutputDir $bundleDir
        $appExecutables = $unixExecutables | ForEach-Object { "ControlDeFinanzasIA.app/Contents/MacOS/$_" }
        $appZip = Join-Path $artifacts "$baseName.app.zip"
        New-ZipArchive -SourceDir $bundleDir -DestinationPath $appZip -ExecutableEntries $appExecutables
        Write-Host "  $appZip"
    }
}

$archives = @(Get-ChildItem -LiteralPath $artifacts -File |
        Where-Object { $_.Name -like "ControlDeFinanzasIA-$version-*" -and ($_.Name -like '*.zip' -or $_.Name -like '*.tar.gz') } |
        ForEach-Object { $_.FullName })
if ($archives.Count -eq 0) { throw 'No archive was produced.' }
Write-Sha256Sums -Files $archives -DestinationPath (Join-Path $artifacts 'SHA256SUMS.txt')
Write-Host "Wrote $(Join-Path $artifacts 'SHA256SUMS.txt') ($($archives.Count) archives)."
