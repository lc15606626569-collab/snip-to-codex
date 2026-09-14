$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$exe=Join-Path $root 'bin/SnipToCodex.exe'
if(Test-Path -LiteralPath $exe) { Start-Process -FilePath $exe -ArgumentList '--quit' -WindowStyle Hidden -Wait }
$shell=New-Object -ComObject WScript.Shell
foreach($dir in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) {
    $path=Join-Path $dir 'Snip to Codex.lnk'
    if(Test-Path -LiteralPath $path) { $link=$shell.CreateShortcut($path);if($link.TargetPath -eq $exe){Remove-Item -LiteralPath $path} }
}
Write-Output 'Shortcuts removed. Your saved screenshots are preserved.'
Write-Output "You may now delete this application folder manually: $root"
