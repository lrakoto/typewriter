param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ("Typewriter portable test " + [Guid]::NewGuid().ToString('N'))
$original = Join-Path $fixture 'Original folder'
$moved = Join-Path $fixture 'Moved — Écrire'
$unrelated = Join-Path $fixture 'Different working directory'
New-Item -ItemType Directory -Path $original, $unrelated -Force | Out-Null
Copy-Item $Executable (Join-Path $original 'Typewriter.exe')
function Run-Check([string]$folder, [switch]$Verify) {
    $arguments = @('--portable-smoke-test')
    if ($Verify) { $arguments += '--verify-existing' }
    $process = Start-Process -FilePath (Join-Path $folder 'Typewriter.exe') -ArgumentList $arguments -WorkingDirectory $unrelated -PassThru
    if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'Portable app did not finish its startup check.' }
    if ($process.ExitCode -ne 0) { throw "Portable executable failed: $(Get-Content (Join-Path $folder 'portable-smoke-test.json') -Raw)" }
    $report = Get-Content (Join-Path $folder 'portable-smoke-test.json') -Raw | ConvertFrom-Json
    if (-not $report.passed) { throw $report.error }
    if (Test-Path (Join-Path $unrelated 'TypewriterData')) { throw 'The app stored data relative to the working directory.' }
}
try {
    Run-Check $original
    Move-Item $original $moved
    Run-Check $moved -Verify
    Write-Output 'PASS: Shipped single-file EXE launches, opens the native editor, and preserves its notes/settings after moving folders.'
} finally { Remove-Item $fixture -Recurse -Force }
