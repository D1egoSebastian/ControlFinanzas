#Requires -Version 5.1
<#
.SYNOPSIS
  Asserts that a self-contained publish folder has the executable and the native libraries for its RID
  (F-021 S9, R-21, S-43).

.DESCRIPTION
  The expected file names were taken from the first real publish of each RID (win-x64, osx-arm64, osx-x64, linux-x64
  with .NET SDK 8.0.422 and the packages in Directory.Packages.props), not guessed. Besides existence, each native file
  must be larger than 4 KB and start with the magic bytes of its platform (PE "MZ", ELF 0x7F "ELF", Mach-O 64-bit or
  universal), which catches a library of the wrong platform. The es-DO satellite resources must be present and no
  ControlDeFinanzasIA.UI.pdb may be shipped (DebugType=embedded).
  Exit code 0 = layout ok; otherwise the missing or wrong items are listed and the exit code is 1.

.PARAMETER Rid
  win-x64, osx-arm64, osx-x64 or linux-x64.

.PARAMETER PublishDir
  Defaults to artifacts/publish/<rid>.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'osx-arm64', 'osx-x64', 'linux-x64')][string]$Rid,
    [string]$PublishDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $PublishDir) { $PublishDir = Join-Path $repoRoot "artifacts/publish/$Rid" }

if (-not (Test-Path -LiteralPath $PublishDir -PathType Container)) {
    Write-Error "Publish folder missing: $PublishDir (run scripts/publish.ps1 -Rid $Rid first)."
    exit 1
}

$expected = @{
    'win-x64'   = @{
        Executable = 'ControlDeFinanzasIA.UI.exe'
        Magic      = 'pe'
        Natives    = @('libSkiaSharp.dll', 'libHarfBuzzSharp.dll', 'e_sqlite3.dll', 'QuestPdfSkia.dll', 'qpdf.dll', 'coreclr.dll', 'hostfxr.dll')
    }
    'osx-arm64' = @{
        Executable = 'ControlDeFinanzasIA.UI'
        Magic      = 'macho'
        Natives    = @('libSkiaSharp.dylib', 'libHarfBuzzSharp.dylib', 'libAvaloniaNative.dylib', 'libe_sqlite3.dylib', 'libQuestPdfSkia.dylib', 'libqpdf.dylib', 'libcoreclr.dylib', 'libhostfxr.dylib')
    }
    'osx-x64'   = @{
        Executable = 'ControlDeFinanzasIA.UI'
        Magic      = 'macho'
        Natives    = @('libSkiaSharp.dylib', 'libHarfBuzzSharp.dylib', 'libAvaloniaNative.dylib', 'libe_sqlite3.dylib', 'libQuestPdfSkia.dylib', 'libqpdf.dylib', 'libcoreclr.dylib', 'libhostfxr.dylib')
    }
    'linux-x64' = @{
        Executable = 'ControlDeFinanzasIA.UI'
        Magic      = 'elf'
        Natives    = @('libSkiaSharp.so', 'libHarfBuzzSharp.so', 'libe_sqlite3.so', 'libQuestPdfSkia.so', 'libqpdf.so', 'libcoreclr.so', 'libhostfxr.so')
    }
}[$Rid]

function Test-Magic([string]$path, [string]$kind) {
    $stream = [IO.File]::OpenRead($path)
    try {
        $head = New-Object byte[] 4
        [void]$stream.Read($head, 0, 4)
    }
    finally { $stream.Dispose() }
    $hex = ($head | ForEach-Object { $_.ToString('X2') }) -join ''
    switch ($kind) {
        'pe' { return $hex.StartsWith('4D5A') }
        'elf' { return $hex -eq '7F454C46' }
        'macho' { return ($hex -eq 'CFFAEDFE' -or $hex -eq 'CAFEBABE' -or $hex -eq 'BEBAFECA') }
    }
    return $false
}

$problems = New-Object System.Collections.Generic.List[string]
foreach ($name in (@($expected.Executable) + $expected.Natives)) {
    $path = Join-Path $PublishDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $problems.Add("missing: $name"); continue }
    if ((Get-Item -LiteralPath $path).Length -lt 4096) { $problems.Add("too small: $name"); continue }
    if ($name -ne $expected.Executable -or $expected.Magic -ne 'pe') {
        if (-not (Test-Magic $path $expected.Magic)) { $problems.Add("wrong platform (magic bytes): $name") }
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $PublishDir 'es-DO/ControlDeFinanzasIA.UI.resources.dll'))) {
    $problems.Add('missing: es-DO/ControlDeFinanzasIA.UI.resources.dll')
}
if (Test-Path -LiteralPath (Join-Path $PublishDir 'ControlDeFinanzasIA.UI.pdb')) {
    $problems.Add('unexpected: ControlDeFinanzasIA.UI.pdb (DebugType must be embedded)')
}

if ($problems.Count -gt 0) {
    Write-Error ("Layout check FAILED for $Rid in ${PublishDir}:`n  " + ($problems -join "`n  "))
    exit 1
}
Write-Host "Layout OK for $Rid ($(@($expected.Natives).Count) native libraries + executable) in $PublishDir"
exit 0
