param([string]$InstallRoot, [switch]$NoLaunch, [switch]$NoShortcuts)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not $InstallRoot) { $InstallRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/SnipToCodex' }
$InstallRoot=[IO.Path]::GetFullPath($InstallRoot)
$exe=Join-Path $InstallRoot 'bin/SnipToCodex.exe'
if (Test-Path -LiteralPath $exe) {
    $running=@(Get-Process SnipToCodex -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
    if($running.Count) {
        Start-Process -FilePath $exe -ArgumentList '--quit' -WindowStyle Hidden -Wait
        foreach($item in $running) { if(-not $item.WaitForExit(5000)) { throw 'Please exit Snip to Codex from the tray and try again.' } }
    }
}
if ($source -ne $InstallRoot) {
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    foreach($name in @('bin','assets','docs','scripts','skills','.codex-plugin','Start.cmd','Install.cmd','Uninstall.cmd','README.md','LICENSE')) {
        $path=Join-Path $source $name
        if(Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $InstallRoot -Recurse -Force }
    }
}
if(-not (Test-Path -LiteralPath $exe)) { throw 'Executable missing. Download the Windows release ZIP and extract the entire folder first.' }
if(-not $NoShortcuts) {
    $shell=New-Object -ComObject WScript.Shell
    foreach($dir in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) {
        $link=$shell.CreateShortcut((Join-Path $dir 'Snip to Codex.lnk'))
        $link.TargetPath=$exe;$link.WorkingDirectory=$InstallRoot;$link.Description='Snip to Codex - Ctrl+Alt+S';$link.Save()
    }
}
Write-Output "Installed: $InstallRoot"
if(-not $NoLaunch) { Start-Process -FilePath $exe -WindowStyle Hidden }
