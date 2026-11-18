param([ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot "dist/windows/Typewriter-$Runtime"
$architecture = $Runtime.Replace('win-', '')
$archive = Join-Path $repoRoot "dist/windows/Typewriter-Windows-$architecture.zip"
dotnet publish (Join-Path $repoRoot 'windows/Typewriter/Typewriter.csproj') -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
$files = @(Get-ChildItem $output -File)
if ($files.Count -ne 1 -or $files[0].Name -ne 'Typewriter.exe') { throw 'Expected exactly one self-contained executable.' }
Compress-Archive -Path (Join-Path $output 'Typewriter.exe'), (Join-Path $repoRoot 'windows/PORTABLE-README.txt') -DestinationPath $archive -Force
Write-Output "Portable archive: $archive"
