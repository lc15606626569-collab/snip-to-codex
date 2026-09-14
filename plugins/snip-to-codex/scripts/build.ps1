$ErrorActionPreference = 'Stop'
$pluginRoot = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$binaryDir = Join-Path $pluginRoot 'bin'
New-Item -ItemType Directory -Force -Path $binaryDir | Out-Null
$options = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', "/out:$binaryDir/SnipToCodex.exe", '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', "/reference:$framework/WPF/UIAutomationClient.dll", "/reference:$framework/WPF/UIAutomationTypes.dll", "/reference:$framework/WPF/WindowsBase.dll")
$iconPath = Join-Path $pluginRoot 'assets/app.ico'
if (Test-Path -LiteralPath $iconPath) { $options += "/win32icon:$iconPath" }
& $compiler @options (Join-Path $PSScriptRoot 'SnipToCodex.cs') (Join-Path $PSScriptRoot 'Interface.cs') (Join-Path $PSScriptRoot 'Shortcuts.cs')
if ($LASTEXITCODE -ne 0) { throw 'Screenshot helper compilation failed.' }
Write-Output (Join-Path $binaryDir 'SnipToCodex.exe')
