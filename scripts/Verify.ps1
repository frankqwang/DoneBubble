$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sourceExe = Join-Path $root 'bin\Release\net8.0-windows\win-x64\publish\DoneBubble.exe'
$result = Join-Path $root 'verification.txt'
# A local copy avoids Windows' open-file prompt for WSL UNC shares.
$local = Join-Path $env:TEMP ('DoneBubble-verify-' + [Guid]::NewGuid())
New-Item -ItemType Directory $local | Out-Null
try {
    $exe = Join-Path $local 'DoneBubble.exe'
    Copy-Item $sourceExe $exe
    Unblock-File $exe
    if (Test-Path $result) { Remove-Item $result }
    $process = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $result + '"')) -PassThru
    if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'Diagnostic timed out.' }
    if (-not (Test-Path $result)) { throw "No result, exit code: $($process.ExitCode)" }
    Get-Content $result
    if ($process.ExitCode -ne 0) { throw 'Diagnostic failed.' }
} finally { Remove-Item $local -Recurse -Force -ErrorAction SilentlyContinue }
