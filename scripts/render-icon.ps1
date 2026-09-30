#Requires -Version 5.1
<#
.SYNOPSIS
    Re-renders the application icon set (PNG sizes, ICO, ICNS) from the SVG sources.
.DESCRIPTION
    Runs tools/IconRenderer over src/ControlDeFinanzasIA.UI/Assets/Icon. The outputs are committed, so this
    script only needs to run when app-icon.svg or app-icon-small.svg changes.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$iconDir = Join-Path $root 'src/ControlDeFinanzasIA.UI/Assets/Icon'
dotnet run --project (Join-Path $root 'tools/IconRenderer') -c Release -- $iconDir
if ($LASTEXITCODE -ne 0) { throw "IconRenderer failed with exit code $LASTEXITCODE" }
