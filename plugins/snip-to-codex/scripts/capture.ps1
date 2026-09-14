param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
if (-not $OutputPath) { $OutputPath = Join-Path $env:LOCALAPPDATA ('SnipToCodex/Captures/snip-' + [guid]::NewGuid().ToString('N') + '.png') }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) 'bin/SnipToCodex.exe'
if (-not (Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'build.ps1') | Out-Null }
$process = Start-Process -FilePath $exe -ArgumentList @('--capture', ('"' + $OutputPath + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -eq 3) { Write-Output 'CANCELLED'; exit 0 }
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $OutputPath)) { throw 'Screenshot failed. Check LOCALAPPDATA/SnipToCodex/status.txt.' }
Write-Output $OutputPath
