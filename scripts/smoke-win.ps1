#Requires -Version 5.1
<#
.SYNOPSIS
  Smoke test of the published win-x64 archive (F-021 S9, R-21, S-42).

.DESCRIPTION
  Extracts artifacts/ControlDeFinanzasIA-<version>-win-x64.zip into a temp folder and starts the extracted executable with
    - CONTROLDEFINANZASIA_DATA_DIR pointing at a throwaway temp folder (the real profile is never touched),
    - DOTNET_ROOT variables removed and every PATH entry that looks like a dotnet install stripped,
  then waits up to -TimeoutSeconds for data.db and a "Migrations applied" line in logs/app-*.log, asserts that the
  process runs the coreclr.dll of the extracted folder (self-contained, not a machine-wide runtime), closes the main
  window (CloseMainWindow) and asserts exit code 0 within 10 s and no startup-error-*.log. Temp folders are deleted.
  Exit code 0 = pass; any failure throws (non-zero exit).
#>
[CmdletBinding()]
param(
    [string]$ZipPath,
    [int]$TimeoutSeconds = 30,
    [switch]$KeepOutput
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'lib/Archive.psm1') -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Get-RepoRoot
$version = Get-AppVersion -RepoRoot $repoRoot
if (-not $ZipPath) { $ZipPath = Join-Path $repoRoot "artifacts/ControlDeFinanzasIA-$version-win-x64.zip" }
if (-not (Test-Path -LiteralPath $ZipPath)) { throw "Archive not found: $ZipPath (run scripts/publish.ps1 -Rid win-x64)." }

$work = Join-Path ([IO.Path]::GetTempPath()) ('cdfia-smoke-' + [Guid]::NewGuid().ToString('N'))
$extract = Join-Path $work 'app'
$dataDir = Join-Path $work 'data'
New-Item -ItemType Directory -Path $extract, $dataDir -Force | Out-Null
$started = Get-Date
$process = $null

try {
    [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $extract)
    $exe = Get-ChildItem -LiteralPath $extract -Recurse -Filter 'ControlDeFinanzasIA.UI.exe' | Select-Object -First 1
    if ($null -eq $exe) { throw 'ControlDeFinanzasIA.UI.exe not found in the archive.' }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe.FullName
    $psi.WorkingDirectory = $exe.DirectoryName
    $psi.UseShellExecute = $false
    foreach ($name in @('DOTNET_ROOT', 'DOTNET_ROOT(x86)', 'DOTNET_ROOT_X64', 'DOTNET_ROOT_X86', 'DOTNET_ROOT_ARM64')) {
        if ($psi.EnvironmentVariables.ContainsKey($name)) { $psi.EnvironmentVariables.Remove($name) }
    }
    $cleanPath = ($env:PATH -split ';' | Where-Object { $_ -and ($_ -notmatch 'dotnet') }) -join ';'
    $psi.EnvironmentVariables['PATH'] = $cleanPath
    $psi.EnvironmentVariables['CONTROLDEFINANZASIA_DATA_DIR'] = $dataDir

    Write-Host "Starting $($exe.FullName)"
    Write-Host "  data dir : $dataDir"
    Write-Host "  dotnet on the child PATH: $(@($cleanPath -split ';' | Where-Object { $_ -match 'dotnet' }).Count) entries"
    $process = [System.Diagnostics.Process]::Start($psi)

    $db = Join-Path $dataDir 'data.db'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) { throw "The application exited early with code $($process.ExitCode)." }
        if (Test-Path -LiteralPath $db) {
            $logs = @(Get-ChildItem -LiteralPath (Join-Path $dataDir 'logs') -Filter 'app-*.log' -ErrorAction SilentlyContinue)
            foreach ($log in $logs) {
                $stream = [IO.File]::Open($log.FullName, 'Open', 'Read', 'ReadWrite')
                try {
                    $text = (New-Object IO.StreamReader($stream)).ReadToEnd()
                }
                finally { $stream.Dispose() }
                if ($text -match 'Migrations applied') { $ready = $true }
            }
        }
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw "No data.db plus 'Migrations applied' log line within $TimeoutSeconds s." }
    Write-Host "  data.db created, log line 'Migrations applied' found after $([int]((Get-Date) - $started).TotalSeconds) s"

    # Environment.ProcessPath for an apphost process is the main module path; coreclr must come from the extracted folder.
    $process.Refresh()
    $mainPath = $process.MainModule.FileName
    $coreclr = $process.Modules | Where-Object { $_.ModuleName -eq 'coreclr.dll' } | Select-Object -First 1
    Write-Host "  main module (Environment.ProcessPath): $mainPath"
    if ($null -eq $coreclr) { throw 'coreclr.dll is not loaded in the process.' }
    Write-Host "  coreclr.dll loaded from: $($coreclr.FileName)"
    if (-not $coreclr.FileName.StartsWith($extract, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The process is not running the self-contained runtime of the extracted archive.'
    }

    # The window handle can lag behind the first log lines.
    $handleDeadline = (Get-Date).AddSeconds(15)
    while ($process.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $handleDeadline) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
    }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'The application has no main window.' }

    [void]$process.CloseMainWindow()
    if (-not $process.WaitForExit(10000)) { throw 'The application did not exit within 10 s of CloseMainWindow.' }
    if ($process.ExitCode -ne 0) { throw "Exit code $($process.ExitCode), expected 0." }
    Write-Host "  closed cleanly, exit code 0"

    $reports = @(Get-ChildItem -LiteralPath $dataDir -Recurse -Filter 'startup-error-*.log' -ErrorAction SilentlyContinue)
    $reports += @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Filter 'startup-error-*.log' -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -ge $started })
    if ($reports.Count -gt 0) { throw "Start-up failure report(s) found: $($reports.FullName -join ', ')" }
    Write-Host 'SMOKE PASSED'
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) }
    if (-not $KeepOutput -and (Test-Path -LiteralPath $work)) {
        for ($i = 0; $i -lt 5; $i++) {
            try { Remove-Item -LiteralPath $work -Recurse -Force; break } catch { Start-Sleep -Seconds 1 }
        }
    }
}
