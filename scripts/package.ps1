param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$plugin=Join-Path $repo 'plugins/snip-to-codex'
$manifest=Get-Content -LiteralPath (Join-Path $plugin '.codex-plugin/plugin.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version=($manifest.version -split '\+')[0]
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo 'dist'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
if(-not(Test-Path -LiteralPath (Join-Path $plugin 'bin/SnipToCodex.exe'))){throw 'Run scripts/build.ps1 inside the plugin first.'}
$staging=Join-Path (Join-Path $repo 'dist') ('stage-'+[guid]::NewGuid().ToString('N'))
$bundle=Join-Path $staging 'SnipToCodex'
New-Item -ItemType Directory -Force -Path $bundle | Out-Null
foreach($name in @('bin','assets','docs','scripts','skills','.codex-plugin','Start.cmd','Install.cmd','Uninstall.cmd','README.md','LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $plugin $name) -Destination $bundle -Recurse -Force
}
$zip=Join-Path $OutputDirectory "SnipToCodex-v$version-windows-x64.zip"
Compress-Archive -LiteralPath $bundle -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'),($hash+'  '+[IO.Path]::GetFileName($zip)+"`n"),[Text.Encoding]::ASCII)
Write-Output $zip
