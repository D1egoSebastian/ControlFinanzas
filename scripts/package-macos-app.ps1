#Requires -Version 5.1
<#
.SYNOPSIS
  Wraps a macOS self-contained publish folder into ControlDeFinanzasIA.app (F-021 S9, R-23, DD-12).

.DESCRIPTION
  Layout: ControlDeFinanzasIA.app/Contents/{Info.plist, MacOS/<publish output>, Resources/AppIcon.icns}.
  The executable stays ControlDeFinanzasIA.UI. The bundle is NOT signed or notarized; the version comes from
  Directory.Build.props and the minimum macOS version is the .NET 8 documented minimum.
  The script only builds the folder (it works on any OS with PowerShell); Unix exec bits are applied by the
  archive step in publish.ps1 / the release workflow.

.PARAMETER PublishDir
  The osx-* publish output (artifacts/publish/<rid>).

.PARAMETER OutputDir
  Folder that will contain ControlDeFinanzasIA.app (recreated if the bundle already exists).

.PARAMETER MinMacOS
  LSMinimumSystemVersion. 12.0 is the minimum supported by .NET 8 (macOS 12 Monterey).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDir,
    [Parameter(Mandatory)][string]$OutputDir,
    [string]$MinMacOS = '12.0'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'lib/Archive.psm1') -Force

$repoRoot = Get-RepoRoot
$version = Get-AppVersion -RepoRoot $repoRoot

if (-not (Test-Path -LiteralPath $PublishDir -PathType Container)) {
    throw "The publish folder does not exist: $PublishDir (run scripts/publish.ps1 first)."
}
if (-not (Test-Path -LiteralPath (Join-Path $PublishDir 'ControlDeFinanzasIA.UI'))) {
    throw "The publish folder has no ControlDeFinanzasIA.UI executable: $PublishDir"
}

$template = Join-Path $repoRoot 'packaging/macos/Info.plist.template'
$icon = Join-Path $repoRoot 'src/ControlDeFinanzasIA.UI/Assets/Icon/app-icon.icns'
foreach ($required in @($template, $icon)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing packaging input: $required" }
}

$app = Join-Path $OutputDir 'ControlDeFinanzasIA.app'
if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force }

$macOS = Join-Path $app 'Contents/MacOS'
$resources = Join-Path $app 'Contents/Resources'
New-Item -ItemType Directory -Path $macOS -Force | Out-Null
New-Item -ItemType Directory -Path $resources -Force | Out-Null

Copy-Item -Path (Join-Path $PublishDir '*') -Destination $macOS -Recurse -Force
Copy-Item -LiteralPath $icon -Destination (Join-Path $resources 'AppIcon.icns') -Force

$plist = (Get-Content -LiteralPath $template -Raw).Replace('@@VERSION@@', $version).Replace('@@MIN_MACOS@@', $MinMacOS)
if ($plist.Contains('@@')) { throw 'Info.plist still contains an unreplaced placeholder.' }
[IO.File]::WriteAllText((Join-Path $app 'Contents/Info.plist'), $plist, (New-Object System.Text.UTF8Encoding($false)))

# Self-check: the plist must parse and carry the required keys.
[xml]$parsed = Get-Content -LiteralPath (Join-Path $app 'Contents/Info.plist') -Raw
$keys = @($parsed.plist.dict.key)
foreach ($key in 'CFBundleExecutable', 'CFBundleIdentifier', 'CFBundleShortVersionString', 'CFBundleVersion',
    'CFBundleIconFile', 'NSHighResolutionCapable', 'LSMinimumSystemVersion') {
    if ($keys -notcontains $key) { throw "Info.plist is missing the key $key." }
}

Write-Host "Created $app (version $version, minimum macOS $MinMacOS)."
